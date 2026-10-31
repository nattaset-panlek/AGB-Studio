using System.Globalization;
using System.Text.RegularExpressions;
using AgbStudio.Build;
using AgbStudio.Build.Content;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;
using AgbStudio.Emu;

namespace AgbStudio.Tests;

/// <summary>
/// P7.12: report.json against the linker map file. P7.10: the cartridge header gbafix writes. P7.11: the build
/// configuration matrix. G-HW.05: the ROM size warning from Project Settings and the 32 MB limit.
/// </summary>
[Collection("emulator")]
public sealed partial class BuildReportTests(EmulatorFixture emu)
{
    static readonly string Root = Path.Combine(NativeBuild.RepoRoot, "build", "test-projects");

    [GeneratedRegex(@"^\s+0x([0-9a-f]+)\s+(?:PROVIDE \()?(__rom_end__|__iheap_start|__eheap_start)\s*=", RegexOptions.Multiline)]
    private static partial Regex LinkerSymbol();

    /// <summary>The linker map read independently of LinkerMap: end symbols and the input sections stored in ROM per
    /// object file (bytes, not counting padding).</summary>
    public static (long RomEnd, long IheapStart, long EheapStart, Dictionary<string, long> RomByObject) ReadMap(string path)
    {
        var text = File.ReadAllText(path);
        var symbols = LinkerSymbol().Matches(text).ToDictionary(m => m.Groups[2].Value, m => long.Parse(m.Groups[1].Value, NumberStyles.HexNumber));
        var byObject = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var lines = text.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        var start = lines.FindIndex(l => l.StartsWith("Linker script and memory map", StringComparison.Ordinal));
        for (var i = start; i < lines.Count; i++)
        {
            // " .rodata.x  0xADDR  0xSIZE path" or the name alone with the numbers on the next line.
            var m = Regex.Match(lines[i], @"^ \.\S+\s+0x([0-9a-f]+)\s+0x([0-9a-f]+)\s+(\S.*)$");
            if (!m.Success && Regex.IsMatch(lines[i], @"^ \.\S+$") && i + 1 < lines.Count)
                m = Regex.Match(lines[++i], @"^\s+0x([0-9a-f]+)\s+0x([0-9a-f]+)\s+(\S.*)$");
            if (!m.Success) continue;
            var addr = long.Parse(m.Groups[1].Value, NumberStyles.HexNumber);
            if (addr < 0x08000000 || addr >= 0x0A000000) continue;  // RAM sections (their ROM copies are code / data)
            var obj = Path.GetFileName(m.Groups[3].Value.Trim());
            byObject[obj] = byObject.GetValueOrDefault(obj) + long.Parse(m.Groups[2].Value, NumberStyles.HexNumber);
        }
        return (symbols["__rom_end__"], symbols["__iheap_start"], symbols["__eheap_start"], byObject);
    }

    static long ObjectBytes(Dictionary<string, long> byObject, string suffix) =>
        byObject.Where(kv => kv.Key.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)).Sum(kv => kv.Value);

    [Fact]
    public void ReportNumbersMatchTheLinkerMap()
    {
        var p = SampleProjects.CreatePlatformerMini(Path.Combine(Root, "report_map"));
        SampleText.AddTo(p);
        var table = new TableFile
        {
            Name = "Enemies",
            Fields = { new FieldDef { Name = "id", Type = "enum" }, new FieldDef { Name = "hp", Type = "int" }, new FieldDef { Name = "speed", Type = "fixed" },
                new FieldDef { Name = "rarity", Type = "enum", EnumValues = { "Common", "Rare", "Epic" } }, new FieldDef { Name = "boss", Type = "bool" } },
        };
        for (var i = 0; i < 12; i++) table.Rows.Add(new() { ["id"] = $"E{i}", ["hp"] = (i * 3).ToString(), ["speed"] = "0.5", ["rarity"] = "Rare", ["boss"] = "false" });
        GameProject.Save(Path.Combine(p.DataDir, "Enemies.db"), table);
        // A script reads the table, so the linker keeps it (unused tables are dropped by --gc-sections).
        File.WriteAllText(Path.Combine(p.ScriptsDir, "Reader.cs"),
            "using GBA;\n\npublic class Reader : Behaviour\n{\n    public int hp;\n\n    void Start()\n    {\n        hp = Enemies.Get(EnemyId.E3).hp;\n    }\n}\n");
        var scenePath = Path.Combine(p.ScenesDir, "Forest_01.scene");
        var sceneFile = GameProject.Load<SceneFile>(scenePath);
        sceneFile.Actors[0].Components.Add(new ComponentData { Type = "Reader" });
        GameProject.Save(scenePath, sceneFile);
        var b = new GameBuilder(p, new Toolchain()) { Scripts = new AgbStudio.Scripting.ScriptBackend() };
        var r = b.Build();
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        var report = BuildReport.Load(p.BuildDir)!;
        var map = ReadMap(r.ElfPath + ".map");

        // ROM: the file is the image the map lays out, and the breakdown accounts for every byte of it.
        Assert.Equal(map.RomEnd - 0x08000000, report.RomBytes);
        Assert.Equal(report.RomBytes, report.Breakdown.Values.Sum());
        // RAM: static IWRAM and EWRAM end where the linker starts the heaps.
        Assert.Equal(map.IheapStart - 0x03000000, report.IwramBytes);
        Assert.Equal(map.EheapStart - 0x02000000, report.EwramBytes);
        Assert.True(report.BuildSeconds > 0, "build time is recorded");

        // Assets: each one's bytes are what its object file puts in ROM (plus alignment padding, under 4 bytes a section).
        long Asset(string name) => report.Assets.FirstOrDefault(a => a.Name == name)?.Bytes ?? -1;
        Assert.Equal(report.Assets.Take(10), report.LargestAssets);
        var all = new Dictionary<string, (string Suffix, long Report)>
        {
            ["hero"] = ("sprite_hero.o", Asset("hero")),
            ["forest_tiles"] = ("tileset_forest_tiles.o", Asset("forest_tiles")),
            ["thai_vwf"] = ("text_font_thai_vwf.o", Asset("thai_vwf")),
            ["Enemies"] = ("data_db_Enemies.o", Asset("Enemies")),
        };
        foreach (var (name, (suffix, bytes)) in all)
        {
            var inMap = ObjectBytes(map.RomByObject, suffix);
            Assert.True(inMap > 0, $"{suffix} is not in the map");
            Assert.True(bytes >= inMap && bytes < inMap + 4 * 8, $"{name}: report {bytes} B, map {inMap} B");
        }
        Assert.True(report.Breakdown["Graphics"] >= ObjectBytes(map.RomByObject, "sprite_hero.o") + ObjectBytes(map.RomByObject, "tileset_forest_tiles.o"));
        Assert.True(report.Breakdown["Audio"] >= ObjectBytes(map.RomByObject, "audio_soundbank.o"));

        // P8.11: the Font Manager / New Font estimate ("about N KB of ROM") is within 10% of the built font.
        var font = b.Assets.Fonts["thai_vwf"].RomBytes;
        Assert.InRange(font, all["thai_vwf"].Report * 0.9, all["thai_vwf"].Report * 1.1);
        // P9.07: the table's size from DatabaseCodegen.TableBytes is its bytes in the ROM. The Database status bar adds
        // the Add Field bytes per row (4 + 4 + 1 + 1 = 10), which leaves out the 2 bytes that pad each row to 12.
        var rom = AgbStudio.Build.Data.DatabaseCodegen.TableBytes(table);
        Assert.Equal(12 * 12, rom);
        Assert.Equal(rom, b.Assets.Data["Enemies"].RomBytes);
        Assert.InRange(all["Enemies"].Report, rom, rom + 3);
        var status = table.Rows.Count * table.Fields.Where(f => f.Name != table.KeyField).Sum(AgbStudio.Build.Data.DatabaseCodegen.FieldBytes);
        Assert.Equal(12 * 2, rom - status);
        // P8.07: text VRAM of the scene: the blank tile, the HUD label (224 px x 1 line = 28 x 2 tiles) and the 28 x 5
        // tile dialogue box the Font Manager meter shows as "4 / 16 KB".
        var scene = report.Scenes["Forest_01"];
        var lineHeight = AgbStudio.Build.Fonts.FontBuilder.Build(GameProject.Load<FontFile>(Path.Combine(p.FontsDir, "thai_vwf.font")), p.FontsDir).LineHeight;
        Assert.Equal(32 + 28 * ((lineHeight + 7) / 8) * 32 + GameBuilder.DialogueBoxBytes, scene.VramTextBytes);
        Assert.Equal(4, GameBuilder.DialogueBoxBytes / 1024);
        Assert.True(scene.VramBgBytes > scene.VramTextBytes);
        Assert.Equal(4, scene.ObjPalettes);  // hero, slime, coin, flag
    }

    [Fact]
    public void RomHeaderHoldsTitleGameCodeMakerAndVersion()
    {
        var p = SampleProjects.CreatePlatformerMini(Path.Combine(Root, "report_header"));
        p.Settings.General.Maker = "GE";
        p.Settings.General.Version = "1.2.7";
        p.SaveSettings();
        var r = new GameBuilder(p, new Toolchain()).Build();
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        var rom = File.ReadAllBytes(r.RomPath);
        Assert.Equal("MINIPLAT\0\0\0\0", System.Text.Encoding.ASCII.GetString(rom, 0xA0, 12));  // title, 12 bytes
        Assert.Equal("AMPE", System.Text.Encoding.ASCII.GetString(rom, 0xAC, 4));             // game code
        Assert.Equal("GE", System.Text.Encoding.ASCII.GetString(rom, 0xB0, 2));               // maker code
        Assert.Equal(0x96, rom[0xB2]);                                                         // fixed value
        Assert.Equal(7, rom[0xBC]);                                                            // version (last part of 1.2.7)
        var sum = 0;
        for (var i = 0xA0; i <= 0xBC; i++) sum += rom[i];
        Assert.Equal((byte)(-(sum + 0x19)), rom[0xBD]);                                       // header checksum
        Assert.Contains(rom[0x04..0xA0], x => x != 0);                                         // logo area filled
        // The ROM boots with that header.
        var core = emu.Core;
        core.LoadRom(r.RomPath);
        DebugChannel? ch = null;
        for (var f = 0; f < 60; f++)
        {
            core.RunFrame();
            ch ??= DebugChannel.Find(core);
        }
        Assert.NotNull(ch);
        Assert.True(ch!.GameFrame > 10);
        core.UnloadRom();
    }

    [Fact]
    public void ConfigurationMatrixSetsCompilerFlagsDefinesAndCompression()
    {
        var p = SampleProjects.CreatePlatformerMini(Path.Combine(Root, "report_matrix"));
        var tc = new Toolchain();
        var tileset = Path.Combine(p.BuildDir, "gen", "assets", "tileset_forest_tiles.cpp");
        var count = 0;
        foreach (var config in new[] { BuildConfiguration.Debug, BuildConfiguration.Release })
            foreach (var compress in new[] { true, false })
                foreach (var keepAsserts in new[] { true, false })
                    foreach (var strip in new[] { true, false })
                    {
                        var b = p.Settings.Build;
                        b.CompressGraphics = compress;
                        b.KeepAsserts = keepAsserts;
                        b.StripDebugLog = strip;
                        b.Optimise = config == BuildConfiguration.Release ? "-Os" : "-O2";
                        p.SaveSettings();
                        var builder = new GameBuilder(p, tc);
                        var r = builder.Build(new BuildOptions { Configuration = config, GenerateOnly = true });
                        var what = $"{config} compress={compress} asserts={keepAsserts} strip={strip}";
                        Assert.True(r.Success, what + ": " + string.Join('\n', r.Diagnostics));
                        var t = builder.Target(config);
                        Assert.Equal(config == BuildConfiguration.Release ? BuildConfig.Release : BuildConfig.Debug, t.Config);
                        Assert.Equal(b.Optimise, t.Optimise);
                        Assert.Equal(keepAsserts, t.Defines.Contains("AGB_KEEP_ASSERTS=1"));
                        Assert.Equal(config == BuildConfiguration.Release && !strip, t.Defines.Contains("AGB_KEEP_LOGS=1"));
                        var flags = NativeBuild.CommonFlags(tc, t.Config);
                        Assert.Contains(config == BuildConfiguration.Debug ? "-DAGB_DEBUG=1 -g" : "-DAGB_RELEASE=1 -DNDEBUG", flags);
                        Assert.Contains("-mcpu=arm7tdmi", flags);
                        Assert.Contains(compress ? ", 4, true}," : ", 4, false},", File.ReadAllText(tileset));
                        count++;
                    }
        Assert.Equal(16, count);
    }

    static (uint[] Frame, byte[] Rom, List<string> Log) BuildAndRun(EmulatorFixture emu, string name, BuildConfiguration config, bool compress, bool strip)
    {
        var p = SampleProjects.CreatePlatformerMini(Path.Combine(Root, name));
        p.Settings.General.BootSplash = false;
        p.Settings.Build.CompressGraphics = compress;
        p.Settings.Build.StripDebugLog = strip;
        p.SaveSettings();
        var r = new GameBuilder(p, new Toolchain()).Build(new BuildOptions { Configuration = config });
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        var s = new PlaySession(emu.Core);
        s.Start(r.RomPath);
        for (var i = 0; i < 90; i++) s.Step();
        var frame = (uint[])s.Core.Frame.Clone();
        var log = s.Log.ToList();
        s.Stop();
        return (frame, File.ReadAllBytes(r.RomPath), log);
    }

    [Fact]
    public void DebugAndReleaseRomsShowTheSameGameWithAndWithoutCompression()
    {
        var debug = BuildAndRun(emu, "report_rom_debug", BuildConfiguration.Debug, compress: true, strip: true);
        var releaseLogs = BuildAndRun(emu, "report_rom_release_logs", BuildConfiguration.Release, compress: false, strip: false);
        var release = BuildAndRun(emu, "report_rom_release", BuildConfiguration.Release, compress: true, strip: true);
        // LZ77 tiles (BIOS decompression) and plain tiles draw the same screen.
        Assert.Equal(debug.Frame, releaseLogs.Frame);
        Assert.Equal(debug.Frame, release.Frame);
        // Strip Debug.Log: log text is in Debug ROMs and in Release ROMs that keep logs, not in stripped ones.
        var marker = "OBJ VRAM is full"u8.ToArray();
        Assert.True(debug.Rom.AsSpan().IndexOf(marker) >= 0);
        Assert.True(releaseLogs.Rom.AsSpan().IndexOf(marker) >= 0);
        Assert.Equal(-1, release.Rom.AsSpan().IndexOf(marker));
        Assert.True(release.Rom.Length < releaseLogs.Rom.Length);
    }

    [Fact]
    public void RomSizeWarningComesFromProjectSettingsAndOver32MbFails()
    {
        var p = SampleProjects.CreatePlatformerMini(Path.Combine(Root, "report_rom_size"));
        // About 1.2 MB of sampled sound pushes the ROM over 1 MB.
        var samples = new byte[1_250_000];
        new Random(3).NextBytes(samples);
        File.WriteAllBytes(Path.Combine(p.AssetsDir, "audio", "sfx", "rain.wav"), AgbStudio.Build.Audio.WavWriter.Encode(samples.Select(x => (sbyte)x).ToArray(), 11025));
        p.Settings.Build.WarnRomMegabytes = 1;
        p.SaveSettings();
        var r = new GameBuilder(p, new Toolchain()).Build();
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        var mb = r.Report!.RomBytes / 1048576.0;
        Assert.InRange(mb, 1.0, 2.0);
        var warning = string.Create(CultureInfo.InvariantCulture, $"ROM is {mb:0.0} MB, over the 1 MB warning set in Project Settings · Build.");
        Assert.Contains(warning, r.Report.Warnings);
        Assert.Contains(r.Diagnostics, d => d.Severity == Severity.Warning && d.Message == warning);
        Assert.Single(r.Diagnostics, d => d.Message == warning);
        // The default 16 MB: no warning.
        p.Settings.Build.WarnRomMegabytes = 16;
        p.SaveSettings();
        r = new GameBuilder(p, new Toolchain()).Build();
        Assert.True(r.Success);
        Assert.DoesNotContain(r.Report!.Warnings, w => w.StartsWith("ROM is", StringComparison.Ordinal));
        // Over 32 MB: the linker refuses the ROM region overflow, and anything larger fails the build.
        Assert.Null(BuildReport.RomSizeError(32L * 1024 * 1024));
        Assert.StartsWith("The ROM is 33.0 MB; a GBA cartridge holds at most 32 MB.", BuildReport.RomSizeError(33L * 1024 * 1024));
        var ld = GameBuilder.ParseLinkerLine(@"c:/devkitpro/devkitarm/bin/../lib/gcc/arm-none-eabi/14.1.0/../../../../arm-none-eabi/bin/ld.exe: region `rom' overflowed by 1500000 bytes");
        Assert.NotNull(ld);
        Assert.Equal(Severity.Error, ld!.Severity);
        Assert.StartsWith("The game is 1465 KB larger than the 32 MB a GBA cartridge holds.", ld.Message);
        Assert.Contains("IWRAM (32 KB) is 12 bytes too small", GameBuilder.ParseLinkerLine("ld.exe: region `iwram' overflowed by 12 bytes")!.Message);
        Assert.Null(GameBuilder.ParseLinkerLine("ld.exe: warning: something else"));
    }
}
