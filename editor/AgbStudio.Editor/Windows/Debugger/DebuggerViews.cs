using System.Globalization;
using System.Text;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Graphics;
using AgbStudio.Editor.Shell;
using AgbStudio.Emu;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace AgbStudio.Editor.Windows.Debugger;

/// <summary>Debugger "VRAM tiles" (P13.02) and "OAM sprites" (P13.03): BG char block tiles, OBJ graphics and the OAM table.</summary>
public sealed class VramOamView : StackPanel
{
    readonly DebuggerModel _m;
    readonly TextBlock _blocks = UI.Text("", "Label/11", "text/muted");
    readonly TextBlock _bgCaption = DebugUI.Caption("BG char block 0");
    readonly PixelImage _bg = new() { Name = "BgTiles", Scale = 2 };
    readonly WrapPanel _obj = new() { Name = "ObjTiles", Orientation = Orientation.Horizontal, ItemWidth = 70, ItemHeight = 50 };
    readonly TextBlock _oamTitle = UI.Text("", "Title/14", "text/primary");
    readonly StackPanel _rows = new() { Name = "OamRows" };
    static readonly (string Header, double Width)[] Columns = [("#", 40), ("Actor", 130), ("X", 60), ("Y", 60), ("Size", 80), ("Tile", 60), ("Pal", 50), ("Prio", 50), ("Flags", 110)];

    public int SelectedRow { get; set; }
    public IReadOnlyList<(OamEntry Entry, string Actor)> Rows { get; private set; } = [];
    readonly Control[] _tiles;

    /// <summary>VRAM tiles view: the character blocks above the OAM table; OAM sprites view: the table alone.</summary>
    public bool ShowTiles
    {
        get => _tiles[0].IsVisible;
        set
        {
            foreach (var c in _tiles) c.IsVisible = value;
            _oamTitle.Margin = new Thickness(0, value ? 24 : 0, 0, 10);
        }
    }

    public VramOamView(DebuggerModel m)
    {
        _m = m;
        Name = "VramOamView";
        Spacing = 0;
        Margin = new Thickness(16, 14, 16, 16);
        var title = UI.Row(10, UI.Text("VRAM · character blocks", "Title/14", "text/primary"), _blocks);
        title.Margin = new Thickness(0, 0, 0, 10);
        Children.Add(title);
        var bgBox = TileBox(_bg);
        var objBox = TileBox(new Border { Padding = new Thickness(6, 4), Child = _obj });
        var boxes = UI.Row(16, UI.Column(6, _bgCaption, bgBox), UI.Column(6, DebugUI.Caption("OBJ tiles"), objBox));
        Children.Add(boxes);
        _tiles = [title, boxes];
        _oamTitle.Margin = new Thickness(0, 24, 0, 10);
        Children.Add(_oamTitle);
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (h, w) in Columns) header.Children.Add(new TableCell { Kind = TableCellKind.Header, Text = h, Width = w });
        Children.Add(new StackPanel { Width = 640, HorizontalAlignment = HorizontalAlignment.Left, Children = { header, _rows } });
    }

    static Border TileBox(Control content)
    {
        var b = UI.Frame(content, "bg/canvas", "border/default", new Thickness(1), new Thickness(8), 6);
        b.Width = 300;
        b.Height = 150;
        b.ClipToBounds = true;
        return b;
    }

    public void Refresh()
    {
        var core = _m.Play.Core;
        if (core == null) { _rows.Children.Clear(); _oamTitle.Text = "OAM · 0 / 128 in use"; return; }
        try
        {
            var (_, bgOn, _) = Inspector.Display(core);
            var blocks = new SortedSet<int>();
            for (var n = 0; n < 4; n++) if (bgOn[n]) blocks.Add((core.ReadU16(GbaMemory.Io + 8 + (uint)n * 2) >> 2) & 3);
            var objHigh = Rows.Any(r => r.Entry.Tile >= 512);
            _blocks.Text = $"BG: block {Range(blocks)} · OBJ: block {(objHigh ? "4–5" : "4")}";
            var first = blocks.Count > 0 ? blocks.Min : 0;
            _bgCaption.Text = $"BG char block {first}";
            // redraw only what changed since the last frame (the view refreshes every frame)
            var pal = core.Memory(GbaMemory.Palette, GbaMemory.PaletteSize);
            var bgKey = Hash(core.Memory(GbaMemory.Vram + (uint)first * 0x4000, 128 * 32), Hash(pal, (ulong)first));
            if (bgKey != _bgKey) { _bgKey = bgKey; RenderBg(core, first); }
            Rows = _m.OamRows();
            var objKey = Hash(core.Memory(GbaMemory.Vram + 0x10000, 0x8000), Hash(pal, (ulong)string.Join(",", Rows.Select(r => $"{r.Entry.Tile}:{r.Entry.Palette}:{r.Entry.Width}x{r.Entry.Height}")).GetHashCode()));
            if (objKey != _objKey) { _objKey = objKey; RenderObj(core); }
            _oamTitle.Text = $"OAM · {Rows.Count} / 128 in use";
            RenderRows();
        }
        catch (ArgumentOutOfRangeException) { }
    }

    ulong _bgKey, _objKey;
    string _rowsKey = "";

    /// <summary>FNV-1a over a span, chained with <paramref name="seed"/>.</summary>
    static ulong Hash(ReadOnlySpan<byte> data, ulong seed = 14695981039346656037)
    {
        var h = seed;
        foreach (var b in data) { h ^= b; h *= 1099511628211; }
        return h;
    }

    static string Range(SortedSet<int> s) => s.Count == 0 ? "—" : s.Count == 1 ? s.Min.ToString(CultureInfo.InvariantCulture) : $"{s.Min}–{s.Max}";

    /// <summary>The first 128 tiles of the char block, each in the palette bank the BG screen entries use it with.</summary>
    void RenderBg(LibretroCore core, int block)
    {
        const int perRow = 16, count = 128, pitch = 9;
        var pal = Inspector.PaletteRam(core);
        var bank = new int[1024];
        for (var n = 0; n < 4; n++)
        {
            var cnt = core.ReadU16(GbaMemory.Io + 8 + (uint)n * 2);
            if (((cnt >> 2) & 3) != block) continue;
            var sbb = (uint)((cnt >> 8) & 31);
            var map = core.Memory(GbaMemory.Vram + sbb * 0x800, 2048);
            for (var i = 0; i < 1024; i++)
            {
                var e = map[i * 2] | (map[i * 2 + 1] << 8);
                bank[e & 0x3FF] = e >> 12;
            }
        }
        var tiles = core.Memory(GbaMemory.Vram + (uint)block * 0x4000, count * 32);
        var w = perRow * pitch - 1;
        var h = count / perRow * pitch - 1;
        var px = new uint[w * h];
        for (var t = 0; t < count; t++)
            for (var y = 0; y < 8; y++)
                for (var x = 0; x < 8; x++)
                {
                    var b = tiles[t * 32 + y * 4 + x / 2];
                    var idx = (x & 1) == 0 ? b & 15 : b >> 4;
                    if (idx == 0) continue;
                    px[(t / perRow * pitch + y) * w + t % perRow * pitch + x] = Inspector.ToArgb(pal[bank[t] * 16 + idx]);
                }
        _bg.SetPixels(px, w, h);
    }

    /// <summary>Each distinct sprite graphic in OBJ VRAM (1D mapping) assembled at its OAM size, in its palette.</summary>
    void RenderObj(LibretroCore core)
    {
        var pal = Inspector.PaletteRam(core);
        var seen = new HashSet<int>();
        var images = new List<PixelImage>();
        foreach (var (e, _) in Rows)
        {
            if (!seen.Add(e.Tile) || images.Count >= 8) continue;
            var tw = e.Width / 8;
            var th = e.Height / 8;
            var bytes = core.Memory(GbaMemory.Vram + 0x10000 + (uint)e.Tile * 32, Math.Min(tw * th * 32, 0x8000 - e.Tile * 32));
            var px = new uint[e.Width * e.Height];
            for (var t = 0; t < tw * th && (t + 1) * 32 <= bytes.Length; t++)
                for (var y = 0; y < 8; y++)
                    for (var x = 0; x < 8; x++)
                    {
                        var b = bytes[t * 32 + y * 4 + x / 2];
                        var idx = (x & 1) == 0 ? b & 15 : b >> 4;
                        if (idx == 0) continue;
                        px[(t / tw * 8 + y) * e.Width + t % tw * 8 + x] = Inspector.ToArgb(pal[256 + e.Palette * 16 + idx]);
                    }
            var img = new PixelImage { Scale = e.Width > 16 || e.Height > 16 ? 1 : 2, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            img.SetPixels(px, e.Width, e.Height);
            images.Add(img);
        }
        _obj.Children.Clear();
        foreach (var i in images) _obj.Children.Add(i);
    }

    void RenderRows()
    {
        var all = Rows.Take(64).Select(x => new string[]
        {
            x.Entry.Index.ToString(CultureInfo.InvariantCulture), x.Actor, x.Entry.X.ToString(CultureInfo.InvariantCulture), x.Entry.Y.ToString(CultureInfo.InvariantCulture),
            $"{x.Entry.Width}×{x.Entry.Height}", x.Entry.Tile.ToString(CultureInfo.InvariantCulture), x.Entry.Palette.ToString(CultureInfo.InvariantCulture),
            x.Entry.Priority.ToString(CultureInfo.InvariantCulture), x.Entry.Flags.Length == 0 ? "—" : x.Entry.Flags,
        }).ToList();
        var key = SelectedRow + "|" + string.Join("|", all.Select(v => string.Join(",", v)));
        if (key == _rowsKey) return;
        _rowsKey = key;
        _rows.Children.Clear();
        for (var r = 0; r < all.Count; r++)
        {
            var values = all[r];
            var selected = r == SelectedRow;
            var row = new StackPanel { Orientation = Orientation.Horizontal, Name = "OamRow_" + r, Background = Brushes.Transparent };
            for (var c = 0; c < values.Length; c++)
                row.Children.Add(new TableCell { Text = values[c], Width = Columns[c].Width, Height = 30, Kind = selected ? TableCellKind.Selected : c >= 2 ? TableCellKind.Mono : TableCellKind.Default });
            var index = r;
            row.PointerPressed += (_, _) => { SelectedRow = index; RenderRows(); };
            _rows.Children.Add(row);
        }
    }
}

/// <summary>Right panel of the Debugger (T-P19): frame graph, this frame's Logic / Render / Audio / Idle, memory and the C# call stack (P13.01, P13.06).</summary>
public sealed class ProfilerPanel : StackPanel
{
    readonly DebuggerModel _m;
    readonly FrameGraph _graph = new() { Height = 88, Name = "FrameGraph" };
    readonly Meter _logic = new() { Label = "Logic", Status = ProgressStatus.Accent, Name = "Meter_Logic" };
    readonly Meter _render = new() { Label = "Render", Status = ProgressStatus.Accent, Name = "Meter_Render" };
    readonly Meter _audio = new() { Label = "Audio", Status = ProgressStatus.Accent, Name = "Meter_Audio" };
    readonly Meter _idle = new() { Label = "Idle", Name = "Meter_Idle" };
    readonly Meter _ewram = new() { Label = "EWRAM", Name = "Meter_EWRAM" };
    readonly Meter _iwram = new() { Label = "IWRAM", Name = "Meter_IWRAM" };
    readonly Meter _vram = new() { Label = "VRAM", Name = "Meter_VRAM" };
    readonly Meter _oam = new() { Label = "OAM", Name = "Meter_OAM" };
    readonly StackPanel _stack = new() { Spacing = 6, Name = "CallStack" };
    int _memCountdown;

    public ProfilerPanel(DebuggerModel m)
    {
        _m = m;
        Name = "ProfilerPanel";
        var intro = UI.Column(10, UI.Text("Frame time · last 120 frames · 16.7 ms budget", "Label/11", "text/muted"), _graph);
        Children.Add(UI.Frame(intro, null, "border/default", new Thickness(0, 0, 0, 1), new Thickness(12, 11, 12, 12)));
        Children.Add(DebugUI.Section("This frame", "clock", UI.Column(6, _logic, _render, _audio, _idle), "ThisFrame"));
        Children.Add(DebugUI.Section("Memory", "chip", UI.Column(6, _ewram, _iwram, _vram, _oam), "Memory"));
        Children.Add(DebugUI.Section("Call stack", "code", _stack, "CallStackSection"));
    }

    public IReadOnlyList<string> StackLines => _stack.Children.OfType<TextBlock>().Select(t => t.Text ?? "").ToList();

    public void Refresh()
    {
        var p = _m.Play.Profile;
        if (p != null)
        {
            _graph.SetFrames(p.History.Select(c => ProfilerStats.Ms(c)).ToArray());
            void Set(Meter meter, uint cycles)
            {
                var ms = ProfilerStats.Ms(cycles);
                meter.ValueText = ms.ToString("0.0", CultureInfo.InvariantCulture) + " ms";
                meter.Percent = Math.Clamp(ms / FrameGraph.BudgetMs * 100, 0, 100);
            }
            Set(_logic, p.Logic);
            Set(_render, p.Render);
            Set(_audio, p.Audio);
            Set(_idle, p.Idle);
        }
        if (--_memCountdown <= 0 && _m.Play.Core is { } core)
        {
            _memCountdown = 30;
            BudgetSnapshot b;
            try { b = BudgetService.Live(core, _m.Play.World.SceneName, _m.Play.Elf, _m.Play.RomPath, null); }
            catch (ArgumentOutOfRangeException) { _memCountdown = 0; RefreshStack(); return; }
            void Mem(Meter meter, string label, double total, string unit)
            {
                var line = b.Line(label);
                var used = line?.Used ?? 0;
                meter.ValueText = $"{used} / {total}{unit}";
                meter.Percent = Math.Clamp(used / total * 100, 0, 100);
                meter.Status = used > total ? ProgressStatus.Danger : used / total >= 0.85 ? ProgressStatus.Warn : ProgressStatus.OK;
            }
            Mem(_ewram, "EWRAM", 256, " KB");
            Mem(_iwram, "IWRAM", 32, " KB");
            var vram = (b.Line("VRAM BG")?.Used ?? 0) + (b.Line("VRAM OBJ")?.Used ?? 0);
            _vram.ValueText = $"{vram} / 96 KB";
            _vram.Percent = Math.Clamp(vram / 96 * 100, 0, 100);
            Mem(_oam, "OAM", 128, "");
        }
        RefreshStack();
    }

    void RefreshStack()
    {
        var lines = _m.CallStack.Select(r => r.Text).ToList();
        if (!lines.SequenceEqual(StackLines))
        {
            _stack.Children.Clear();
            foreach (var l in lines) _stack.Children.Add(UI.Text(l, "Mono/11", "text/secondary"));
        }
    }
}

/// <summary>Debugger · Memory (T-P23, P13.05): EWRAM / IWRAM map by category and the hex view of the selected watch.</summary>
public sealed class MemoryView : StackPanel
{
    readonly DebuggerModel _m;
    readonly (TextBlock Title, MemoryBar Bar, WrapPanel Legend)[] _maps = new (TextBlock, MemoryBar, WrapPanel)[2];
    readonly TextBlock _hexTitle = UI.Text("", "Label/11", "text/muted");
    readonly StackPanel _hexRows = new() { Name = "HexRows" };
    int _countdown;

    public string HexText => string.Join("\n", _hexRows.Children.OfType<StackPanel>().Select(r => string.Join(" | ", r.Children.OfType<TableCell>().Select(c => c.Text))));
    public IReadOnlyList<string> LegendTexts => _maps.SelectMany(m => m.Legend.Children.OfType<StackPanel>().Select(s => s.Children.OfType<TextBlock>().First().Text ?? "")).ToList();

    public MemoryView(DebuggerModel m)
    {
        _m = m;
        Name = "MemoryView";
        Margin = new Thickness(16, 14, 4, 16);
        Children.Add(UI.Row(10, UI.Text("Memory map", "Title/14", "text/primary"), UI.Text("where the 288 KB of work RAM goes", "Label/11", "text/muted")));
        for (var i = 0; i < 2; i++)
        {
            var title = UI.Text("", "Mono/10", "text/muted");
            var bar = new MemoryBar { Height = 20, Name = i == 0 ? "EwramBar" : "IwramBar" };
            var legend = new WrapPanel { Orientation = Orientation.Horizontal };
            _maps[i] = (title, bar, legend);
            var heading = UI.Row(8, UI.Text(i == 0 ? "EWRAM" : "IWRAM", "Body/12 Semibold", "text/primary"), title);
            var block = UI.Column(6, heading, bar, legend);
            legend.Margin = new Thickness(0, 2, 0, 0);
            block.Margin = new Thickness(0, 10, 0, 0);
            Children.Add(block);
        }
        var hexTitle = UI.Row(10, UI.Text("Hex view", "Title/14", "text/primary"), _hexTitle);
        hexTitle.Margin = new Thickness(0, 26, 0, 10);
        Children.Add(hexTitle);
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        header.Children.Add(new TableCell { Kind = TableCellKind.Header, Text = "Address", Width = 130 });
        header.Children.Add(new TableCell { Kind = TableCellKind.Header, Text = "00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F", Width = 470 });
        header.Children.Add(new TableCell { Kind = TableCellKind.Header, Text = "ASCII", Width = 160 });
        Children.Add(new StackPanel { Width = 760, HorizontalAlignment = HorizontalAlignment.Left, Children = { header, _hexRows } });
    }

    public void Refresh(bool force = false)
    {
        RenderHex();
        if (--_countdown > 0 && !force) return;
        _countdown = 30;
        var (ew, iw) = _m.MemoryMap();
        Fill(0, ew, 256 * 1024);
        Fill(1, iw, 32 * 1024);
    }

    void Fill(int i, IReadOnlyList<MemorySegment> segs, long total)
    {
        var used = segs.Sum(s => s.Bytes);
        _maps[i].Title.Text = $"{total / 1024} KB · {Kb(used)} KB used";
        _maps[i].Bar.Set(segs.Select(s => (s.Bytes, s.Token)).ToList(), total);
        var legend = _maps[i].Legend;
        legend.Children.Clear();
        foreach (var s in segs.Append(new MemorySegment("Free", Math.Max(0, total - used), "bg/hover")))
        {
            var dot = new Rectangle { Width = 9, Height = 9, RadiusX = 2, RadiusY = 2, VerticalAlignment = VerticalAlignment.Center };
            DebugUI.Fill(dot, s.Token);
            var item = UI.Row(6, dot, UI.Text($"{s.Category} · {Kb(s.Bytes)} KB", "Label/11", "text/secondary"));
            item.Margin = new Thickness(0, 0, 16, 0);
            legend.Children.Add(item);
        }
    }

    static long Kb(long bytes) => (bytes + 1023) / 1024;

    string _hexKey = "";

    void RenderHex()
    {
        var watches = _m.Watches;
        var sel = watches.Count == 0 ? null : watches[Math.Clamp(_m.SelectedWatch, 0, watches.Count - 1)];
        var core = _m.Play.Core;
        if (sel == null || core == null || sel.Address == 0) { _hexTitle.Text = ""; _hexRows.Children.Clear(); _hexKey = ""; return; }
        var start = sel.Address & ~15u;
        _hexTitle.Text = $"0x{start >> 16:X4}_{start & 0xFFFF:X4}" + (sel.Owner is { } o ? " · " + o : "");
        var lines = new List<(uint Address, string Hex, string Ascii)>();
        for (var l = 0; l < 5; l++)
        {
            var a = start + (uint)l * 16;
            Span<byte> row;
            try { row = core.Memory(a, 16); }
            catch (ArgumentOutOfRangeException) { break; }
            var hex = new StringBuilder();
            var ascii = new StringBuilder();
            foreach (var b in row)
            {
                if (hex.Length > 0) hex.Append(' ');
                hex.Append(b.ToString("X2", CultureInfo.InvariantCulture));
                ascii.Append(b is >= 32 and < 127 ? (char)b : '.');
            }
            lines.Add((a, hex.ToString(), ascii.ToString()));
        }
        var key = string.Join("|", lines);
        if (key == _hexKey) return;
        _hexKey = key;
        _hexRows.Children.Clear();
        for (var l = 0; l < lines.Count; l++)
        {
            var (a, hex, ascii) = lines[l];
            var kind = l == 0 ? TableCellKind.Selected : TableCellKind.Mono;
            var r = new StackPanel { Orientation = Orientation.Horizontal, Name = "HexRow_" + l };
            r.Children.Add(new TableCell { Text = $"{a >> 16:X4} {a & 0xFFFF:X4}", Width = 130, Height = 28, Kind = kind });
            r.Children.Add(new TableCell { Text = hex.ToString(), Width = 470, Height = 28, Kind = kind });
            r.Children.Add(new TableCell { Text = ascii.ToString(), Width = 160, Height = 28, Kind = l == 0 ? TableCellKind.Default : TableCellKind.Mono });
            _hexRows.Children.Add(r);
        }
    }
}

/// <summary>Watch memory (T-P23 right panel): watches by name and address with live values, and Add watch.</summary>
public sealed class WatchPanel : StackPanel
{
    readonly DebuggerModel _m;
    readonly StackPanel _list = new() { Spacing = 4, Name = "WatchList" };
    readonly TextBox _add = new() { Watermark = "player.position or 0x0200 1A40", IsVisible = false, Name = "AddWatchBox" };

    public AgbButton AddButton { get; } = new() { Content = "Add watch", Glyph = "plus", Kind = ButtonKind.Ghost, Size = ButtonSize.SM, Name = "AddWatchButton", HorizontalAlignment = HorizontalAlignment.Left };
    public IReadOnlyList<string> Values => _list.Children.OfType<Border>().Select(b => b.Tag as string ?? "").ToList();

    public WatchPanel(DebuggerModel m)
    {
        _m = m;
        Name = "WatchPanel";
        Margin = new Thickness(12, 12, 12, 12);
        Spacing = 4;   // Figma T-P23: Add watch 4 px under the last watch card
        Children.Add(_list);
        Children.Add(_add);
        Children.Add(AddButton);
        AddButton.Click += (_, _) => { _add.IsVisible = true; _add.Focus(); };
        _add.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { if (_m.AddWatch(_add.Text ?? "")) { _add.Text = ""; _add.IsVisible = false; } e.Handled = true; }
            else if (e.Key == Key.Escape) { _add.IsVisible = false; e.Handled = true; }
        };
    }

    public void Refresh()
    {
        var watches = _m.Watches;
        while (_list.Children.Count > watches.Count) _list.Children.RemoveAt(_list.Children.Count - 1);
        for (var i = 0; i < watches.Count; i++)
        {
            var w = watches[i];
            if (i >= _list.Children.Count) _list.Children.Add(Card(i));
            var card = (Border)_list.Children[i];
            var grid = (DockPanel)card.Child!;
            ((TextBlock)grid.Children[0]).Text = w.Value;
            var col = (StackPanel)grid.Children[1];
            ((TextBlock)col.Children[0]).Text = w.Name;
            ((TextBlock)col.Children[1]).Text = w.Address == 0 ? "" : DebuggerModel.Address(w.Address);
            card.Tag = $"{w.Name}={w.Value}";
        }
    }

    Border Card(int index)
    {
        var value = UI.Text("", "Mono/11", "text/primary");
        value.VerticalAlignment = VerticalAlignment.Top;
        var col = UI.Column(2, UI.Text("", "Mono/11", "accent/default"), UI.Text("", "Mono/10", "text/muted"));
        var dock = new DockPanel();
        DockPanel.SetDock(value, Avalonia.Controls.Dock.Right);
        dock.Children.Add(value);
        dock.Children.Add(col);
        var card = UI.Frame(dock, "bg/raised", null, null, new Thickness(8, 8, 8, 8), 6).Named("Watch_" + index);
        card.PointerPressed += (_, _) => { _m.SelectedWatch = index; _m.Play.Publish(); };
        return card;
    }
}

/// <summary>Debugger · Palettes (T-P24, P13.04): live palette RAM, 16 BG and 16 OBJ banks; a white outline marks colours changed this frame.</summary>
public sealed class PalettesView : StackPanel
{
    readonly DebuggerModel _m;
    readonly Border[] _cells = new Border[512];
    readonly int[] _state = Enumerable.Repeat(-1, 512).ToArray();

    public PalettesView(DebuggerModel m)
    {
        _m = m;
        Name = "PalettesView";
        Margin = new Thickness(16, 14, 16, 16);
        var title = UI.Row(10, UI.Text("Palette RAM · live", "Title/14", "text/primary"), UI.Text("white outline = changed this frame", "Label/11", "text/muted"));
        title.Margin = new Thickness(0, 0, 0, 10);
        Children.Add(title);
        Children.Add(UI.Row(32, Grid(0, "BG palettes · 0x0500 0000"), Grid(256, "OBJ palettes · 0x0500 0200")));
        ActualThemeVariantChanged += (_, _) => Array.Fill(_state, -1);
    }

    public Border Cell(int index) => _cells[index];

    Control Grid(int start, string caption)
    {
        var col = new StackPanel { Spacing = 3 };
        var head = UI.Text(caption, "Overline/11", "text/secondary");
        head.Margin = new Thickness(0, 0, 0, 3);
        col.Children.Add(head);
        for (var r = 0; r < 16; r++)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 };
            var label = UI.Text(r.ToString(CultureInfo.InvariantCulture), "Mono/10", "text/muted", 12);
            label.TextAlignment = TextAlignment.Right;
            row.Children.Add(label);
            for (var c = 0; c < 16; c++)
            {
                var index = start + r * 16 + c;
                var cell = new Border { Width = 18, Height = 18, CornerRadius = new CornerRadius(3), BorderThickness = new Thickness(0), Name = "Pal_" + index, Background = Brushes.Transparent };
                cell.PointerPressed += (_, _) => { _m.SelectedColour = index; _m.Play.Publish(); };
                _cells[index] = cell;
                row.Children.Add(cell);
            }
            col.Children.Add(row);
        }
        return col;
    }

    public void Refresh()
    {
        var pal = _m.Play.Palette;
        var empty = DebugUI.Brush(this, "bg/raised");
        var white = DebugUI.Brush(this, "lit/white");
        var sel = DebugUI.Brush(this, "accent/default");
        for (var i = 0; i < 512; i++)
        {
            var c = pal[i];
            var changed = _m.ChangedThisFrame[i];
            var state = c | (changed ? 1 << 16 : 0) | (i == _m.SelectedColour ? 1 << 17 : 0) | (_m.Play.Core != null ? 1 << 18 : 0);
            if (_state[i] == state) continue;  // only cells that changed get new brushes
            _state[i] = state;
            var used = c != 0 || (i % 16 == 0 && i % 256 == 0 && _m.Play.Core != null);
            _cells[i].Background = used ? new SolidColorBrush(Color.FromUInt32(Inspector.ToArgb(c))) : empty;
            _cells[i].BorderBrush = changed ? white : i == _m.SelectedColour ? sel : null;
            _cells[i].BorderThickness = new Thickness(changed || i == _m.SelectedColour ? 1.5 : 0);
        }
    }
}

/// <summary>Colour panel (T-P24 right): selected entry, address, BGR555 value and RGB; used by; Open in Palette Manager and Break when this colour changes.</summary>
public sealed class ColourPanel : StackPanel
{
    readonly DebuggerModel _m;
    readonly SectionHeader _title = new() { Glyph = "pipette", Name = "Section_Colour" };
    readonly Border _swatch = new() { Width = 50, Height = 50, CornerRadius = new CornerRadius(6), Name = "ColourSwatch" };
    readonly TextBlock _address = UI.Text("", "Mono/11", "text/primary");
    readonly TextBlock _value = UI.Text("", "Mono/11", "text/primary");
    readonly TextBlock _rgb = UI.Text("", "Mono/11", "text/primary");
    readonly StackPanel _usedBy = new() { Spacing = 12, Name = "UsedBy" };
    int _usedByFor = -1;
    long _usedByFrame = -1;
    IReadOnlyList<string> _usedByCache = [];

    public AgbButton OpenPaletteManager { get; } = new() { Content = "Open in Palette Manager", Glyph = "sliders", Size = ButtonSize.SM, Name = "OpenPaletteManager", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left };
    public AgbButton BreakOnChange { get; } = new() { Content = "Break when this colour changes", Glyph = "bug", Size = ButtonSize.SM, Name = "BreakOnColour", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left };
    public IReadOnlyList<string> UsedByLines => _usedBy.Children.OfType<StackPanel>().Select(s => s.Children.OfType<TextBlock>().First().Text ?? "").ToList();
    public string Title => _title.Title ?? "";
    public string ValueText => _value.Text ?? "";
    public string AddressText => _address.Text ?? "";
    public string RgbText => _rgb.Text ?? "";

    public ColourPanel(DebuggerModel m, EditorServices s)
    {
        _m = m;
        Name = "ColourPanel";
        Grid Kv()
        {
            var g = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("68,*"), RowDefinitions = RowDefinitions.Parse("18,18,18") };
            void Add(string k, Control v, int row) { var t = UI.Text(k, "Label/11", "text/muted"); Grid.SetRow(t, row); Grid.SetRow(v, row); Grid.SetColumn(v, 1); g.Children.Add(t); g.Children.Add(v); }
            Add("Address", _address, 0);
            Add("Value", _value, 1);
            Add("RGB", _rgb, 2);
            g.VerticalAlignment = VerticalAlignment.Top;
            g.Margin = new Thickness(0, -2, 0, 0);
            return g;
        }
        _swatch.VerticalAlignment = VerticalAlignment.Top;
        var top = UI.Row(14, _swatch, Kv());
        var colTop = UI.Column(6, _title, top);   // Figma T-P24: 6 px under the header, as the other sections
        Children.Add(UI.Frame(colTop, null, "border/default", new Thickness(0, 0, 0, 1), new Thickness(12, 8, 12, 10)));
        Children.Add(DebugUI.Section("Used by", "link", _usedBy, "UsedBySection", new Thickness(12, 8, 12, 13), gap: 9));
        OpenPaletteManager.Click += (_, _) => _ = s.Windows.Open("T-P10", $"{(_m.SelectedColour < 256 ? "BG" : "OBJ")} {_m.SelectedColour / 16 % 16}", source: "DebuggerPalettes");
        BreakOnChange.Click += (_, _) => { _m.BreakOnColour = _m.BreakOnColour == _m.SelectedColour ? null : _m.SelectedColour; Refresh(); };
        Children.Add(DebugUI.Section("Actions", "spark", UI.Column(6, OpenPaletteManager, BreakOnChange), "Actions", new Thickness(12, 8, 12, 11), gap: 6));
    }

    public void Refresh()
    {
        var i = _m.SelectedColour;
        var c = _m.Play.Palette[i];
        _title.Title = $"{(i < 256 ? "BG" : "OBJ")} {i / 16 % 16} · index {i % 16}";
        _swatch.Background = new SolidColorBrush(Color.FromUInt32(Inspector.ToArgb(c)));
        var addr = GbaMemory.Palette + (uint)i * 2;
        _address.Text = DebuggerModel.Address(addr);
        _value.Text = $"0x{c:X4} (BGR555)";
        _rgb.Text = $"{c & 31}, {(c >> 5) & 31}, {(c >> 10) & 31}";
        // the layers using a bank change only with the scene: read them again every 30 frames or for another colour
        if (_usedByFor != i || _m.Play.Frame / 30 != _usedByFrame)
        {
            _usedByFor = i;
            _usedByFrame = _m.Play.Frame / 30;
            _usedByCache = _m.UsedBy(i);
        }
        var lines = _usedByCache.ToList();
        var last = _m.LastChanged[i];
        lines.Add(last < 0 ? "Not changed while playing" : $"Changed {Math.Max(0, _m.Play.Frame - last)} frames ago");
        if (_m.BreakOnColour == i) lines.Add("Breaks when it changes");
        if (lines.SequenceEqual(UsedByLines)) return;
        _usedBy.Children.Clear();
        foreach (var l in lines)
        {
            var icon = new Icon { Glyph = "dots", Size = 12, VerticalAlignment = VerticalAlignment.Center }.Token(Icon.ForegroundProperty, "text/muted");
            _usedBy.Children.Add(UI.Row(8, icon, UI.Text(l, "Body/12", "text/secondary")));
        }
    }
}
