using AgbStudio.Build;

namespace AgbStudio.Tests;

/// <summary>P24.04: the ROM header written in C# is the one gbafix wrote, byte for byte.</summary>
public sealed class GbaHeaderTests
{
    static byte[] Noise(int seed)
    {
        var rom = new byte[4096];
        new Random(seed).NextBytes(rom);
        return rom;
    }

    [Theory]
    [InlineData("HELLO", "ABCD", "01", 3)]
    [InlineData("SKYHOPPER", "ASKE", "00", 0)]
    [InlineData("TWELVECHARSX", "Z9_A", "AB", 255)]
    public void SameBytesAsGbafix(string title, string code, string maker, int version)
    {
        var gbafix = Path.Combine(new Toolchain().DevkitPro, "tools", "bin", "gbafix.exe");
        if (!File.Exists(gbafix)) return;  // a PC without devkitPro has nothing to compare with
        var dir = Path.Combine(NativeBuild.RepoRoot, "build", "test-gbaheader");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{title}.gba");
        var input = Noise(title.Length * 31 + version);
        File.WriteAllBytes(path, input);
        var r = Proc.Run(gbafix, $"{path} -t{title} -c{code} -m{maker} -r{version}", dir, TimeSpan.FromSeconds(30));
        Assert.Equal(0, r.ExitCode);
        var expected = File.ReadAllBytes(path);

        var ours = (byte[])input.Clone();
        GbaHeader.Fix(ours, new RomHeader(title, code, maker, version));
        Assert.Equal(expected, ours);
    }

    [Fact]
    public void HeaderOfAFixedInput()
    {
        var rom = new byte[GbaHeader.Size];
        GbaHeader.Fix(rom, new RomHeader("AGB", "AGBE", "00", 1));
        Assert.Equal(0x24, rom[0x04]);                 // the logo starts
        Assert.Equal(0x07, rom[0x9F]);                 // and ends
        Assert.Equal("AGB\0\0\0\0\0\0\0\0\0AGBE00", System.Text.Encoding.ASCII.GetString(rom, 0xA0, 18));
        Assert.Equal(0x96, rom[0xB2]);
        Assert.Equal(1, rom[0xBC]);
        var sum = 0;
        for (var i = 0xA0; i <= 0xBD; i++) sum += rom[i];
        Assert.Equal(0, (sum + 0x19) & 0xFF);         // what the BIOS checks
    }

    [Fact]
    public void ARomShorterThanAHeaderIsRefused() =>
        Assert.Throws<ArgumentException>(() => GbaHeader.Fix(new byte[16], new RomHeader()));
}
