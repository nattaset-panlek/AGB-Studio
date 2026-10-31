using AgbStudio.Build;
using AgbStudio.Build.Audio;
using AgbStudio.Build.Content;
using AgbStudio.Build.Pipeline;

namespace AgbStudio.Tests;

/// <summary>G-HW.08: audio channel use stays within the Maxmod settings (the project's audio.channels) and the
/// DMG channels do not clash. The runtime side (effects never exceed the mixing channels, one effect per DMG channel,
/// music never on a DMG channel) is audio_basic channel_use_within_maxmod_settings_and_dmg_channels_do_not_clash.</summary>
public sealed class AudioChannelTests
{
    static string Root => Path.Combine(NativeBuild.RepoRoot, "build", "test-projects");

    [Fact]
    public void ModuleChannelCountsAreReadFromTheFiles()
    {
        Assert.Equal(4, AudioChannels.ModuleChannels(Chiptune.SimpleTune("t", 120), ".mod"));
        Assert.Equal(6, AudioChannels.ModuleChannels(XmWriter.Tune("t", 120, 1, 1), ".xm"));
        var xm = new XmWriter { Channels = 12 };
        xm.Patterns.Add(xm.NewPattern());
        xm.Order.Add(0);
        Assert.Equal(12, AudioChannels.ModuleChannels(xm.Encode(), ".xm"));
        var eight = Chiptune.SimpleTune("t", 120);
        "8CHN"u8.CopyTo(eight.AsSpan(1080));
        Assert.Equal(8, AudioChannels.ModuleChannels(eight, ".mod"));
        "16CH"u8.CopyTo(eight.AsSpan(1080));
        Assert.Equal(16, AudioChannels.ModuleChannels(eight, ".mod"));
        Assert.Null(AudioChannels.ModuleChannels(new byte[100], ".mod"));
        Assert.Null(AudioChannels.ModuleChannels(new byte[100], ".xm"));
    }

    [Fact]
    public void ModulesMustFitTheMixingChannels()
    {
        var dir = Path.Combine(Root, "audio_channels");
        Directory.CreateDirectory(dir);
        var mod = Path.Combine(dir, "four.mod");
        var xm = Path.Combine(dir, "six.xm");
        File.WriteAllBytes(mod, Chiptune.SimpleTune("t", 120));
        File.WriteAllBytes(xm, XmWriter.Tune("t", 120, 1, 1));
        Assert.Empty(AudioChannels.Check(8, [mod, xm]));
        Assert.Empty(AudioChannels.Check(6, [mod, xm]));
        var problem = Assert.Single(AudioChannels.Check(4, [mod, xm]));
        Assert.Contains("'six.xm' plays 6 channels", problem, StringComparison.Ordinal);
        Assert.Contains(AudioChannels.Check(0, []), p => p.Contains("supports 1 to 16", StringComparison.Ordinal));
        Assert.Contains(AudioChannels.Check(17, []), p => p.Contains("supports 1 to 16", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildStopsWhenMusicNeedsMoreChannelsThanTheSettings()
    {
        var p = SampleProjects.CreatePlatformerMini(Path.Combine(Root, "audio_channels_build"));
        p.Settings.Audio.Channels = 2;  // the sample's music is a 4-channel MOD
        p.SaveSettings();
        var r = new GameBuilder(p, new Toolchain()).Build();
        Assert.False(r.Success);
        Assert.Contains(r.Diagnostics, d => d.ToString().Contains("plays 4 channels but the project mixes 2", StringComparison.Ordinal));
    }
}
