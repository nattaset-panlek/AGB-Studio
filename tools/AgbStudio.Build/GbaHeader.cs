namespace AgbStudio.Build;

/// <summary>
/// P24.04: writes the cartridge header of a built ROM, as devkitPro's gbafix did: the boot logo, title, game code, maker
/// code, the fixed value 0x96, device type 0, version and the header checksum. Layout from the GBA's documented header
/// (0x04 logo, 0xA0 title, 0xAC game code, 0xB0 maker code, 0xB2 0x96, 0xB4 device type, 0xBC version, 0xBD checksum).
/// </summary>
public static class GbaHeader
{
    public const int Size = 0xC0;

    /// <summary>The 156-byte logo the GBA's BIOS compares at boot: a ROM without it does not start on the console. Every
    /// GBA toolchain writes these bytes; they are data the hardware requires, not part of any program.</summary>
    static readonly byte[] Logo = Convert.FromHexString(
        "24FFAE51699AA2213D84820A84E409AD11248B98C0817F21A352BE199309CE2010464A4AF82731EC58C7E83382E3CEBF85F4DF94CE4B09C194568AC0" +
        "1372A7FC9F844D73A3CA9A615897A327FC039876231DC7610304AE56BF38840040A70EFDFF52FE036F9530F197FBC08560D68025A963BE03014E38E2F9" +
        "A234FFBB3E0344780090CB88113A9465C07C6387F03CAFD625E48B380AAC7221D4F807");

    /// <summary>Fixes the header of a ROM in memory. Title and codes are written as given (the build cleans them first);
    /// shorter ones are padded with zeros.</summary>
    public static void Fix(byte[] rom, RomHeader h)
    {
        if (rom.Length < Size) throw new ArgumentException($"A GBA ROM is at least {Size} bytes; this one is {rom.Length}.", nameof(rom));
        Logo.CopyTo(rom, 0x04);
        Put(rom, 0xA0, 12, h.Title);
        Put(rom, 0xAC, 4, h.GameCode);
        Put(rom, 0xB0, 2, h.Maker);
        rom[0xB2] = 0x96;
        rom[0xB4] = 0;
        rom[0xBC] = (byte)h.Version;
        rom[0xBD] = Checksum(rom);
        rom[0xBE] = rom[0xBF] = 0;
    }

    /// <summary>Fixes the header of a ROM file in place.</summary>
    public static void Fix(string path, RomHeader h)
    {
        var rom = File.ReadAllBytes(path);
        var before = rom[..Size];
        Fix(rom, h);
        if (!rom.AsSpan(0, Size).SequenceEqual(before)) File.WriteAllBytes(path, rom);
    }

    /// <summary>The complement check of bytes 0xA0..0xBC that the BIOS also checks.</summary>
    public static byte Checksum(byte[] rom)
    {
        var sum = 0;
        for (var i = 0xA0; i <= 0xBC; i++) sum += rom[i];
        return (byte)(-(sum + 0x19));
    }

    static void Put(byte[] rom, int at, int length, string text)
    {
        for (var i = 0; i < length; i++) rom[at + i] = i < text.Length ? (byte)text[i] : (byte)0;
    }
}
