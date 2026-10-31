using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace AgbStudio.Editor.Windows.Build;

/// <summary>What a device reports about the cartridge (or SD card) in it, as the Flash dialog shows it.</summary>
/// <param name="FlashMegabytes">The cartridge's flash size when the device can measure it (GBxCart RW).</param>
public sealed record CartridgeStatus(bool Detected, string Summary, string Detail, string? CurrentTitle = null, int? FlashMegabytes = null)
{
    /// <summary>
    /// T-D07 cartridge line: "Cartridge detected · 32 MB flash · SRAM 32 KB" from the measured flash size and the save
    /// chip the game uses (Project Settings › Save data); the device's own summary when the size is not known.
    /// </summary>
    public string Line(AgbStudio.Core.Project.SaveType save) => !Detected || FlashMegabytes is not { } mb ? Summary
        : $"Cartridge detected · {mb} MB flash" + (save == AgbStudio.Core.Project.SaveType.None ? "" : " · " + Settings.ProjectSettingsModel.SaveTypeSegments[Settings.ProjectSettingsModel.SaveTypes.ToList().IndexOf(save)]);

    /// <summary>
    /// The flash size of a cartridge from its address mirrors: a chip smaller than the 32 MB address space repeats
    /// itself, so the first 64 bytes come back at 4, 8 or 16 MB. <paramref name="readAt"/> reads 64 bytes at a byte
    /// offset; a cartridge with no mirror is 32 MB.
    /// </summary>
    public static int FlashSizeFromMirrors(Func<int, byte[]> readAt)
    {
        var first = readAt(0);
        foreach (var mb in new[] { 4, 8, 16 })
            if (readAt(mb * 1024 * 1024).AsSpan().SequenceEqual(first)) return mb;
        return 32;
    }
}

/// <summary>
/// A way to put the ROM on a cartridge (D-12): (a) a flash cart's SD card (EZ-Flash, EverDrive and similar), fully
/// supported; (b) GBxCart RW over USB serial, verified with hardware later. Tests use a fake device; nothing in the
/// tests writes to a real device.
/// </summary>
public interface ICartridgeDevice
{
    string Name { get; }
    Task<CartridgeStatus> DetectAsync(CancellationToken ct);
    /// <summary>Reads the save of the game on the cartridge; null when there is none.</summary>
    Task<byte[]?> ReadSaveAsync(string romName, IProgress<long> progress, CancellationToken ct);
    Task WriteRomAsync(string romName, byte[] rom, IProgress<long> progress, CancellationToken ct);
    Task<bool> VerifyAsync(string romName, byte[] rom, IProgress<long> progress, CancellationToken ct);
}

public interface ICartridgeDeviceProvider
{
    IReadOnlyList<ICartridgeDevice> Find();
}

/// <summary>The devices of this PC: GBxCart RW serial ports (CH340, USB VID 1A86 PID 7523) and removable drives.</summary>
public sealed class SystemCartridgeDevices : ICartridgeDeviceProvider
{
    public IReadOnlyList<ICartridgeDevice> Find()
    {
        var list = new List<ICartridgeDevice>();
        if (OperatingSystem.IsWindows()) foreach (var port in GbxCartRw.Ports()) list.Add(new GbxCartRw(port));
        foreach (var d in DriveInfo.GetDrives())
        {
            try { if (d.DriveType == DriveType.Removable && d.IsReady) list.Add(new SdCardFlashCart(d.RootDirectory.FullName)); }
            catch (IOException) { }
        }
        return list;
    }
}

/// <summary>A flash cart's SD card: the ROM is copied to the card root, the save next to it or in the cart's save folder.</summary>
public sealed class SdCardFlashCart(string root) : ICartridgeDevice
{
    public string Root { get; } = root;

    public string Name
    {
        get
        {
            var kind = Directory.Exists(Path.Combine(Root, "GBASYS")) ? "EverDrive GBA" : Directory.Exists(Path.Combine(Root, "SAVER")) ? "EZ-Flash" : "Flash cart";
            return $"{kind} · SD card ({Root.TrimEnd('\\', '/')})";
        }
    }

    public Task<CartridgeStatus> DetectAsync(CancellationToken ct)
    {
        try
        {
            var d = new DriveInfo(Root);
            var free = d.AvailableFreeSpace / (1024.0 * 1024 * 1024);
            return Task.FromResult(new CartridgeStatus(true, $"SD card detected · {free.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} GB free · {d.DriveFormat}", "Writes the .gba to the card root"));
        }
        catch (Exception e) when (e is IOException or ArgumentException or UnauthorizedAccessException)
        {
            return Task.FromResult(new CartridgeStatus(false, "No SD card", e.Message));
        }
    }

    IEnumerable<string> SaveCandidates(string romName)
    {
        var stem = Path.GetFileNameWithoutExtension(romName);
        yield return Path.Combine(Root, stem + ".sav");
        yield return Path.Combine(Root, "SAVER", stem + ".sav");
        yield return Path.Combine(Root, "GBASYS", "save", stem + ".sav");
        yield return Path.Combine(Root, "GBASYS", "save", stem + ".srm");
    }

    public async Task<byte[]?> ReadSaveAsync(string romName, IProgress<long> progress, CancellationToken ct)
    {
        var f = SaveCandidates(romName).FirstOrDefault(File.Exists);
        if (f is null) return null;
        var bytes = await File.ReadAllBytesAsync(f, ct).ConfigureAwait(false);
        progress.Report(bytes.Length);
        return bytes;
    }

    public async Task WriteRomAsync(string romName, byte[] rom, IProgress<long> progress, CancellationToken ct)
    {
        var target = Path.Combine(Root, romName);
        var temp = target + ".part";
        var fs = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, true);
        await using (fs.ConfigureAwait(false))
        {
            for (var o = 0; o < rom.Length; o += 32 * 1024)
            {
                ct.ThrowIfCancellationRequested();
                var n = Math.Min(32 * 1024, rom.Length - o);
                await fs.WriteAsync(rom.AsMemory(o, n), ct).ConfigureAwait(false);
                progress.Report(o + n);
            }
            await fs.FlushAsync(ct).ConfigureAwait(false);
        }
        File.Move(temp, target, true);
    }

    public async Task<bool> VerifyAsync(string romName, byte[] rom, IProgress<long> progress, CancellationToken ct)
    {
        var written = await File.ReadAllBytesAsync(Path.Combine(Root, romName), ct).ConfigureAwait(false);
        progress.Report(written.Length);
        return written.AsSpan().SequenceEqual(rom);
    }
}

/// <summary>
/// GBxCart RW (insideGadgets) over its USB serial port at 1 Mbaud: detection by the CH340 USB id, firmware handshake
/// ('h' PCB version, 'V' firmware version) and reading the cartridge header (GBA mode 'G', 3.3 V '3', address 'A',
/// read 'r' in 64-byte blocks). Writing a flash cart through the device protocol is verified with hardware later
/// (D-12, P11.60 "n/a until hardware"), so <see cref="WriteRomAsync"/> refuses instead of guessing the protocol.
/// </summary>
public sealed class GbxCartRw(string port) : ICartridgeDevice
{
    public string Port { get; } = port;
    public string Name => $"GBxCart RW · USB ({Port})";

    /// <summary>COM ports of CH340 USB serial adapters (the GBxCart RW chip), from the registry.</summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public static IEnumerable<string> Ports()
    {
        var result = new List<string>();
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\USB\VID_1A86&PID_7523");
            if (key is null) return result;
            foreach (var inst in key.GetSubKeyNames())
            {
                using var p = key.OpenSubKey(inst + @"\Device Parameters");
                if (p?.GetValue("PortName") is string name && name.StartsWith("COM", StringComparison.OrdinalIgnoreCase)) result.Add(name);
            }
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException) { }
        return result.Distinct();
    }

    public async Task<CartridgeStatus> DetectAsync(CancellationToken ct) => await Task.Run(() =>
    {
        try
        {
            using var s = SerialPort.Open(Port, 1_000_000);
            s.Write("0"u8);
            s.Write("h"u8);
            var pcb = s.Read(1, 500);
            s.Write("V"u8);
            var fw = s.Read(1, 500);
            if (pcb.Length == 0 || fw.Length == 0) return new CartridgeStatus(false, "GBxCart RW not responding", "Check the USB cable and the firmware");
            s.Write("G"u8);
            s.Write("3"u8);
            var header = new byte[192];
            for (var o = 0; o < 192; o += 64)
            {
                s.Write(Encoding.ASCII.GetBytes($"A{o / 2:x}\0"));
                s.Write("r"u8);
                var block = s.Read(64, 1000);
                if (block.Length < 64) return new CartridgeStatus(false, "No cartridge", "Insert a cartridge into the GBxCart RW");
                block.CopyTo(header, o);
                s.Write("0"u8);
            }
            var title = Encoding.ASCII.GetString(header, 0xA0, 12).TrimEnd('\0', ' ');
            var code = Encoding.ASCII.GetString(header, 0xAC, 4).TrimEnd('\0');
            if (header[0xB2] != 0x96) return new CartridgeStatus(false, "No cartridge", "Insert a cartridge into the GBxCart RW");
            byte[] ReadAt(int offset)
            {
                s.Write(Encoding.ASCII.GetBytes($"A{offset / 2:x}\0"));
                s.Write("r"u8);
                var block = s.Read(64, 1000);
                s.Write("0"u8);
                return block;
            }
            var mb = CartridgeStatus.FlashSizeFromMirrors(ReadAt);
            return new CartridgeStatus(true, $"Cartridge detected · {code}", $"Current game: {title} rev {header[0xBC]}", title, mb);
        }
        catch (IOException e)
        {
            return new CartridgeStatus(false, "GBxCart RW not available", e.Message);
        }
    }, ct);

    public Task<byte[]?> ReadSaveAsync(string romName, IProgress<long> progress, CancellationToken ct) =>
        Task.FromException<byte[]?>(new NotSupportedException("Reading saves through GBxCart RW is verified on hardware later (D-12)."));

    public Task WriteRomAsync(string romName, byte[] rom, IProgress<long> progress, CancellationToken ct) =>
        Task.FromException(new NotSupportedException("Flashing through GBxCart RW is verified on hardware later (D-12). Copy the ROM to a flash cart's SD card instead."));

    public Task<bool> VerifyAsync(string romName, byte[] rom, IProgress<long> progress, CancellationToken ct) => Task.FromResult(false);
}

/// <summary>A Win32 serial port (no System.IO.Ports package): CreateFile, SetCommState, ReadFile / WriteFile.</summary>
sealed class SerialPort : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    struct Dcb
    {
        public int DCBlength;
        public uint BaudRate;
        public uint Flags;
        public ushort Reserved, XonLim, XoffLim;
        public byte ByteSize, Parity, StopBits;
        public sbyte XonChar, XoffChar, ErrorChar, EofChar, EvtChar;
        public ushort Reserved1;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct CommTimeouts { public uint ReadInterval, ReadMultiplier, ReadConstant, WriteMultiplier, WriteConstant; }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool GetCommState(SafeFileHandle h, ref Dcb dcb);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool SetCommState(SafeFileHandle h, ref Dcb dcb);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool SetCommTimeouts(SafeFileHandle h, ref CommTimeouts t);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool ReadFile(SafeFileHandle h, byte[] buffer, int count, out int read, IntPtr overlapped);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool WriteFile(SafeFileHandle h, byte[] buffer, int count, out int written, IntPtr overlapped);

    readonly SafeFileHandle _h;

    SerialPort(SafeFileHandle h) => _h = h;

    public static SerialPort Open(string port, uint baud)
    {
        var h = CreateFileW(@"\\.\" + port, 0xC0000000, 0, IntPtr.Zero, 3, 0, IntPtr.Zero);
        if (h.IsInvalid) throw new IOException($"{port} cannot be opened (error {Marshal.GetLastWin32Error()}).");
        var dcb = new Dcb { DCBlength = Marshal.SizeOf<Dcb>() };
        GetCommState(h, ref dcb);
        dcb.BaudRate = baud;
        dcb.ByteSize = 8;
        dcb.Parity = 0;
        dcb.StopBits = 0;
        dcb.Flags = 1;  // binary mode
        if (!SetCommState(h, ref dcb)) { h.Dispose(); throw new IOException($"{port} does not accept {baud} baud."); }
        return new SerialPort(h);
    }

    public void Write(ReadOnlySpan<byte> data)
    {
        var buf = data.ToArray();
        if (!WriteFile(_h, buf, buf.Length, out _, IntPtr.Zero)) throw new IOException("Serial write failed.");
    }

    public byte[] Read(int count, int timeoutMs)
    {
        var t = new CommTimeouts { ReadInterval = 0, ReadMultiplier = 0, ReadConstant = (uint)timeoutMs };
        SetCommTimeouts(_h, ref t);
        var buf = new byte[count];
        var total = 0;
        while (total < count)
        {
            var chunk = new byte[count - total];
            if (!ReadFile(_h, chunk, chunk.Length, out var n, IntPtr.Zero) || n == 0) break;
            Array.Copy(chunk, 0, buf, total, n);
            total += n;
        }
        return buf[..total];
    }

    public void Dispose() => _h.Dispose();
}
