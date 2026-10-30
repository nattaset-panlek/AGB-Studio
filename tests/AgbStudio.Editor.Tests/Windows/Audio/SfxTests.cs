using AgbStudio.Build.Audio;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Support;
using AgbStudio.Editor.Tests.Windows.Build;
using AgbStudio.Editor.Windows.Audio;
using AgbStudio.Emu;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.Audio;

/// <summary>DMG register values and the hardware synthesis of the SFX Designer (P11.45).</summary>
public class DmgSoundTests
{
    static SfxFile Preset(string name)
    {
        var s = new SfxFile { Name = name.ToLowerInvariant() };
        SfxPresets.Apply(name, s);
        return s;
    }

    [Fact]
    public void Registers_AreTheHardwareEncodingOfTheFields()
    {
        var jump = Preset("Jump");
        var regs = DmgSound.Registers(jump).ToDictionary(r => r.Name, r => r.Value);
        // SOUND1CNT_L: sweep time 3, up, shift 6
        Assert.Equal(0x0036, regs["SOUND1CNT_L"]);
        // SOUND1CNT_H: volume 15, envelope down, step 2, duty 50 % (2), length 64 - 0.21 * 256 = 10
        Assert.Equal(0xF28A, regs["SOUND1CNT_H"]);
        // SOUND1CNT_X: restart, length on, rate 2048 - 131072 / 523.25 Hz (C5)
        Assert.Equal(0xC000 | 1798, regs["SOUND1CNT_X"]);
        Assert.Equal(["SOUND1CNT_L", "SOUND1CNT_H", "SOUND1CNT_X"], DmgSound.Shown(jump).Select(r => r.Name));

        var coin = Preset("Coin");
        Assert.Equal(["SOUND2CNT_L", "SOUND2CNT_H"], DmgSound.Shown(coin).Select(r => r.Name));
        var noise = Preset("Explosion");
        var nr = DmgSound.Registers(noise).ToDictionary(r => r.Name, r => r.Value);
        Assert.Equal(0x8000, nr["SOUND4CNT_H"] & 0xC000);   // 0.64 s is longer than the 0.25 s length counter: the runtime timer stops it
        Assert.Equal(7, nr["SOUND4CNT_H"] & 7);
        var wave = new SfxFile { Channel = DmgChannelKind.Wave, StartNote = "A4", Length = 0.5, Volume = 15 };
        var wr = DmgSound.Registers(wave);
        Assert.Equal(8, wr.Count(r => r.Name.StartsWith("WAVE_RAM", StringComparison.Ordinal)));
        Assert.Equal(0x2000 | 128, wr.Single(r => r.Name == "SOUND3CNT_H").Value);  // 100 %, length 256 - 128
        Assert.Equal(new[] { "SOUND3CNT_L", "SOUND3CNT_H", "SOUND3CNT_X" }, DmgSound.Shown(wave).Select(r => r.Name));
        Assert.Equal(13, DmgSound.Frames(Preset("Jump")));  // ceil(0.21 x 59.73)
    }

    static double ZeroCrossHz(float[] x, int rate, int from, int count)
    {
        var mean = x.Skip(from).Take(count).Average();
        var crossings = 0;
        for (var i = from + 1; i < from + count && i < x.Length; i++)
            if ((x[i - 1] - mean < 0) != (x[i] - mean < 0)) crossings++;
        return crossings / 2.0 / (count / (double)rate);
    }

    [Fact]
    public void Synth_FollowsPitchEnvelopeLengthAndSweepOverflow()
    {
        var coin = Preset("Coin");
        const int rate = 44100;
        var pcm = DmgSound.Render(coin, rate);
        Assert.InRange(ZeroCrossHz(pcm, rate, 0, rate / 20), 131072.0 / (2048 - DmgSound.SquareRate(DmgSound.Hz("B5"))) * 0.97, 131072.0 / (2048 - DmgSound.SquareRate(DmgSound.Hz("B5"))) * 1.03);
        var last = Array.FindLastIndex(pcm, v => Math.Abs(v) > 1e-4);
        Assert.InRange(last / (double)rate, 0.11, 0.125);
        var t = DmgSound.Simulate(coin);
        Assert.Equal(12, t.Volume[0]);
        Assert.Equal(11, t.Volume[(int)(2 / 64.0 * 512) + 2]);   // one step down after 2 / 64 s
        // jump: the sweep rises the pitch until the frequency overflows, just after its 0.21 s
        var jump = DmgSound.Simulate(Preset("Jump"));
        var pitches = jump.Pitch.Where(p => !double.IsNaN(p)).ToList();
        Assert.True(pitches[^1] > pitches[0] * 1.05);
        Assert.InRange(jump.End, 0.17, 0.2);   // the 8th sweep step (every 3/128 s) overflows
        // the fixture's jump.sfx (shift 3) overflows on its first sweep step
        var fixtureJump = DmgSound.Simulate(new SfxFile { Name = "jump", StartNote = "C5", SweepTime = 3, SweepShift = 3, Length = 0.21 });
        Assert.InRange(fixtureJump.End, 0.015, 0.03);
    }

    [Fact]
    public void SweepAmount_IsTheInverseOfTheShift()
    {
        Assert.Equal(2, SfxDesignerPage.SweepAmount(6));
        Assert.Equal(6, SfxDesignerPage.SweepShift(2));
        Assert.Equal(0, SfxDesignerPage.SweepAmount(0));
        foreach (var name in SfxPresets.Names) Assert.Equal(name, SfxPresets.Matching(Preset(name)));
    }

    /// <summary>
    /// "Preview sounds the same as EMU": the register writes of each preset are played by the mGBA core (D-06) through a
    /// small test ROM that copies (offset, value) pairs from EWRAM to the sound registers; its pitch and length are
    /// compared with the editor's preview synthesis.
    /// </summary>
    [AvaloniaFact]
    public void Preview_SoundsLikeTheEmulator()
    {
        using var fx = new Fixture();
        LibretroCore core;
        try { core = fx.Services.Emulator.Acquire(); }
        catch (Exception e) when (e is DllNotFoundException or InvalidOperationException or BadImageFormatException)
        {
            Assert.Fail("mGBA core not available: " + e.Message);
            return;
        }
        try
        {
            core.LoadRom(DmgTestRom.Build(), "dmg-test.gba");
            for (var i = 0; i < 10; i++) core.RunFrame();
            foreach (var name in new[] { "Coin", "Jump", "Select", "Hurt" })
            {
                var sfx = Preset(name);
                var regs = DmgSound.Registers(sfx);
                var ram = core.SystemRam;
                for (var i = 0; i < regs.Count; i++)
                {
                    BitConverter.TryWriteBytes(ram[(4 + i * 4)..], (ushort)regs[i].Offset);
                    BitConverter.TryWriteBytes(ram[(6 + i * 4)..], regs[i].Value);
                }
                BitConverter.TryWriteBytes(ram, regs.Count);
                core.DrainAudio();
                var frames = DmgSound.Frames(sfx);
                for (var f = 0; f < frames + 20; f++)
                {
                    core.RunFrame();
                    // the runtime stops the channel after the effect's frames (agb::audio::update)
                    if (f == frames) Stop(core, sfx.Channel);
                }
                var stereo = core.DrainAudio();
                var rate = (int)Math.Round(core.SampleRate);
                var emu = Enumerable.Range(0, stereo.Length / 2).Select(i => stereo[i * 2] / 32768f).ToArray();
                var start = Array.FindIndex(emu, v => Math.Abs(v - emu[0]) > 0.01);
                Assert.True(start >= 0, name + ": the emulator produced no sound");
                var emuSound = emu[start..];
                var ours = DmgSound.Render(sfx, rate);
                var sweeps = sfx.Channel == DmgChannelKind.Square1 && sfx.SweepTime > 0 && sfx.SweepShift > 0;
                if (sfx.Channel != DmgChannelKind.Noise && !sweeps)
                {
                    var window = rate / 20;
                    var a = ZeroCrossHz(emuSound, rate, 0, window);
                    var b = ZeroCrossHz(ours, rate, 0, window);
                    Assert.True(a / b is > 0.95 and < 1.05, $"{name}: emulator {a:0} Hz, preview {b:0} Hz");
                }
                // the end of the last 5 ms with several waveform edges (a single step when the channel is switched off does not count)
                double Length(float[] x)
                {
                    var peak = 0f;
                    for (var i = 1; i < x.Length; i++) peak = Math.Max(peak, Math.Abs(x[i] - x[i - 1]));
                    var edges = new List<int>();
                    for (var i = 1; i < x.Length; i++) if (Math.Abs(x[i] - x[i - 1]) > peak * 0.1f) edges.Add(i);
                    var w = rate / 200;
                    for (var k = edges.Count - 1; k >= 3; k--)
                        if (edges[k] - edges[k - 3] <= w) return edges[k] / (double)rate;
                    return 0;
                }
                var diff = Length(emuSound) - Length(ours);
                Assert.True(Math.Abs(diff) <= 0.03, $"{name}: emulator {Length(emuSound):0.000} s, preview {Length(ours):0.000} s");
            }
        }
        finally
        {
            fx.Services.Emulator.Release();
        }
    }

    static void Stop(LibretroCore core, DmgChannelKind ch)
    {
        var ram = core.SystemRam;
        var (off, val) = ch switch
        {
            DmgChannelKind.Square1 => (0x62, 0),
            DmgChannelKind.Square2 => (0x68, 0),
            DmgChannelKind.Wave => (0x70, 0),
            _ => (0x78, 0),
        };
        BitConverter.TryWriteBytes(ram[4..], (ushort)off);
        BitConverter.TryWriteBytes(ram[6..], (ushort)val);
        BitConverter.TryWriteBytes(ram, 1);
    }
}

/// <summary>
/// A 276-byte GBA program (ARM code written here, no toolchain): enables the sound unit, then polls EWRAM
/// 0x02000000 for a count of (I/O offset, value) halfword pairs at 0x02000004, writes them and clears the count.
/// </summary>
static class DmgTestRom
{
    public static byte[] Build()
    {
        var rom = new byte[0x200];
        uint[] code =
        [
            0xE3A00301, // mov r0, #0x04000000
            0xE3A01080, // mov r1, #0x80
            0xE1C018B4, // strh r1, [r0, #0x84]   SOUNDCNT_X: sound on
            0xE59F103C, // ldr r1, =0xFF77
            0xE1C018B0, // strh r1, [r0, #0x80]   SOUNDCNT_L: DMG channels on both sides, volume 7
            0xE3A01002, // mov r1, #2
            0xE1C018B2, // strh r1, [r0, #0x82]   SOUNDCNT_H: DMG volume 100 %
            0xE3A02402, // mov r2, #0x02000000
            0xE5923000, // loop: ldr r3, [r2]
            0xE3530000, // cmp r3, #0
            0x0AFFFFFC, // beq loop
            0xE2824004, // add r4, r2, #4
            0xE1D450B0, // copy: ldrh r5, [r4]
            0xE1D460B2, // ldrh r6, [r4, #2]
            0xE18060B5, // strh r6, [r0, r5]
            0xE2844004, // add r4, r4, #4
            0xE2533001, // subs r3, r3, #1
            0x1AFFFFF9, // bne copy
            0xE5823000, // str r3, [r2]
            0xEAFFFFF3, // b loop
            0x0000FF77, // literal
        ];
        BitConverter.TryWriteBytes(rom.AsSpan(0), 0xEA00002Eu);   // b 0xC0
        System.Text.Encoding.ASCII.GetBytes("AGBSFXTEST").CopyTo(rom, 0xA0);
        rom[0xB2] = 0x96;
        for (var i = 0; i < code.Length; i++) BitConverter.TryWriteBytes(rom.AsSpan(0xC0 + i * 4), code[i]);
        byte check = 0;
        for (var i = 0xA0; i < 0xBD; i++) check = (byte)(check - rom[i]);
        rom[0xBD] = (byte)(check - 0x19);
        return rom;
    }
}

/// <summary>SFX Designer E2E (P11.45): output, channel, duty, sliders, presets, graph, registers, script line, bake.</summary>
public class SfxDesignerTests : IDisposable
{
    readonly Fixture _fx = new();
    readonly GameProject _p;
    readonly CaptureAudioOutput _out = new();
    readonly MainWindow _win;

    public SfxDesignerTests()
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

    SfxDesignerPage Open(string? file = "jump.sfx")
    {
        _ = _fx.Services.Windows.Open("T-P25", file, source: "test");
        Capture.Pump();
        return Assert.IsType<SfxDesignerPage>(_win.CurrentPage);
    }

    string Reg(string name) => Find<Grid>("Reg_" + name).Children.OfType<TextBlock>().Last().Text!;

    [AvaloniaFact]
    public void OpensTheEffectWithItsFieldsRegistersAndScriptLine()
    {
        var page = Open();
        Assert.Equal(["blip.sfx", "coin.sfx", "hurt.sfx", "jump.sfx", "laser.sfx"], page.Files.Select(Path.GetFileName));
        Assert.Equal("Square 1", Find<TreeItem>("Sfx_jump.sfx").Meta);
        Assert.Equal("Noise", Find<TreeItem>("Sfx_hurt.sfx").Meta);
        Assert.True(Find<Segment>("Seg_Square 1 (sweep)").IsChecked);
        Assert.True(Find<Segment>("Seg_50%").IsChecked);
        Assert.True(Find<Segment>("Seg_Hardware (DMG)").IsChecked);
        Assert.Equal("Jump", page.ActivePreset);
        Assert.Equal(ChipStyle.Accent, Find<Chip>("Preset_Jump").Style);
        Assert.Equal("Sound.Play(\"jump\");", string.Concat(Find<SelectableTextBlock>("Snippet").Inlines!.OfType<Avalonia.Controls.Documents.Run>().Select(r => r.Text)));
        foreach (var r in DmgSound.Shown(page.Sfx)) Assert.Equal(r.Hex, Reg(r.Name));
        Assert.Equal("GBA DMG sound · Square 1", Find<Chip>("ChannelChip").Content);
        Assert.Equal("jump.sfx", _win.TitleBar.Document);
        Assert.Equal("jump.sfx · 0.21 s · 0 bytes of ROM", Find<StatusItem>("Status_Sfx").Content);
    }

    [AvaloniaFact]
    public void PresetsChannelDutyAndSliders_ChangeTheEffect()
    {
        var page = Open();
        Click(Find<Chip>("Preset_Laser"));
        Assert.Equal(0.125, page.Sfx.Duty);
        Assert.True(Find<Segment>("Seg_12.5%").IsChecked);
        Assert.Equal("Laser", page.ActivePreset);
        Assert.False(page.Sfx.SweepUp);
        Assert.Equal("pitch (sweep down)", _win.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text?.StartsWith("pitch", StringComparison.Ordinal) == true).Text);
        Click(Find<Segment>("Seg_Noise"));
        Assert.Equal(DmgChannelKind.Noise, page.Sfx.Channel);
        Assert.Equal(DmgSound.Shown(page.Sfx)[1].Hex, Reg("SOUND4CNT_H"));
        Click(Find<Segment>("Seg_Square 2"));
        Click(Find<Segment>("Seg_75%"));
        Assert.Equal(0.75, page.Sfx.Duty);
        Find<AgbSlider>("VolumeSlider").Value = 9;
        Assert.Equal(9, page.Sfx.Volume);
        Find<AgbSlider>("NoteSlider").Value = 36;  // C6
        Assert.Equal("C6", page.Sfx.StartNote);
        Find<AgbSlider>("LengthSlider").Value = 0.5;
        Assert.Equal(0.5, page.Sfx.Length);
        Assert.Null(page.ActivePreset);
        Assert.True(page.Document!.IsDirty);
        Assert.False(Find<StatusItem>("Status_Saved").IsVisible);
        // sliders of the same field merge into one undo step
        Find<AgbSlider>("VolumeSlider").Value = 8;
        Find<AgbSlider>("VolumeSlider").Value = 7;
        _fx.Services.Undo.Undo();
        Assert.Equal(9, page.Sfx.Volume);
    }

    [AvaloniaFact]
    public void Play_RendersTheEffectAndLoopRepeatsIt()
    {
        var page = Open();
        Click(Find<PlayControl>("SfxPlay"));
        var n = _out.Pull(10);
        var expected = page.PreviewSamples();
        Assert.InRange(n, expected.Length, expected.Length + 2047);
        Assert.Equal((short)Math.Clamp((int)(expected[500] * 26000), -32768, 32767), _out.Pcm[1000]);
        Click(Find<AgbButton>("LoopButton"));
        Assert.True(page.IsLooping);
        Assert.Equal(20 * 2048, _out.Pull(20));
    }

    [AvaloniaFact]
    public void BakeToWav_SavesTheSampleAndHardwareKeepsZeroBytes()
    {
        var page = Open("coin.sfx");
        Click(Find<Segment>("Seg_Bake to .wav"));
        Assert.True(page.Sfx.Bake);
        Assert.EndsWith("of ROM", (string)Find<StatusItem>("Status_Sfx").Content!);
        _fx.Services.Commands.Execute("file.save");
        Capture.Pump();
        var wav = Path.Combine(_p.AssetsDir, "audio", "sfx", "coin.wav");
        var data = WavData.Load(wav);
        Assert.Equal(11025, data.Rate);
        Assert.Equal(8, data.Bits);
        Assert.Equal(DmgSound.Bake(page.Sfx).Length, data.Samples.Length);
        Assert.True(GameProject.Load<SfxFile>(Path.Combine(_p.SfxDir, "coin.sfx")).Bake);
        Assert.True(Find<StatusItem>("Status_Saved").IsVisible);
    }

    [AvaloniaFact]
    public void WaveChannel_PencilDrawsWaveRam_AndPlusCreatesAnEffect()
    {
        var page = Open();
        Click(Find<Segment>("Seg_Wave"));
        Click(Find<IconButton>("Tool_pencil"));
        Assert.True(page.PencilTool);
        var box = Find<WaveformBox>("WaveformBox");
        box.DrawAt(new Point(box.Bounds.Width * 0.05, 18));
        Assert.Equal(15, page.Sfx.Wave[1]);
        Assert.Contains(DmgSound.Registers(page.Sfx), r => r.Name == "WAVE_RAM0_L");
        Click(Find<PanelHeader>("SfxHeader").GetVisualDescendants().OfType<Button>().First(b => b.Name == "PART_Action"));
        Assert.Equal("sfx_1.sfx", Path.GetFileName(page.FilePath));
        Assert.Contains(page.Files, f => f.EndsWith("sfx_1.sfx", StringComparison.Ordinal));
        Click(Find<DocTab>("Doc_Audio"));
        Assert.IsType<AudioPage>(_win.CurrentPage);
    }
}
