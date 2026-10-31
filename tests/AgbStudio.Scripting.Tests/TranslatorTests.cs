using AgbStudio.Build;

namespace AgbStudio.Scripting.Tests;

/// <summary>P6.04-P6.09: C# -> C++20 translation, golden files, line mapping, Inspector fields, namespace, templates, API.</summary>
public sealed class TranslatorTests
{
    static readonly Lazy<ScriptCompileResult> s_figma = new(() =>
        TestSupport.Compile("translator-figma", TestSupport.Context(), TestSupport.FigmaScripts()));

    public static TheoryData<string> GoldenFiles() =>
    [
        "PlayerController.hpp", "PlayerController.cpp", "Coin.hpp", "SlimeController.hpp", "SlimeController.cpp", "SlimeMood.hpp",
        "Forest.cpp", "ShopNatives.hpp", "agb_scripts.hpp", "agb_scripts.cpp", "agb_prelude.hpp", "GBA.Project.g.cs",
    ];

    [Theory]
    [MemberData(nameof(GoldenFiles))]
    public void TranslationMatchesGolden(string file)
    {
        var r = s_figma.Value;
        Assert.True(r.Success, TestSupport.Errors(r));
        TestSupport.AssertGolden(file, File.ReadAllText(Path.Combine(r.IncludeDir, file)));
    }

    [Fact]
    public void GeneratedCppCompilesWithRuntimeFlags()
    {
        var r = s_figma.Value;
        Assert.True(r.Success, TestSupport.Errors(r));
        Assert.Equal("", TestSupport.CompileCpp(r, debug: true));
    }

    [Fact]
    public void ReleaseBuildHasNoProbesAndCompiles()
    {
        var r = TestSupport.Compile("translator-release", TestSupport.Context(debug: false), TestSupport.FigmaScripts());
        Assert.True(r.Success, TestSupport.Errors(r));
        var cpp = File.ReadAllText(Path.Combine(r.IncludeDir, "PlayerController.cpp"));
        Assert.DoesNotContain("AGB_PROBE", cpp);
        Assert.DoesNotContain("AGB_SCOPE", cpp);
        Assert.Contains("#line 14 \"Scripts/PlayerController.cs\"", cpp);
        Assert.Equal("", TestSupport.CompileCpp(r, debug: false));
    }

    [Fact]
    public void NoExceptionsNoRttiNoHeap()
    {
        var r = s_figma.Value;
        foreach (var f in Directory.EnumerateFiles(r.IncludeDir, "*.?pp"))
        {
            var text = File.ReadAllText(f);
            Assert.DoesNotContain("throw", text);
            Assert.DoesNotContain("dynamic_cast", text);
            Assert.DoesNotContain("typeid", text);
            Assert.DoesNotContain(" new ", text.Replace("// ", "").Replace("new (", "")); // only arena placement
            Assert.DoesNotContain("malloc", text);
        }
    }

    [Fact]
    public void FilesAreWrittenOnlyWhenContentChanges()
    {
        var dir = TestSupport.Scratch("translator-incremental");
        var ctx = TestSupport.Context();
        var sources = TestSupport.FigmaScripts().ToList();
        var first = new ScriptCompiler().Compile(sources, ctx, dir);
        Assert.True(first.Success, TestSupport.Errors(first));
        Assert.NotEmpty(first.Changed);
        var stamp = File.GetLastWriteTimeUtc(Path.Combine(dir, "Coin.cpp"));
        var second = new ScriptCompiler().Compile(sources, ctx, dir);
        Assert.Empty(second.Changed);
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(Path.Combine(dir, "Coin.cpp")));
        // A change inside one method body rewrites that class's .cpp only.
        var i = sources.FindIndex(s => s.Path.EndsWith("PlayerController.cs"));
        sources[i] = sources[i] with { Text = sources[i].Text.Replace("Game.score += 10;", "Game.score += 20;") };
        var third = new ScriptCompiler().Compile(sources, ctx, dir);
        Assert.Equal(["PlayerController.cpp"], third.Changed.Select(Path.GetFileName));
        // A removed class disappears from the output folder.
        sources.RemoveAt(sources.FindIndex(s => s.Path.EndsWith("SlimeController.cs")));
        var fourth = new ScriptCompiler().Compile(sources, ctx, dir);
        Assert.True(fourth.Success, TestSupport.Errors(fourth));
        Assert.False(File.Exists(Path.Combine(dir, "SlimeController.cpp")));
        Assert.True(File.Exists(Path.Combine(dir, "PlayerController.cpp")));
    }

    [Fact]
    public void PlantedSyntaxErrorReportsFileLineColumn()
    {
        // Figma Build log: "PlayerController.cs(22,9): ';' expected"
        var lines = File.ReadAllLines(Path.Combine(TestSupport.ScriptsDir, "PlayerController.cs")).ToList();
        lines.Insert(21, "   int x");  // becomes line 22; the missing semicolon is at column 9
        var r = TestSupport.Compile("translator-syntax", TestSupport.Context(), TestSupport.Inline("PlayerController.cs", string.Join("\n", lines)));
        Assert.False(r.Success);
        var d = r.Diagnostics.First(x => x.IsError);
        Assert.Equal("PlayerController.cs(22,9): ';' expected", d.ToLogLine());
    }

    [Fact]
    public void GxxErrorsMapBackToCsLines()
    {
        // P6.05: an error in generated C++ (planted here) is reported at the .cs line through #line.
        var r = TestSupport.Compile("translator-gxx", TestSupport.Context(), TestSupport.FigmaScripts());
        Assert.True(r.Success, TestSupport.Errors(r));
        var cpp = Path.Combine(r.IncludeDir, "PlayerController.cpp");
        var text = File.ReadAllText(cpp);
        Assert.Contains("body->vy = -jumpPower;", text);
        File.WriteAllText(cpp, text.Replace("body->vy = -jumpPower;", "body->vy = -jumpPowr;"));
        var log = TestSupport.CompileCpp(r, only: [cpp]);
        var errors = GccLogMapper.Map(log).Where(d => d.IsError).ToList();
        var e = Assert.Single(errors);
        Assert.Equal("Scripts/PlayerController.cs", e.File);
        Assert.Equal(17, e.Line);
        Assert.Contains("jumpPowr", e.Message);
        Assert.StartsWith("PlayerController.cs(17,", e.ToLogLine());
    }

    [Fact]
    public void InspectorFieldsAndFactories()
    {
        var r = s_figma.Value;
        var pc = r.Behaviours.Single(b => b.Name == "PlayerController");
        Assert.Equal(["speed", "jumpPower", "lives"], pc.Fields.Select(f => f.Name));
        var speed = pc.Fields[0];
        Assert.Equal((1, "1.5", 0.5, 3.0), (speed.Kind, speed.Default, speed.Min!.Value, speed.Max!.Value));
        Assert.Equal(2, r.Behaviours.Single(b => b.Name == "Coin").Fields[1].Kind);
        var slime = r.Behaviours.Single(b => b.Name == "SlimeController");
        Assert.Equal(["Calm", "Angry"], slime.Fields.Single(f => f.Name == "mood").EnumValues);
        Assert.Equal("agb_make_PlayerController(arena)", r.FactoryCall("PlayerController", new Dictionary<string, string>()));
        Assert.Equal("agb_make_PlayerController_with(arena, agb_init_PlayerController{.speed = agb::fixed::fromRaw(512), .lives = 5})",
            r.FactoryCall("PlayerController", new Dictionary<string, string> { ["lives"] = "5", ["speed"] = "2" }));
        Assert.Equal("agb_make_SlimeController_with(arena, agb_init_SlimeController{.mood = ::SlimeMood::Angry})",
            r.FactoryCall("SlimeController", new Dictionary<string, string> { ["mood"] = "Angry" }));
        Assert.Null(r.FactoryCall("Nope", new Dictionary<string, string>()));
        // Type ids: 64 + stable index (kept across builds through TypeIndices).
        Assert.Equal(64, r.Behaviours.Single(b => b.Name == "Coin").TypeId);
        var ctx = TestSupport.Context();
        ctx.TypeIndices = new() { ["SlimeController"] = 0 };
        var again = TestSupport.Compile("translator-typeids", ctx, TestSupport.FigmaScripts());
        Assert.Equal(64, again.Behaviours.Single(b => b.Name == "SlimeController").TypeId);
        Assert.Equal(0, again.TypeIndices["SlimeController"]);
    }

    [Fact]
    public void ProjectNamespaceSetting()
    {
        // P6.07: classes outside any C# namespace go into the project namespace in C++.
        var ctx = TestSupport.Context();
        ctx.Namespace = "SkyHopper";
        var r = TestSupport.Compile("translator-ns", ctx, TestSupport.FigmaScripts());
        Assert.True(r.Success, TestSupport.Errors(r));
        Assert.Contains("namespace SkyHopper {", File.ReadAllText(Path.Combine(r.IncludeDir, "PlayerController.hpp")));
        Assert.Contains("arena.make<::SkyHopper::PlayerController>()", File.ReadAllText(Path.Combine(r.IncludeDir, "PlayerController.cpp")));
        Assert.Equal("::SkyHopper::PlayerController", r.Behaviours.Single(b => b.Name == "PlayerController").CppName);
        Assert.Equal("", TestSupport.CompileCpp(r));
        // A C# namespace wins over the setting.
        var src = "using GBA;\n\nnamespace SkyHopper.Enemies;\n\npublic class Bat : Behaviour { public int hp = 2; }\n";
        var r2 = TestSupport.Compile("translator-ns2", ctx, TestSupport.Inline("Bat.cs", src));
        Assert.Equal("::SkyHopper::Enemies::Bat", r2.Behaviours.Single().CppName);
        Assert.StartsWith("using GBA;\n\nnamespace SkyHopper;", ScriptTemplates.Create(ScriptTemplates.Kind.Behaviour, "Bat", "SkyHopper"));
    }

    [Theory]
    [InlineData(ScriptTemplates.Kind.Behaviour)]
    [InlineData(ScriptTemplates.Kind.StaticHelper)]
    [InlineData(ScriptTemplates.Kind.SceneScript)]
    public void TemplatesCompileClean(ScriptTemplates.Kind kind)
    {
        // P6.08: each New Script template compiles without any diagnostic and translates to warning-free C++.
        var text = ScriptTemplates.Create(kind, "NewScript", "SkyHopper");
        var r = TestSupport.Compile("translator-template-" + kind, TestSupport.Context(), TestSupport.Inline("NewScript.cs", text));
        Assert.Empty(r.Diagnostics);
        Assert.Equal("", TestSupport.CompileCpp(r));
    }

    [Fact]
    public void ScriptingApiMembersTranslate()
    {
        // P6.09: every member of the Scripting API page and the Figma samples, with the C++ it becomes.
        var src = File.ReadAllText(Path.Combine(TestSupport.ScriptsDir, "ApiTour.cs"));
        var r = TestSupport.Compile("translator-api", TestSupport.Context(), TestSupport.Inline("ApiTour.cs", src), TestSupport.Script("Coin.cs"));
        Assert.True(r.Success, TestSupport.Errors(r));
        var cpp = File.ReadAllText(Path.Combine(r.IncludeDir, "ApiTour.cpp"));
        string[] expected =
        [
            "agb::api::Input::Pressed(agb::Key::A)", "agb::api::Input::Held(agb::Key::B)", "agb::api::Input::Released(agb::Key::Start)",
            "agb::api::Input::AxisX()", "agb::api::Input::AxisY()", "agb::api::Input::AnyKey()",
            "agb::Key::Select", "agb::Key::Right", "agb::Key::Left", "agb::Key::Up", "agb::Key::Down", "agb::Key::R", "agb::Key::L",
            "actor->get<agb::PlatformerBody>()", "other.is<::Coin>()", "other.destroy()", "other.tag == agb_tag::Enemy", "actor->pos.x",
            "agb::api::Sound::Play(agb_sound::SFX_JUMP, agb::fixed::lit(0.8))", "agb::api::Music::Play(agb_sound::MOD_HELP)", "agb::api::Music::Stop()",
            "agb::api::Camera::Shake(2, 10)", "agb::api::Camera::X()", "agb::api::Camera::Follow(actor)",
            "agb::api::Save::Write(1)", "agb::api::Save::Read(1)", "agb::api::Scene::Load(static_cast<agb::u16>(agb::api::Save::scene()))",
            "agb::api::Save::Checkpoint()", "agb::script::natives::showText(agb_text::forest_help_me)",
            "agb_db::Items::Get(agb_db::ItemId::Potion)", "health->heal(potion.power)", "agbgen::shopBuy_Items(agb_db::ItemId::Potion)",
            "agb_state.coins >= potion.price", "agb::abs(", "agb::math::sin(static_cast<agb::u8>(64))", "agb::clamp(",
            "agb::api::Scene::Load(static_cast<agb::u16>(agb_scene::Forest_02))", "agb::api::Scene::Pause()",
            "agb_state.score", "agb::api::Debug::Log(\"hello\")", "agb::api::Random::Range(1, 6)",
            "agb_bit_set(agb_state.collected, 3, true)", "static_cast<agb_db::ItemId>(agb_state.bag[0])",
            "agb::script::setVar(0x0002, agb::script::var(0x0002) + 1)", "agb_start_script(agb_cutscene::ApiTour_Celebrate, actor->index, 3)",
            "agb::screenArena().make<::Trail>()",
        ];
        foreach (var e in expected) Assert.Contains(e, cpp);
        Assert.Equal("", TestSupport.CompileCpp(r));
    }

    [Fact]
    public void ErrorsSayWhatWhyAndHow()
    {
        const string head = "using GBA;\n\npublic class Hero : Behaviour\n{\n";
        var r = TestSupport.Compile("translator-err1", TestSupport.Context(), TestSupport.Inline("Hero.cs", head + "    void Update() { Sound.Play(\"jmp\"); }\n}\n"));
        var d = Assert.Single(r.Diagnostics, x => x.IsError);
        Assert.Equal("Sound \"jmp\" is not in the project. Sound names are the .wav files in assets/audio/sfx without the extension. Did you mean \"jump\"?", d.Message);
        Assert.Equal((5, 32), (d.Line, d.Column));

        var r2 = TestSupport.Compile("translator-err2", TestSupport.Context(), TestSupport.Inline("Hero.cs", head + "    void Start() { var h = new Hero(); }\n}\n"));
        Assert.Contains(r2.Diagnostics, x => x.Id == "AGB0120" && x.Message.StartsWith("Behaviours are created by the scene"));

        var r3 = TestSupport.Compile("translator-err3", TestSupport.Context(), TestSupport.Inline("Hero.cs", head + "    void Start() { string s = name(); Debug.Log(s + \"!\"); }\n    string name() => \"a\";\n}\n"));
        Assert.Contains(r3.Diagnostics, x => x.IsError && x.Message.StartsWith("Strings cannot be joined at runtime"));

        var r4 = TestSupport.Compile("translator-err4", TestSupport.Context(), TestSupport.Inline("Hero.cs", head + "    void Start() { var l = new System.Collections.Generic.List<int>(); }\n}\n"));
        Assert.Contains(r4.Diagnostics, x => x.IsError);
    }

    [Fact]
    public void SceneEventsLinkConditionsAndDatabaseScriptCells()
    {
        // Scene Settings events ("FadeIn(0.5s)"), link "Only if" and database cells of type script compile to C++ hooks.
        var ctx = TestSupport.Context();
        ctx.SceneEvents.Add(new SceneEventsDef
        {
            Scene = "Forest_01", File = Path.Combine(TestSupport.TestsDir, "scenes", "Forest_01.scene"),
            OnEnter = "FadeIn(0.5s)", OnExit = "Save.Checkpoint()", LinkConditions = { [0] = "Game.score >= 10" },
        });
        var items = ctx.Tables[0];
        items.Fields.Add(new AgbStudio.Core.Project.FieldDef { Name = "use", Type = "script" });
        items.Rows[0]["use"] = "Heal(power)";
        items.Rows[1]["use"] = "score += price";
        // P9.06: a script cell is callable from C# on its row (Items.Get(id).use()).
        var drinker = TestSupport.Inline("Drinker.cs", "using GBA;\n\npublic class Drinker : Behaviour\n{\n    void Start() { Items.Get(ItemId.Potion).use(); }\n}\n");
        var r = TestSupport.Compile("translator-events", ctx, TestSupport.Script("Forest.cs"), drinker);
        Assert.True(r.Success, TestSupport.Errors(r));
        Assert.Contains("agb_db::Run(agb_db::Items::Get(agb_db::ItemId::Potion).use);", File.ReadAllText(Path.Combine(r.IncludeDir, "Drinker.cpp")));
        // The table points at the compiled cells.
        var table = File.ReadAllText(Path.Combine(r.IncludeDir, "db_Items.cpp"));
        Assert.Contains("void agb_dbscript_Items_Potion_use();", table);
        Assert.Contains("&agb_dbscript_Items_Potion_use", table);
        Assert.Contains("&agb_dbscript_Items_Ether_use", table);
        var hpp = File.ReadAllText(Path.Combine(r.IncludeDir, "agb_scripts.hpp"));
        Assert.Contains("void agb_scene_Forest_01_onEnter();", hpp);
        Assert.Contains("bool agb_link_Forest_01_0();", hpp);
        Assert.Contains("void agb_dbscript_Items_Potion_use();", hpp);
        Assert.Contains("void agb_dbscript_Items_Ether_use();", hpp);
        var events = File.ReadAllText(Path.Combine(r.IncludeDir, "SceneEvents_Forest_01.cpp"));
        Assert.Contains("agb::script::natives::fadeIn(agb::fixed::lit(0.5));", events);
        Assert.Contains("return agb_state.score >= 10;", events);
        Assert.Contains("#line 1 \"scenes/Forest_01.scene\"", events);
        var cells = File.ReadAllText(Path.Combine(r.IncludeDir, "DbScripts_Items.cpp"));
        Assert.Contains("agb_state.score += price;", cells);
        Assert.Equal("", TestSupport.CompileCpp(r));
    }

    [Fact]
    public void DebugMapListsFilesAndMethods()
    {
        var r = s_figma.Value;
        var map = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(r.IncludeDir, "debug-map.json"))).RootElement;
        var files = map.GetProperty("files").EnumerateArray().Select(e => e.GetString()).ToList();
        var methods = map.GetProperty("methods").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Equal(0, files.IndexOf("PlayerController.cs"));
        var cpp = File.ReadAllText(Path.Combine(r.IncludeDir, "PlayerController.cpp"));
        Assert.Contains($"AGB_SCOPE(0, {methods.IndexOf("PlayerController.Update()")});", cpp);
        Assert.Contains("AGB_PROBE(0, 15); if (", cpp);
        Assert.Contains("agb::dbg::setNames(kDebugFiles, kDebugMethods);", File.ReadAllText(Path.Combine(r.IncludeDir, "agb_scripts.cpp")));
        _ = NativeBuild.RepoRoot;
    }
}
