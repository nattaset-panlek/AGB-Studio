using AgbStudio.Build.Audio;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Support;
using AgbStudio.Editor.Tests.Windows.Build;
using AgbStudio.Editor.Windows.Audio;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.Audio;

/// <summary>Module reading and rendering (P11.43 back end): MOD / XM parsing, song length, pattern write-back.</summary>
public class TrackerModuleTests
{
    [Fact]
    public void Mod_ParsesOrderPatternsAndLengthFromTempo()
    {
        var m = TrackerModule.Parse(Chiptune.SimpleTune("t", 96, 3));
        Assert.Equal("MOD", m.Format);
        Assert.Equal(4, m.Channels);
        Assert.Equal([0, 1], m.Order);
        Assert.Equal(2, m.Patterns.Count);
        Assert.Equal("lead", m.ChannelName(0));
        // Fxx = 96 BPM at speed 6: 128 rows x 6 ticks / (96 x 2 / 5) Hz = 20 s
        var a = ModuleAnalysis.Of(m);
        Assert.InRange(a.Seconds, 19.9, 20.1);
        Assert.Equal(96, new ModulePlayer(m).Let(p => { p.Render(new short[2048]); return p.Bpm; }));
        Assert.Equal("0:20", ModuleAnalysis.FormatTime(a.Seconds));
    }

    [Fact]
    public void Xm_ParsesHeaderInstrumentsAndCells()
    {
        var bytes = XmWriter.Tune("forest", 128, 3, 11, "C");
        var m = TrackerModule.Parse(bytes);
        Assert.True(m.IsXm);
        Assert.Equal(6, m.Channels);
        Assert.Equal(128, m.InitialBpm);
        Assert.Equal(6, m.InitialSpeed);
        Assert.Equal(["Lead", "Bass", "Drums", "Pad", "Arp", "FX"], Enumerable.Range(0, 6).Select(m.ChannelName));
        var lead = m.Patterns[0].Cells[0, 0];
        Assert.Equal(1, lead.Instrument);
        Assert.Equal("C-4", m.NoteName(49));
        Assert.Equal("v40", TrackerGrid.CellText(m, lead)[^3..]);
        Assert.Equal("... .. ...", TrackerGrid.CellText(m, default));
        // 3 patterns x 64 rows at 128 BPM, speed 6 = 192 x 6 / 51.2 = 22.5 s
        Assert.InRange(ModuleAnalysis.Of(m).Seconds, 22.4, 22.6);
    }

    [Fact]
    public void Encode_WritesPatternEditsAndKeepsTheRest()
    {
        var mod = Chiptune.SimpleTune("t", 120, 5);
        var m = TrackerModule.Parse(mod);
        Assert.Equal(mod, m.Encode());
        m.Patterns[0].Cells[1, 2] = new TrackerCell(49, 1, 0, 0xC, 32);
        var again = TrackerModule.Parse(m.Encode());
        Assert.Equal(m.Patterns[0].Cells[1, 2], again.Patterns[0].Cells[1, 2]);
        Assert.Equal(mod.Length, m.Encode().Length);

        var xm = TrackerModule.Parse(XmWriter.Tune("x", 125, 2, 3));
        xm.Patterns[1].Cells[5, 4] = default;
        xm.Patterns[1].Cells[6, 5] = new TrackerCell(61, 6, 0x40, 0, 0);
        var x2 = TrackerModule.Parse(xm.Encode());
        Assert.Equal(xm.Patterns[1].Cells[6, 5], x2.Patterns[1].Cells[6, 5]);
        Assert.True(x2.Patterns[1].Cells[5, 4].IsEmpty);
        Assert.Equal(xm.Instruments.Select(i => i.Samples[0].Data.Length), x2.Instruments.Select(i => i.Samples[0].Data.Length));
    }

    [Fact]
    public void Player_RendersAudibleStereoAndLoops()
    {
        var m = TrackerModule.Parse(Chiptune.SimpleTune("t", 150, 2));
        var p = new ModulePlayer(m, 22050) { LoopOrder = 0 };
        var buf = new short[22050 * 2 * 15];
        Assert.Equal(22050 * 15, p.Render(buf));
        Assert.False(p.Ended);
        Assert.True(buf.Select(v => Math.Abs((int)v)).Max() > 1000);
        var once = new ModulePlayer(m, 22050);
        Assert.True(once.Render(buf) < 22050 * 15);
        Assert.True(once.Ended);
    }
}

static class Ext
{
    public static TR Let<T, TR>(this T v, Func<T, TR> f) => f(v);
}

/// <summary>Audio page E2E (P11.43, P11.44): real music and sound files of Sky Hopper, track info, tracker, preview PCM.</summary>
public class AudioPageTests : IDisposable
{
    readonly Fixture _fx = new();
    readonly GameProject _p;
    readonly CaptureAudioOutput _out = new();
    readonly MainWindow _win;

    public AudioPageTests()
    {
        _p = GroupFixture.SkyHopper(_fx);
        _fx.Services.Set(new AudioPreview(_out));
        _win = new MainWindow(_fx.Services) { Width = 1440, Height = 900 };
        _win.Show();
        Capture.Pump();
    }

    public void Dispose()
    {
        _win.Close();
        _fx.Dispose();
    }

    T Find<T>(string name) where T : Control => _win.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    void Click(Control c)
    {
        var p = c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), _win)!.Value;
        _win.MouseMove(p);
        _win.MouseDown(p, MouseButton.Left);
        _win.MouseUp(p, MouseButton.Left);
        Capture.Pump();
    }

    AudioPage OpenAudio()
    {
        _fx.Services.Commands.Execute("tools.audio");
        Capture.Pump();
        return Assert.IsType<AudioPage>(_win.CurrentPage);
    }

    [AvaloniaFact]
    public void ToolsAudio_ListsTheProjectsMusicAndSoundsWithLengths()
    {
        var page = OpenAudio();
        Assert.Equal(["bgm_cave.mod", "bgm_forest.xm", "bgm_title.xm", "jingle_win.mod"], page.MusicFiles.Select(Path.GetFileName));
        Assert.Equal(["blip.wav", "coin.wav", "explode.wav", "hurt.wav", "jump.wav"], page.SoundFiles.Select(Path.GetFileName));
        Assert.Equal("0.21 s", Find<TreeItem>("Sound_jump.wav").Meta);
        Assert.Equal("0.64 s", Find<TreeItem>("Sound_explode.wav").Meta);
        Assert.Equal("0:20", Find<TreeItem>("Music_bgm_cave.mod").Meta);
        // the last scene (Forest_01) plays bgm_forest, so it is selected and is the active document
        Assert.Equal("bgm_forest.xm", Path.GetFileName(page.Track));
        Assert.True(Find<TreeItem>("Music_bgm_forest.xm").IsSelected);
        Assert.Equal("bgm_forest.xm", _win.TitleBar.Document);
        Assert.Equal("Maxmod · 8 ch · 21 kHz", Find<Chip>("EngineChip").Content);
        // Create › Music and Create › Sound Effect open the same page
        _fx.Services.Commands.Execute("assets.create.music");
        _fx.Services.Commands.Execute("assets.create.soundEffect");
        Assert.Equal(["T-P15", "T-P15", "T-P15"], _fx.Services.Windows.Requests.Where(r => r.Window == "T-P15").Select(r => r.Window));
    }

    [AvaloniaFact]
    public void TrackInfo_ComesFromTheModuleAndMeta()
    {
        var page = OpenAudio();
        var a = page.Analysis!;
        Assert.Equal(128, a.Bpm);
        Assert.Equal(6, a.Speed);
        Assert.Equal(21, a.PatternCount);
        Assert.InRange(a.Seconds, 157, 158);  // 21 x 64 rows x 6 ticks at 51.2 Hz
        string Stat(string n) => Find<StackPanel>("Stat_" + n).Children.OfType<TextBlock>().Last().Text!;
        Assert.Equal("128", Stat("BPM"));
        Assert.Equal("21", Stat("Patterns"));
        Assert.Equal("2:38", Stat("Length"));
        Assert.Equal("Loop · pattern 4–20", Find<Chip>("LoopChip").Content);
        Assert.Equal("XM → Maxmod bank", Find<InspectorField>("FormatField").Value);
        Assert.Equal("0.8", Find<InspectorField>("VolumeField").Value);
        Assert.Equal("pattern 4", Find<InspectorField>("LoopStartField").Value);
        Assert.True(Find<AgbSwitch>("FadeSwitch").IsChecked);
        // selecting another track reads its module
        Click(Find<TreeItem>("Music_bgm_cave.mod"));
        Assert.Equal("MOD", page.Module!.Format);
        Assert.Equal(96, page.Analysis!.Bpm);
        Assert.Equal("bgm_cave.mod", _win.TitleBar.Document);
    }

    [AvaloniaFact]
    public void Tracker_ShowsThePatternAndFollowsSeek()
    {
        var page = OpenAudio();
        page.Seek(7, 20);
        Capture.Pump();
        var grid = Find<TrackerGrid>("Tracker");
        Assert.Equal(page.Module!.Order[7], grid.PatternIndex);
        Assert.Equal(20, grid.CurrentRow);
        Assert.Equal(16, grid.FirstRow);
        var overview = Find<WaveformOverview>("Overview");
        Assert.InRange(overview.Playhead, 0.3, 0.4);
        // the loop chip jumps to the loop start
        Click(Find<Chip>("LoopChip"));
        Assert.Equal((4, 0), (page.Order, page.Row));
    }

    [AvaloniaFact]
    public void Play_RendersTheModuleToPcmAndFollowsThePlayhead()
    {
        var page = OpenAudio();
        Click(Find<PlayControl>("TrackPlay"));
        Assert.True(page.IsPlaying);
        Assert.True(_out.IsRunning);
        var frames = _out.Pull(20);
        Assert.Equal(20 * 2048, frames);
        // the preview is the module player's output (same module, volume 0.8 from the meta)
        var reference = new ModulePlayer(page.Module!, AudioPreview.Rate) { Volume = 0.8, LoopOrder = 4 };
        var expected = new short[frames * 2];
        reference.Render(expected);
        Assert.Equal(expected, _out.Pcm.ToArray());
        Assert.True(Math.Sqrt(_out.Pcm.Average(v => (double)v * v)) > 300);
        page.Tick();
        Assert.True(page.Row > 0 || page.Order > 0);
        Click(Find<PlayControl>("TrackStop"));
        Assert.False(page.IsPlaying);
        Assert.Equal((0, 0), (page.Order, page.Row));
    }

    [AvaloniaFact]
    public void Inspector_EditsTheMetaUndoAndSave()
    {
        var page = OpenAudio();
        var volume = Find<InspectorField>("VolumeField");
        volume.SliderValue = 50;
        Assert.Equal(0.5, page.Meta.Volume);
        Assert.Equal("0.5", volume.Value);
        Find<InspectorField>("LoopStartField").Value = "pattern 2";
        Assert.Equal(2, page.Meta.LoopStart);
        Assert.Equal("Loop · pattern 2–20", Find<Chip>("LoopChip").Content);
        Find<AgbSwitch>("FadeSwitch").IsChecked = false;
        Assert.False(page.Meta.FadeOnSceneChange);
        Assert.True(page.Document!.IsDirty);
        _fx.Services.Undo.Undo();
        Assert.True(page.Meta.FadeOnSceneChange);
        _fx.Services.Commands.Execute("file.save");
        Capture.Pump();
        var meta = GameProject.Load<MusicMeta>(GameProject.MetaPath(page.Track!));
        Assert.Equal(0.5, meta.Volume);
        Assert.Equal(2, meta.LoopStart);
        Assert.True(meta.FadeOnSceneChange);
        Assert.False(page.Document.IsDirty);
    }

    [AvaloniaFact]
    public void TrackerTools_SelectPencilEraser_WriteTheModule()
    {
        var page = OpenAudio();
        var pattern = page.Module!.Patterns[page.Module.Order[0]];
        var source = Enumerable.Range(0, 64).First(r => pattern.Cells[r, 0].Note != 0);
        var target = Enumerable.Range(0, 64).First(r => pattern.Cells[r, 3].IsEmpty);
        Click(Find<IconButton>("Tool_cursor"));
        page.ApplyTool(source, 0);
        Click(Find<IconButton>("Tool_pencil"));
        Assert.Equal(AudioPage.Tool.Pencil, page.CurrentTool);
        page.ApplyTool(target, 3);
        Assert.Equal(pattern.Cells[source, 0], pattern.Cells[target, 3]);
        Click(Find<IconButton>("Tool_eraser"));
        page.ApplyTool(source, 0);
        Assert.True(pattern.Cells[source, 0].IsEmpty);
        _fx.Services.Undo.Undo();
        Assert.False(pattern.Cells[source, 0].IsEmpty);
        page.Save();
        var saved = TrackerModule.Load(page.Track!);
        Assert.Equal(pattern.Cells[target, 3], saved.Patterns[page.Module.Order[0]].Cells[target, 3]);
    }

    [AvaloniaFact]
    public void ChannelsMixerBudget_AndPreviewSfx()
    {
        var page = OpenAudio();
        var ch = page.Channels.ToDictionary(c => c.Name);
        Assert.Equal(("music L", 100.0), (ch["DSound A"].Value, ch["DSound A"].Percent));
        Assert.Equal("SFX jump +1", ch["Square 1"].Value);   // jump.sfx and laser.sfx
        Assert.InRange(ch["Square 1"].Percent, 45, 47);    // 0.21 s + 0.25 s of the channel per second
        Assert.Equal("SFX blip +1", ch["Square 2"].Value);
        Assert.Equal("free", ch["Wave"].Value);
        Assert.Equal("SFX hurt", ch["Noise"].Value);
        Assert.Equal(["Lead", "Bass", "Drums", "Pad", "Arp"], page.MixerLevels.Select(m => m.Name));
        Assert.InRange(page.MixerLevels[0].Db, -6.5, -5.5);  // pulse at 100/128, volume 40/64
        Assert.Equal(6, page.MixerCpuPercent);
        var expectedRom = page.MusicFiles.Sum(f => new FileInfo(f).Length) + page.SoundFiles.Sum(f => (long)Math.Ceiling(AudioPage.SoundSeconds(f) * 11025));
        Assert.Equal(expectedRom, page.AudioRomBytes);
        Assert.Equal($"Audio ROM {Math.Round(expectedRom / 1024.0)} KB", Find<StatusItem>("Status_AudioRom").Content);
        Click(Find<AgbButton>("PreviewSfx_jump"));
        _out.Pull(2);
        var wav = WavData.Load(Path.Combine(_p.AssetsDir, "audio", "sfx", "jump.wav")).Resample(AudioPreview.Rate);
        Assert.Equal((short)Math.Clamp((int)(wav[100] * 26000), -32768, 32767), _out.Pcm[200]);
    }

    [AvaloniaFact]
    public void PlusButtons_OpenImportAndSfxDesigner()
    {
        var page = OpenAudio();
        Click(Find<PanelHeader>("MusicHeader").GetVisualDescendants().OfType<Button>().First(b => b.Name == "PART_Action"));
        Assert.Equal(("T-D04", "AudioMusicAdd"), _fx.Services.Windows.Requests[^1]);
        _win.Overlays.CloseTop();
        Click(Find<PanelHeader>("SoundHeader").GetVisualDescendants().OfType<Button>().First(b => b.Name == "PART_Action"));
        Assert.Equal(("T-P25", "AudioSoundAdd"), _fx.Services.Windows.Requests[^1]);
        var sfx = Assert.IsType<SfxDesignerPage>(_win.CurrentPage);
        Assert.Equal("sfx_1.sfx", Path.GetFileName(sfx.FilePath));
        Assert.True(File.Exists(sfx.FilePath));
        _ = page;
    }
}
