using AgbStudio.Build;
using AgbStudio.Build.Audio;
using AgbStudio.Build.Content;
using AgbStudio.Build.Fonts;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;
using AgbStudio.Emu;

namespace AgbStudio.Tests;

/// <summary>
/// P9.14: Text.Show picks the current language at runtime: the language is switched in the Options screen from the pause
/// menu and the next line in the same scene is in the new language. G-PERF.05: the maxmod mixer's share of the frame
/// with 8 channels at 21 kHz, measured by the runtime profiler (Figma Audio window: 6%).
/// </summary>
[Collection("emulator")]
public sealed class RuntimeTextAudioTests(EmulatorFixture emu)
{
    static readonly string Root = Path.Combine(NativeBuild.RepoRoot, "build", "test-projects");

    static void Press(PlaySession s, GbaKeys key)
    {
        s.Keys = key;
        s.Step();
        s.Keys = 0;
        for (var i = 0; i < 3; i++) s.Step();
    }

    // The offsets in the dialogue box area (or from line `top` to `bottom`) where the expected text's ink pixels are
    // exactly one colour on screen and that colour is nowhere else under the text.
    static List<(int X, int Y)> Find(uint[] frame, byte[] expected, int w, int h, int top = 104, int bottom = 160)
    {
        var hits = new List<(int, int)>();
        for (var oy = top; oy + h <= bottom; oy++)
            for (var ox = 0; ox + w <= 240 && ox < 48; ox++)
            {
                uint? ink = null;
                var ok = true;
                for (var y = 0; y < h && ok; y++)
                    for (var x = 0; x < w && ok; x++)
                    {
                        var screen = frame[(oy + y) * 240 + ox + x];
                        if (expected[y * w + x] == 1)
                        {
                            ink ??= screen;
                            ok = screen == ink;
                        }
                        else if (ink != null && screen == ink) ok = false;
                    }
                if (ok && ink != null) hits.Add((ox, oy));
            }
        return hits;
    }

    [Fact]
    public void TextShowUsesTheLanguageChosenInOptionsInTheSameScene()
    {
        var p = SampleProjects.CreatePlatformerMini(Path.Combine(Root, "language_switch"));
        SampleText.AddTo(p);  // Thai first, English second; the HUD label shows the same key
        File.WriteAllText(Path.Combine(p.ScriptsDir, "Talker.cs"),
            "using GBA;\n\npublic class Talker : Behaviour\n{\n    void Update()\n    {\n        if (Input.Pressed(Key.L)) Text.Show(\"dlg.forest.hopper_01\");\n    }\n}\n");
        var scenePath = Path.Combine(p.ScenesDir, "Forest_01.scene");
        var scene = GameProject.Load<SceneFile>(scenePath);
        scene.Actors[0].Components.Add(new ComponentData { Type = "Talker" });
        GameProject.Save(scenePath, scene);
        var r = new GameBuilder(p, new Toolchain()) { Scripts = new AgbStudio.Scripting.ScriptBackend() }.Build();
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));

        var en = GameProject.Load<StringTable>(Path.Combine(p.TextDir, "strings.en.json")).Strings["dlg.forest.hopper_01"];
        var th = GameProject.Load<StringTable>(Path.Combine(p.TextDir, "strings.th.json")).Strings["dlg.forest.hopper_01"];
        var font = FontBuilder.Build(GameProject.Load<FontFile>(Path.Combine(p.FontsDir, "thai_vwf.font")), p.FontsDir, ThaiText.CodePoints(en + th + "SCOREคะแนนStartเริ่มเกมไทยEnglish"));
        (byte[] Pixels, int W, int H) Expected(string text)
        {
            var layout = TextLayout.Layout(text, font, 224);
            int w = layout.MaxWidth + 2, h = font.LineHeight * layout.Lines;
            return (TextRenderer.Render(layout, font, w, h, new TextRenderer.Style(1, 4, 0)), w, h);
        }
        var thai = Expected(th);
        var english = Expected(en);
        var shots = Path.Combine(p.BuildDir, "language-shots");
        Directory.CreateDirectory(shots);

        var s = new PlaySession(emu.Core);
        s.Start(r.RomPath);
        for (var i = 0; i < 60; i++) s.Step();
        Press(s, GbaKeys.L);
        for (var i = 0; i < 60; i++) s.Step();
        var before = (uint[])s.Core.Frame.Clone();
        AgbStudio.Core.Png.WriteArgb(Path.Combine(shots, "1-thai.png"), before, 240, 160);

        // Pause menu > Options > Language: next language; back; resume the same scene.
        void Expect(string line) => Assert.True(s.Log.Any(l => l.Contains(line, StringComparison.Ordinal)), $"no '{line}' in the log:\n" + string.Join("\n", s.Log.TakeLast(20)));
        Press(s, GbaKeys.A);  // close the dialogue box: the pause menu does not open over a line of text
        for (var i = 0; i < 10; i++) s.Step();
        Press(s, GbaKeys.Start);
        for (var i = 0; i < 6; i++) s.Step();
        Expect("Pause menu opened");
        Press(s, GbaKeys.Down);
        Press(s, GbaKeys.A);
        for (var i = 0; i < 6; i++) s.Step();
        AgbStudio.Core.Png.WriteArgb(Path.Combine(shots, "options.png"), s.Core.Frame, 240, 160);
        Expect("Options opened");
        Press(s, GbaKeys.Down);
        Press(s, GbaKeys.Down);
        Press(s, GbaKeys.Right);
        Press(s, GbaKeys.B);
        Expect("Options closed");
        for (var i = 0; i < 6; i++) s.Step();
        Press(s, GbaKeys.Start);
        Expect("Pause menu closed");
        for (var i = 0; i < 10; i++) s.Step();
        Press(s, GbaKeys.L);
        for (var i = 0; i < 60; i++) s.Step();
        var after = (uint[])s.Core.Frame.Clone();
        AgbStudio.Core.Png.WriteArgb(Path.Combine(shots, "2-english.png"), after, 240, 160);
        var player = s.Actors().First(a => a.Name == "Player");
        s.Stop();

        Assert.True(player.Alive, "still the same scene and player");
        Assert.Single(Find(before, thai.Pixels, thai.W, thai.H));
        Assert.Empty(Find(before, english.Pixels, english.W, english.H));
        Assert.Single(Find(after, english.Pixels, english.W, english.H));
        Assert.Empty(Find(after, thai.Pixels, thai.W, thai.H));
        // The HUD Text Label (same key, at 16, 16) is back after the pause menu and Options, in the new language.
        Assert.Equal(new List<(int, int)> { (16, 16) }, Find(before, thai.Pixels, thai.W, thai.H, 0, 48));
        Assert.Equal(new List<(int, int)> { (16, 16) }, Find(after, english.Pixels, english.W, english.H, 0, 48));
        Assert.Empty(Find(after, thai.Pixels, thai.W, thai.H, 0, 48));
    }

    /// <summary>An XM whose 8 channels each hold a looping note from the first row on.</summary>
    // cycles: how many times the 32-sample waveform repeats in the instrument's loop (1 = a chiptune cycle).
    static byte[] EightChannelLoop(int channels, int cycles)
    {
        var xm = new XmWriter { Title = "mix", Bpm = 125, Channels = 8 };
        string[] waves = ["pulse25", "triangle", "sine", "square", "saw", "pulse25", "triangle", "square"];
        for (var i = 0; i < 8; i++)
        {
            var one = ModWriter.Waveform(waves[i]);
            xm.Instruments.Add(new($"w{i}", Enumerable.Repeat(one, cycles).SelectMany(x => x).ToArray(), 32, true, 24));
        }
        var pattern = xm.NewPattern();
        for (var c = 0; c < channels; c++) pattern[0, c] = new(37 + c * 5, c + 1);
        xm.Patterns.Add(pattern);
        xm.Order.Add(0);
        return xm.Encode();
    }

    static double MixerShare(EmulatorFixture emu, int channels, int cycles, int khz = 21)
    {
        var p = SampleProjects.CreatePlatformerMini(Path.Combine(Root, $"mixer_{channels}ch_{cycles}_{khz}k"));
        File.Delete(Path.Combine(p.AssetsDir, "audio", "music", "tune.mod"));
        File.WriteAllBytes(Path.Combine(p.AssetsDir, "audio", "music", "mix.xm"), EightChannelLoop(channels, cycles));
        var scenePath = Path.Combine(p.ScenesDir, "Forest_01.scene");
        var scene = GameProject.Load<SceneFile>(scenePath);
        scene.Music = new MusicSettings { Track = "mix" };
        GameProject.Save(scenePath, scene);
        Assert.Equal((8, 21), (p.Settings.Audio.Channels, p.Settings.Audio.MixKHz));  // Project Settings · Audio defaults
        p.Settings.Audio.MixKHz = khz;
        p.SaveSettings();
        var r = new GameBuilder(p, new Toolchain()).Build(new BuildOptions { Configuration = BuildConfiguration.Release });
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        var s = new PlaySession(emu.Core);
        s.Start(r.RomPath);
        for (var i = 0; i < 90; i++) s.Step();
        var samples = new List<uint>();
        for (var i = 0; i < 240; i++)
        {
            s.Step();
            samples.Add(s.Profiler()!.Audio);
        }
        s.Stop();
        return samples.Average(x => (double)x) / 280896.0 * 100;
    }

    [Fact]
    public void MixerShareFor8ChannelsAt21kHzIsMeasured()
    {
        var eight = MixerShare(emu, 8, 64);
        var one = MixerShare(emu, 1, 64);
        var chip = MixerShare(emu, 8, 1);
        // Project Settings · Audio mixing rate: the runtime mixes at the project's rate (fewer samples per frame).
        var rates = new[] { 16, 8 }.ToDictionary(k => k, k => MixerShare(emu, 8, 64, k));
        File.AppendAllText(Path.Combine(NativeBuild.RepoRoot, "build", "perf-targets.txt"),
            $"{DateTime.Now:yyyy-MM-dd HH:mm} mixer 8 ch 21 kHz {eight:0.00}% (1 ch {one:0.00}%, 8 ch of 32-sample loops {chip:0.00}%; 8 ch at " +
            string.Join(", ", rates.Select(r => $"{r.Key} kHz {r.Value:0.00}%")) + ")\n");
        Assert.True(eight > one, $"8 channels {eight:0.00}% should cost more than 1 channel {one:0.00}%");
        Assert.True(rates[16] < eight && rates[8] < rates[16], $"8 channels: 21 kHz {eight:0.00}%, 16 kHz {rates[16]:0.00}%, 8 kHz {rates[8]:0.00}%");
        // Figma shows 6% for 8 channels at 21 kHz. Measured 2026-10-31 (docs/DECISIONS.md D-26): 14.49% at 21 kHz, 11.66% at
        // 16 kHz, 7.55% at 8 kHz; about 3.2% of it does not depend on the rate (the module player), so 6% is out of reach
        // with 8 channels at any Maxmod rate. These bounds guard the measured cost; they do not claim the Figma value.
        Assert.True(eight <= 15.0, $"the mixer takes {eight:0.00}% of the frame with 8 channels at 21 kHz (measured 14.49%, Figma 6%)");
        Assert.True(rates[16] <= 12.0, $"the mixer takes {rates[16]:0.00}% with 8 channels at 16 kHz (measured 11.66%)");
        Assert.True(rates[8] <= 8.0, $"the mixer takes {rates[8]:0.00}% with 8 channels at 8 kHz (measured 7.55%)");
    }
}
