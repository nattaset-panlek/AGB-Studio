using System.Globalization;
using AgbStudio.Build;
using AgbStudio.Build.Pipeline;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Graphics;
using AgbStudio.Editor.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace AgbStudio.Editor.Windows.Build;

/// <summary>
/// Flash to Cartridge (T-D07, 560 x 368; Build › Flash to Cartridge…, Build Report › Flash to cartridge). Per D-12:
/// device detection, cartridge info, "Back up the save file first", "Verify after writing" and Writing x / y KB
/// progress (P11.60). Devices come from <see cref="ICartridgeDeviceProvider"/> (a fake one in tests).
/// </summary>
[EditorWindow("T-D07")]
public sealed class FlashDialog : FigmaDialog
{
    readonly EditorServices _s;
    readonly Icon _infoIcon = new() { Glyph = "chip", Size = 20, VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _infoTitle = UI.Text("", "Body/12 Semibold", "text/primary"), _infoDetail = UI.Text("", "Label/11", "text/muted");
    readonly TextBlock _stage = UI.Text("Writing", "Label/11", "text/secondary"), _progressText = UI.Text("", "Mono/10", "text/secondary");
    readonly AgbProgress _bar = new() { Status = ProgressStatus.Accent, VerticalAlignment = VerticalAlignment.Center, Name = "FlashProgress" };
    readonly DialogFooter _footer;
    CancellationTokenSource? _cts;

    public SelectField Device { get; } = new() { Label = "Device", Name = "FlashDevice" };
    public CheckBox Backup { get; } = new() { Content = "Back up the save file first", IsChecked = true, Name = "OptBackup" };
    public CheckBox Verify { get; } = new() { Content = "Verify after writing", IsChecked = true, Name = "OptVerify" };
    public IReadOnlyList<ICartridgeDevice> Devices { get; private set; } = [];
    public ICartridgeDevice? Selected { get; private set; }
    public CartridgeStatus? Status { get; private set; }
    public string? RomPath { get; }
    public string? BackupPath { get; private set; }
    public bool IsFlashing => _cts != null;
    public string ProgressText => _progressText.Text ?? "";
    public string StageText => _stage.Text ?? "";
    public string InfoTitle => _infoTitle.Text ?? "";
    public Task Detection { get; private set; } = Task.CompletedTask;

    public FlashDialog(WindowContext ctx) : this(ctx.Services) { }

    public FlashDialog(EditorServices s, ICartridgeDeviceProvider? provider = null)
    {
        _s = s;
        Name = "FlashDialog";
        RomPath = FindRom(s);
        provider ??= s.GetService(typeof(ICartridgeDeviceProvider)) as ICartridgeDeviceProvider ?? new SystemCartridgeDevices();
        Devices = provider.Find();
        Device.Options = Devices.Select(d => d.Name).ToList();
        Device.TemplateApplied += (_, e) =>
        {
            if (e.NameScope.Find<Button>("PART_Box") is not { } b) return;
            var flyout = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedLeft };
            foreach (var d in Devices)
            {
                var dev = d;
                var item = new MenuItem { Header = d.Name };
                item.Click += (_, _) => SelectDevice(dev);
                flyout.Items.Add(item);
            }
            b.Flyout = flyout;
        };
        _infoIcon.Token(Icon.ForegroundProperty, "text/muted");
        var info = new DockPanel { HorizontalSpacing = 12 };
        DockPanel.SetDock(_infoIcon, Dock.Left);
        info.Children.AddRange([_infoIcon, UI.Column(3, _infoTitle, _infoDetail)]);
        var infoBox = UI.Frame(info, "bg/base", pad: new Thickness(12, 10), radius: 8).Named("CartridgeInfo");
        infoBox.Height = 56;
        var progress = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("70,12,*,16,70"), Margin = new Thickness(0, 2, 0, 0) };
        _progressText.HorizontalAlignment = HorizontalAlignment.Right;
        _progressText.TextAlignment = Avalonia.Media.TextAlignment.Right;
        Grid.SetColumn(_bar, 2);
        Grid.SetColumn(_progressText, 4);
        progress.Children.AddRange([_stage, _bar, _progressText]);
        var content = UI.Column(14, Device, infoBox, UI.Column(15, Backup, Verify), progress);
        content.Margin = new Thickness(20, 20, 20, 16);
        _footer = new DialogFooter { CancelText = "Cancel", ConfirmText = "Flash", Hint = "Keep the cartridge connected until it finishes." };
        _footer.Cancel += (_, _) => { if (_cts != null) _cts.Cancel(); else Close(null); };
        _footer.Confirm += (_, _) => _ = FlashAsync();
        _footer.TemplateApplied += (_, e) => { _confirm = e.NameScope.Find<AgbButton>("PART_Confirm"); UpdateButtons(); };
        Build("Flash to Cartridge", "chip", content, _footer, 560);
        SetProgress("Writing", 0, RomBytes);
        if (Devices.Count > 0) SelectDevice(Devices[0]);
        else ShowStatus(new CartridgeStatus(false, "No device found", "Connect a GBxCart RW or insert a flash cart's SD card"));
    }

    long RomBytes => RomPath != null && File.Exists(RomPath) ? new FileInfo(RomPath).Length : 0;

    /// <summary>The ROM of the last build (report), else the path the next build writes.</summary>
    static string? FindRom(EditorServices s)
    {
        var p = s.Project.Current;
        if (p is null) return null;
        if (s.Build.LoadReport() is { Rom.Length: > 0 } r && File.Exists(Path.Combine(p.BuildDir, r.Rom))) return Path.Combine(p.BuildDir, r.Rom);
        return new GameBuilder(p, new Toolchain()).RomPath;
    }

    public void SelectDevice(ICartridgeDevice d)
    {
        Selected = d;
        Device.Selected = d.Name;
        ShowStatus(new CartridgeStatus(false, "Looking for a cartridge…", d.Name));
        Detection = DetectAsync(d);
    }

    async Task DetectAsync(ICartridgeDevice d)
    {
        CartridgeStatus status;
        try { status = await d.DetectAsync(CancellationToken.None); }
        catch (Exception e) when (e is IOException or NotSupportedException or UnauthorizedAccessException) { status = new CartridgeStatus(false, "Device error", e.Message); }
        if (Selected == d) ShowStatus(status);
    }

    void ShowStatus(CartridgeStatus status)
    {
        Status = status;
        _infoTitle.Text = status.Line(_s.ProjectSettings.Settings.Save.Type);
        var detail = status.Detail;
        if (status.CurrentTitle is { Length: > 0 } t)
        {
            var g = _s.ProjectSettings.Settings.General;
            detail = t == g.RomTitle && status.Detail.Contains(" rev ", StringComparison.Ordinal)
                ? $"Current game: {t} {string.Join('.', g.Version.Split('.').SkipLast(1))}.{status.Detail[(status.Detail.LastIndexOf(' ') + 1)..]}"
                : status.Detail;
        }
        _infoDetail.Text = detail;
        _infoIcon.Token(Icon.ForegroundProperty, status.Detected ? "status/success" : "text/muted");
        UpdateButtons();
    }

    AgbButton? _confirm;

    /// <summary>Flash is enabled when a device has a cartridge (or card) and the ROM exists.</summary>
    public bool CanFlash => _cts is null && Selected != null && Status?.Detected == true && RomBytes > 0;

    void UpdateButtons()
    {
        if (_confirm != null) _confirm.IsEnabled = CanFlash;
    }

    /// <summary>Progress of one stage; reports that arrive after the next stage started are dropped.</summary>
    Progress<long> Reporter(string stage, long total) => new(n => { if (_stage.Text == stage) SetProgress(stage, n, total); });

    void SetProgress(string stage, long done, long total)
    {
        _stage.Text = stage;
        _bar.Maximum = Math.Max(1, total);
        _bar.Value = done;
        _progressText.Text = $"{(done / 1024).ToString(CultureInfo.InvariantCulture)} / {(total / 1024).ToString(CultureInfo.InvariantCulture)} KB";
    }

    /// <summary>Backs up the save (if asked), writes the ROM with progress, then verifies it (if asked).</summary>
    public async Task<bool> FlashAsync()
    {
        if (_cts != null || Selected is not { } d || RomPath is null || !File.Exists(RomPath) || Status?.Detected != true) return false;
        _cts = new CancellationTokenSource();
        UpdateButtons();
        var rom = await File.ReadAllBytesAsync(RomPath);
        var name = Path.GetFileName(RomPath);
        try
        {
            if (Backup.IsChecked == true)
            {
                SetProgress("Backing up", 0, 0);
                var save = await d.ReadSaveAsync(name, new Progress<long>(n => { if (_stage.Text == "Backing up") SetProgress("Backing up", n, n); }), _cts.Token);
                if (save != null && _s.Project.Current is { } p)
                {
                    var dir = Path.Combine(p.BuildDir, "save-backups");
                    Directory.CreateDirectory(dir);
                    BackupPath = Path.Combine(dir, $"{Path.GetFileNameWithoutExtension(name)}-{DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.sav");
                    await File.WriteAllBytesAsync(BackupPath, save, _cts.Token);
                }
            }
            SetProgress("Writing", 0, rom.Length);
            await d.WriteRomAsync(name, rom, Reporter("Writing", rom.Length), _cts.Token);
            SetProgress("Writing", rom.Length, rom.Length);
            if (Verify.IsChecked == true)
            {
                SetProgress("Verifying", 0, rom.Length);
                if (!await d.VerifyAsync(name, rom, Reporter("Verifying", rom.Length), _cts.Token))
                {
                    _s.Toasts.Show("Verify failed: the cartridge does not match the ROM. Flash again.", ToastStatus.Error);
                    return false;
                }
            }
            _s.Toasts.Show($"Flashed {name} · {PageFrame.Kb(rom.Length)}");
            _cts = null;
            Close(true);
            return true;
        }
        catch (OperationCanceledException)
        {
            _s.Toasts.Show("Flashing cancelled. Flash again before playing the cartridge.", ToastStatus.Error);
            return false;
        }
        catch (Exception e) when (e is IOException or NotSupportedException or UnauthorizedAccessException)
        {
            _s.Toasts.Show(e.Message, ToastStatus.Error);
            return false;
        }
        finally
        {
            _cts = null;
            UpdateButtons();
        }
    }
}
