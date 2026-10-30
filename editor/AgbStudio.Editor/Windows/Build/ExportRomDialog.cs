using System.Globalization;
using System.Text.RegularExpressions;
using AgbStudio.Build;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace AgbStudio.Editor.Windows.Build;

/// <summary>
/// Export ROM (T-D05, 560 x 488; File › Export ROM (.gba)…). File name, folder, ROM title / game code / version, the
/// options (LZ77 graphics, strip debug info and logs, pad to a power of two) and the estimated size. Export saves the
/// header and options to Project Settings, builds a Release ROM through Build ROM (T-D06) and copies it (P11.59).
/// </summary>
[EditorWindow("T-D05")]
public sealed class ExportRomDialog : FigmaDialog
{
    readonly EditorServices _s;
    readonly TextBlock _estimate = UI.Text("", "Mono/10", "text/secondary");
    readonly AgbProgress _estimateBar = new() { Status = ProgressStatus.Accent, VerticalAlignment = VerticalAlignment.Center, Name = "EstimateBar" };

    public TextField FileName { get; } = new() { Label = "File name", Name = "ExportFileName" };
    public SelectField Folder { get; } = new() { Label = "Folder", Name = "ExportFolder" };
    public TextField RomTitle { get; } = new() { Label = "ROM title", Name = "ExportRomTitle" };
    public TextField GameCode { get; } = new() { Label = "Game code", Name = "ExportGameCode" };
    public TextField Version { get; } = new() { Label = "Version", Name = "ExportVersion" };
    public CheckBox Compress { get; } = new() { Content = "Compress graphics (LZ77)", Name = "OptCompress" };
    public CheckBox Strip { get; } = new() { Content = "Strip debug info and logs", Name = "OptStrip" };
    public CheckBox Pad { get; } = new() { Content = "Pad ROM to a power of two", Name = "OptPad" };
    public string EstimateText => _estimate.Text ?? "";
    public BuildRequest? Request { get; private set; }

    public ExportRomDialog(WindowContext ctx) : this(ctx.Services) { }

    public ExportRomDialog(EditorServices s)
    {
        _s = s;
        Name = "ExportRomDialog";
        var p = s.Project.Current;
        var settings = s.ProjectSettings.Settings;
        FileName.Text = p is null ? "game.gba" : Path.GetFileName(new GameBuilder(p, new Toolchain()).RomPath);
        Folder.Selected = p is null ? "" : Path.GetFullPath(Path.Combine(p.Root, settings.Build.Output.Length > 0 ? settings.Build.Output : "build")).TrimEnd(Path.DirectorySeparatorChar, '/');
        RomTitle.Text = settings.General.RomTitle;
        GameCode.Text = settings.General.GameCode;
        Version.Text = settings.General.Version;
        Compress.IsChecked = settings.Build.CompressGraphics;
        Strip.IsChecked = settings.Build.StripDebugLog;
        Pad.IsChecked = false;
        Folder.TemplateApplied += (_, e) => { if (e.NameScope.Find<Button>("PART_Box") is { } b) b.Click += async (_, _) => await BrowseAsync(); };
        Pad.IsCheckedChanged += (_, _) => UpdateEstimate();

        var header = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("*,*,*"), ColumnSpacing = 11 };
        Grid.SetColumn(GameCode, 1);
        Grid.SetColumn(Version, 2);
        header.Children.AddRange([RomTitle, GameCode, Version]);
        var options = UI.Column(15, Compress, Strip, Pad);
        var label = UI.Text("Estimated size", "Label/11", "text/secondary").Wrap(70);
        _estimate.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        _estimate.TextAlignment = Avalonia.Media.TextAlignment.Right;
        var size = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("70,12,*,20,66") };
        Grid.SetColumn(_estimateBar, 2);
        Grid.SetColumn(_estimate, 4);
        size.Children.AddRange([label, _estimateBar, _estimate]);
        var overline = UI.Text("Options", "Overline/11", "text/secondary");
        var content = UI.Column(14, FileName, Folder, header, overline, options, size);
        options.Margin = new Thickness(0, 0, 0, 0);
        content.Margin = new Thickness(20, 20, 20, 15);

        var footer = new DialogFooter { CancelText = "Cancel", ConfirmText = "Export .gba" };
        footer.Cancel += (_, _) => Close(null);
        footer.Confirm += (_, _) => Export();
        Build("Export ROM", "chip", content, footer, 560);
        UpdateEstimate();
    }

    async Task BrowseAsync()
    {
        var folder = await _s.System.PickFolderAsync("Export folder");
        if (folder != null) Folder.Selected = folder;
    }

    /// <summary>Estimated size: the last build's ROM, padded when "Pad ROM to a power of two" is on.</summary>
    public long EstimatedBytes()
    {
        var bytes = _s.Build.LoadReport()?.RomBytes ?? 0;
        return Pad.IsChecked == true && bytes > 0 ? NextPowerOfTwo(bytes) : bytes;
    }

    void UpdateEstimate()
    {
        var bytes = EstimatedBytes();
        const long capacity = 32L * 1024 * 1024;
        _estimate.Text = bytes > 0 ? $"{Math.Round(bytes / 1024.0).ToString(CultureInfo.InvariantCulture)} KB / 32 MB" : "— / 32 MB";
        _estimateBar.Value = 100.0 * bytes / capacity;
    }

    public static long NextPowerOfTwo(long n)
    {
        long p = 1;
        while (p < n) p <<= 1;
        return p;
    }

    /// <summary>Pads a ROM with 0xFF up to the next power of two (what flash carts and some emulators expect).</summary>
    public static byte[] PadRom(byte[] rom)
    {
        var size = (int)NextPowerOfTwo(rom.Length);
        if (size == rom.Length) return rom;
        var r = new byte[size];
        Array.Fill(r, (byte)0xFF);
        rom.CopyTo(r, 0);
        return r;
    }

    /// <summary>Checks the fields; the problems are shown as help text under them.</summary>
    public bool Validate()
    {
        var ok = true;
        void Check(TextField f, bool valid, string help) { f.Help = valid ? null : help; ok &= valid; }
        var name = (FileName.Text ?? "").Trim();
        Check(FileName, name.Length > 0 && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0, "Enter a file name.");
        Check(RomTitle, Regex.IsMatch((RomTitle.Text ?? "").Trim().ToUpperInvariant(), "^[A-Z0-9 _]{1,12}$"), "Up to 12 letters, digits or spaces.");
        Check(GameCode, Regex.IsMatch((GameCode.Text ?? "").Trim().ToUpperInvariant(), "^[A-Z0-9]{4}$"), "4 letters or digits.");
        Check(Version, Regex.IsMatch((Version.Text ?? "").Trim(), @"^\d+\.\d+\.\d+$"), "major.minor.patch, e.g. 1.0.3");
        if (string.IsNullOrWhiteSpace(Folder.Selected)) { Folder.Help = "Choose a folder."; ok = false; } else Folder.Help = null;
        return ok;
    }

    public void Export()
    {
        if (!Validate() || _s.Project.Current is null) return;
        var settings = _s.ProjectSettings.Settings;
        settings.General.RomTitle = RomTitle.Text!.Trim().ToUpperInvariant();
        settings.General.GameCode = GameCode.Text!.Trim().ToUpperInvariant();
        settings.General.Version = Version.Text!.Trim();
        settings.Build.CompressGraphics = Compress.IsChecked == true;
        settings.Build.StripDebugLog = Strip.IsChecked == true;
        _s.ProjectSettings.Save();
        var name = FileName.Text!.Trim();
        if (!name.EndsWith(".gba", StringComparison.OrdinalIgnoreCase)) name += ".gba";
        Request = new BuildRequest(BuildConfiguration.Release, Path.Combine(Folder.Selected!, name), Pad.IsChecked == true);
        Close(Request);
        _ = _s.Windows.Open("T-D06", Request, source: "ExportRom");
    }
}
