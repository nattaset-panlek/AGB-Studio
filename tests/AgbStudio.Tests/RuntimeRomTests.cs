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

    // P2.08: the whole sequence of every stream (and bounded values) equals the host copy of the generator.
    [Fact]
    public void RngMatchesHostReference()
    {
        Assert.True(s_build.Value.Success, s_build.Value.Log);
        var result = RomTestRunner.Run(emu.Core, RuntimeTests.RomPath("core_basics"), new RomTestOptions { MaxFrames = 3000 });
        Assert.True(result.Passed, string.Join('\n', result.Log));
        for (var stream = 0; stream < 3; stream++)
        {
            var rom = result.Log.Where(l => l.StartsWith($"rng {stream} ", StringComparison.Ordinal))
                .SelectMany(l => l.Split(' ').Skip(3)).Select(h => Convert.ToUInt32(h, 16)).ToArray();
            Assert.Equal(XoshiroReference.Sequence(1234, stream, 16), rom);
        }
        var range = result.Log.Single(l => l.StartsWith("rng range ", StringComparison.Ordinal)).Split(' ').Skip(2).Select(int.Parse).ToArray();
        Assert.Equal(XoshiroReference.Range(1234, -3, 3, 16), range);
    }

    // P2.09: fixed-point multiply, divide, sine table and square root against values computed exactly on the host.
    [Fact]
    public void FixedPointMatchesHostReference()
    {
        Assert.True(s_build.Value.Success, s_build.Value.Log);
        var result = RomTestRunner.Run(emu.Core, RuntimeTests.RomPath("core_basics"), new RomTestOptions { MaxFrames = 3000 });
        Assert.True(result.Passed, string.Join('\n', result.Log));
        var pairs = result.Log.Where(l => l.StartsWith("fx ", StringComparison.Ordinal)).Select(l => l.Split(' ').Skip(1).Select(long.Parse).ToArray()).ToList();
        Assert.Equal(32, pairs.Count);
        foreach (var p in pairs)
        {
            long a = p[0], b = p[1];
            // 24.8: a * b rounds toward negative infinity (arithmetic shift), a / b truncates toward zero (C# casts).
            var mul = (long)decimal.Floor((decimal)a * b / 256m);
            var div = (long)decimal.Truncate((decimal)a * 256m / b);
            Assert.True(mul == p[2], $"{a} * {b}: ROM {p[2]}, reference {mul}");
            Assert.True(div == p[3], $"{a} / {b}: ROM {p[3]}, reference {div}");
            // Within one step (1/256) of the real quotient.
            Assert.True(Math.Abs(p[3] / 256.0 - (double)a / b) < 1 / 256.0 + 1e-12, $"{a} / {b}");
        }
        var sin = result.Log.Where(l => l.StartsWith("sin ", StringComparison.Ordinal)).SelectMany(l => l.Split(' ').Skip(2).Select(int.Parse)).ToArray();
        Assert.Equal(256, sin.Length);
        for (var i = 0; i < 256; i++)
            Assert.True(Math.Abs(sin[i] - Math.Round(256 * Math.Sin(i * Math.PI / 128))) <= 1, $"sin({i}): ROM {sin[i]}");
        var sqrt = result.Log.Where(l => l.StartsWith("sqrt ", StringComparison.Ordinal)).Select(l => l.Split(' ').Skip(1).Select(long.Parse).ToArray()).ToList();
        Assert.Equal(16, sqrt.Count);
        foreach (var q in sqrt) Assert.Equal((long)Math.Floor(Math.Sqrt(q[0] * 256.0)), q[1]);
        // BUD: the measured cycles per operation are in the log for the record.
        Assert.Contains(result.Log, l => l.StartsWith("fixed cycles: mul ", StringComparison.Ordinal));
    }

    // P2.07: in a Debug build a stale pool handle and a double free stop the game with the assert message, file and
    // line in the Console; a Release build has no asserts and the pool refuses both without harm.
    [Theory]
    [InlineData(false, "stale handle used after the object was destroyed")]
    [InlineData(true, "double free or stale handle")]
    public void PoolAssertsCatchStaleHandlesAndDoubleFrees(bool doubleFree, string message)
    {
        var input = new Dictionary<int, GbaKeys> { [0] = doubleFree ? GbaKeys.A : GbaKeys.None };
        var debug = RuntimeTests.BuildAll(["host_assert"]);
        Assert.True(debug.Success, debug.Log);
        var r = RomTestRunner.Run(emu.Core, RuntimeTests.RomPath("host_assert"), new RomTestOptions { MaxFrames = 120, Input = input });
        Assert.True(r.Status == TestStatus.Fail, string.Join('\n', r.Log));
        Assert.Contains(r.Log, l => l.StartsWith("assert failed: memory.hpp(", StringComparison.Ordinal) && l.Contains(message, StringComparison.Ordinal));
        Assert.Contains("TEST FAIL: assert", r.Log);

        var release = new NativeBuild(new Toolchain(), Path.Combine(RuntimeTests.BuildDir, "release"))
            .Build([RuntimeTests.Target("host_assert", BuildConfig.Release)]);
        Assert.True(release.Success, release.Log);
        var rr = RomTestRunner.Run(emu.Core, release.RomPath, new RomTestOptions { MaxFrames = 120, Input = input });
        emu.Core.Keys = GbaKeys.None;  // the shared core keeps the last keys for the next test
        Assert.True(rr.Passed, $"{rr.Error}\n{string.Join('\n', rr.Log)}");
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
    public static uint First(uint seed) => Sequence(seed, 0, 1)[0];

    // rng::seed(seed) seeds stream i with seed + i * 0x51ED270B.
    public static uint[] Sequence(uint seed, int stream, int count)
    {
        var s = Seed(seed + (uint)stream * 0x51ED270Bu);
        var result = new uint[count];
        for (var i = 0; i < count; i++) result[i] = Next(s);
        return result;
    }

    // rng::range(lo, hi) on the Gameplay stream: rejection sampling below a multiple of the bound.
    public static int[] Range(uint seed, int lo, int hi, int count)
    {
        var s = Seed(seed);
        var bound = (uint)(hi - lo + 1);
        var limit = 0xFFFFFFFFu - 0xFFFFFFFFu % bound;
        var result = new int[count];
        for (var i = 0; i < count; i++)
        {
            uint v;
            do v = Next(s);
            while (v >= limit);
            result[i] = lo + (int)(v % bound);
        }
        return result;
    }

    static uint[] Seed(uint seed)
    {
        var x = seed;
        var s = new uint[4];
        for (var i = 0; i < 4; i++) s[i] = SplitMix(ref x);
        if ((s[0] | s[1] | s[2] | s[3]) == 0) s[0] = 1;
        return s;
    }

    static uint Next(uint[] x)
    {
        var result = BitOperations.RotateLeft(x[1] * 5, 7) * 9;
        var t = x[1] << 9;
        x[2] ^= x[0];
        x[3] ^= x[1];
        x[1] ^= x[2];
        x[0] ^= x[3];
        x[2] ^= t;
        x[3] = BitOperations.RotateLeft(x[3], 11);
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
