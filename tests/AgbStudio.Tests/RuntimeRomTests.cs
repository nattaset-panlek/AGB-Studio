using System.Numerics;
using AgbStudio.Build;
using AgbStudio.Core;
using AgbStudio.Emu;

namespace AgbStudio.Tests;

/// <summary>Builds every runtime test ROM (runtime/tests/*) once and runs each in the emulator.</summary>
[Collection("emulator")]
public sealed class RuntimeRomTests(EmulatorFixture emu)
{
    static readonly Lazy<NativeBuildResult> s_build = new(() => RuntimeTests.BuildAll());

    public static TheoryData<string> Names()
    {
        var data = new TheoryData<string>();
        foreach (var n in RuntimeTests.Discover()) data.Add(n);
        return data;
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void RomPasses(string name)
    {
        Assert.True(s_build.Value.Success, s_build.Value.Log);
        var result = RomTestRunner.Run(emu.Core, RuntimeTests.RomPath(name), new RomTestOptions { MaxFrames = 3000 });
        Png.WriteArgb(Path.Combine(RuntimeTests.BuildDir, name + ".png"), result.LastFrame, LibretroCore.Width, LibretroCore.Height);
        Assert.True(result.Passed, $"{name}: {result.Error}\n{string.Join('\n', result.Log)}");
    }

    [Fact]
    public void RngMatchesHostReference()
    {
        Assert.True(s_build.Value.Success, s_build.Value.Log);
        var result = RomTestRunner.Run(emu.Core, RuntimeTests.RomPath("core_basics"), new RomTestOptions { MaxFrames = 3000 });
        var line = result.Log.First(l => l.StartsWith("rng first ", StringComparison.Ordinal));
        Assert.Equal(XoshiroReference.First(1234).ToString(), line["rng first ".Length..]);
    }

    [Fact]
    public void ReleaseBuildStripsLogText()
    {
        var target = RuntimeTests.Target("p0_hello", BuildConfig.Release);
        var dir = Path.Combine(RuntimeTests.BuildDir, "release");
        var r = new NativeBuild(new Toolchain(), dir).Build([target]);
        Assert.True(r.Success, r.Log);
        var rom = File.ReadAllBytes(r.RomPath);
        Assert.Equal(-1, rom.AsSpan().IndexOf("hello from AGB Studio runtime"u8));
        var debugRom = File.ReadAllBytes(RuntimeTests.RomPath("p0_hello"));
        Assert.True(debugRom.AsSpan().IndexOf("hello from AGB Studio runtime"u8) >= 0);
    }
}

/// <summary>Host copy of the runtime RNG (xoshiro128** seeded through splitmix32) for determinism checks.</summary>
public static class XoshiroReference
{
    public static uint First(uint seed)
    {
        // rng::seed(seed) seeds the Gameplay stream with seed + 0 * 0x51ED270B.
        var x = seed;
        var s = new uint[4];
        for (var i = 0; i < 4; i++) s[i] = SplitMix(ref x);
        var result = BitOperations.RotateLeft(s[1] * 5, 7) * 9;
        return result;
    }

    static uint SplitMix(ref uint x)
    {
        var z = x += 0x9E3779B9u;
        z = (z ^ (z >> 16)) * 0x85EBCA6Bu;
        z = (z ^ (z >> 13)) * 0xC2B2AE35u;
        return z ^ (z >> 16);
    }
}
