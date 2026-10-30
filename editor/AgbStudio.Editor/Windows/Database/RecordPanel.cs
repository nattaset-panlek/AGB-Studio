using System.Globalization;
using AgbStudio.Build.Data;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Graphics;
using AgbStudio.Editor.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace AgbStudio.Editor.Windows.Database;

/// <summary>
/// The RECORD panel of the Database (T-P07): the selected row's icon (its first sprite field), name and C# id; the
/// Fields section (one Inspector field per field and per language for localized text; enum as a dropdown, fixed as a
/// slider, bool as a switch); the Description section; and "Used by" (references from other tables, scene actors,
/// scripts and dialogue). Edits are undoable.
/// </summary>
public sealed class RecordPanel : DockPanel
{
    readonly DatabasePage _page;
    readonly StackPanel _body = new() { Name = "RecordBody" };
    readonly Dictionary<string, InspectorField> _fields = new(StringComparer.Ordinal);
    string _shape = "";
    TextBox? _description;

    public RecordPanel(DatabasePage page)
    {
        _page = page;
        Name = "RecordPanel";
        var header = new PanelHeader { Title = "Record", Glyph = "sliders", ActionGlyph = "dots", Name = "RecordHeader" };
        header.ActionCommand = new Relay(() => Dropdown.Show(header, ["Duplicate", "Delete"], null, v =>
        {
            if (v == "Duplicate") page.DuplicateRow();
            else page.DeleteRow();
        }));
        SetDock(header, Dock.Top);
        Children.Add(header);
        Children.Add(new ScrollViewer { Content = _body, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });
    }

    public IReadOnlyDictionary<string, InspectorField> Fields => _fields;
    public TextBox? Description => _description;
    public IEnumerable<ListRow> UsedByRows => _body.GetLogicalDescendantsOfType<ListRow>();

    /// <summary>Languages the record shows localized text in: the source language and every language that has strings.</summary>
    public static List<string> RecordLanguages(ProjectData data) =>
        data.Languages.Select(l => l.Code).Where(c => c == data.Source || data.Strings.TryGetValue(c, out var t) && t.Strings.Count > 0).ToList();

    /// <summary>Label of a field row: the name, or "name · EN" per language for localized text.</summary>
    public static string LabelOf(FieldDef f, string? lang) => lang is null ? f.Name : $"{f.Name} · {lang.ToUpperInvariant()}";

    /// <summary>Fields the Fields section lists: shown in the inspector, not the icon (first sprite), not script code, not the description.</summary>
    public static IEnumerable<FieldDef> InspectorFields(TableFile t)
    {
        var icon = t.Fields.FirstOrDefault(f => f.Type == "sprite");
        return t.Fields.Where(f => f.ShowInInspector && f != icon && f.Type != "script" && !TableGrid.IsDescription(f));
    }

    readonly Reentry _refresh = new();
    int _row;

    public void Refresh() => _refresh.Run(RefreshCore);

    void RefreshCore()
    {
        var data = _page.Data;
        var t = _page.Current?.Table;
        var r = _page.SelectedRow;
        if (data is null || t is null || r < 0 || r >= t.Rows.Count)
        {
            _body.Children.Clear();
            _fields.Clear();
            _shape = "";
            return;
        }
        var langs = RecordLanguages(data);
        var shape = $"{_page.Current!.Path}|{r}|{string.Join(",", t.Fields.Select(f => f.Name + f.Type + f.Localized + f.ShowInInspector + string.Join("/", f.EnumValues)))}|{string.Join(",", langs)}";
        if (shape != _shape) Build(data, t, r, langs);
        _shape = shape;
        Update(data, t, r);
    }

    void Build(ProjectData data, TableFile t, int r, List<string> langs)
    {
        // a focused box commits to the row it was built for while it is removed
        _body.Children.Clear();
        _fields.Clear();
        _description = null;
        _row = r;

        // icon, name and id
        var icon = new PixelImage { MaxScale = 2, Name = "RecordIcon" };
        var thumb = new Border { Width = 52, Height = 52, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), Child = icon }
            .Token(Border.BackgroundProperty, "bg/base").Token(Border.BorderBrushProperty, "border/default");
        if (t.Fields.FirstOrDefault(f => f.Type == "sprite") is { } iconField)
        {
            thumb.Cursor = new Cursor(StandardCursorType.Hand);
            thumb.PointerPressed += (_, _) => Dropdown.Show(thumb, SpriteLibrary.Names(data.Project), t.Rows[_row].GetValueOrDefault(iconField.Name), v => _page.SetCell(_row, iconField, null, v));
        }
        var title = UI.Text("", "Title/14", "text/primary").Named("RecordTitle");
        var sub = UI.Text("", "Label/11", "text/muted").Named("RecordSubtitle");
        var names = UI.Column(4, title, sub);
        names.VerticalAlignment = VerticalAlignment.Center;
        var card = UI.Row(10, thumb, names);
        card.Margin = new Thickness(12, 12, 12, 0);
        _body.Children.Add(card);

        // Fields
        var list = UI.Column(6);
        foreach (var f in InspectorFields(t))
        {
            if (f.Type == "text" && f.Localized)
                foreach (var lang in langs) list.Children.Add(Field(data, t, f, lang));
            else list.Children.Add(Field(data, t, f, null));
        }
        _body.Children.Add(Section("Fields", "table", list, new Thickness(12, 20, 12, 10)));

        if (t.Fields.FirstOrDefault(TableGrid.IsDescription) is { } descField)
        {
            _body.Children.Add(Ui.Rule());
            _description = new TextBox { Name = "DescriptionBox", TextWrapping = TextWrapping.Wrap, AcceptsReturn = false, Height = double.NaN, MinHeight = 38, Padding = new Thickness(11, 10) };
            Ui.CommitOnLeave(_description, () => descField.Localized ? data.Text(data.Source, Row(t).GetValueOrDefault(descField.Name) ?? "") ?? "" : Row(t).GetValueOrDefault(descField.Name) ?? "",
                v => _page.SetCell(_row, descField, descField.Localized ? data.Source : null, v));
            _body.Children.Add(Section("Description", "book", _description, new Thickness(12, 8, 12, 9)));
        }

        _body.Children.Add(Ui.Rule());
        _body.Children.Add(Section("Used by", "link", UI.Column(4).Named("UsedByList"), new Thickness(12, 8, 12, 9)));
        _body.Children.Add(Ui.Rule());
    }

    Dictionary<string, string> Row(TableFile t) => t.Rows[Math.Clamp(_row, 0, t.Rows.Count - 1)];

    static StackPanel Section(string title, string glyph, Control content, Thickness margin)
    {
        var header = new SectionHeader { Title = title, Glyph = glyph, Name = "Section_" + title.Replace(" ", "") };
        var s = UI.Column(6, header, content);
        s.Margin = margin;
        header.PropertyChanged += (_, e) => { if (e.Property == SectionHeader.IsOpenProperty) content.IsVisible = header.IsOpen; };
        return s;
    }

    InspectorField Field(ProjectData data, TableFile t, FieldDef f, string? lang)
    {
        var label = LabelOf(f, lang);
        var kind = f.Type switch { "fixed" => FieldKind.Slider, "bool" => FieldKind.Toggle, _ when _page.OptionsOf(f) != null => FieldKind.Dropdown, _ => FieldKind.Text };
        if (f.Name == t.KeyField) kind = FieldKind.Text;
        var field = new InspectorField { Kind = kind, Label = label, Name = "Field_" + label.Replace(" · ", "_") };
        _fields[label] = field;
        switch (kind)
        {
            case FieldKind.Dropdown:
                Dropdown.Attach(field, () => _page.OptionsOf(f)!, v => _page.SetCell(_row, f, null, v));
                break;
            case FieldKind.Toggle:
                field.PropertyChanged += (_, e) =>
                {
                    if (e.Property != InspectorField.IsOnProperty || _updating) return;
                    var v = field.IsOn ? "yes" : "no";
                    if (TableGrid.IsTrue(Row(t).GetValueOrDefault(f.Name)) != field.IsOn) _page.SetCell(_row, f, null, v);
                };
                break;
            case FieldKind.Slider:
                double? dragFrom = null;
                field.PropertyChanged += (_, e) =>
                {
                    if (e.Property != InspectorField.SliderValueProperty || _updating) return;
                    var v = SliderToFixed(t, f, field.SliderValue);
                    dragFrom ??= ParseFixed(Row(t).GetValueOrDefault(f.Name));
                    field.Value = Format(v);
                };
                field.AddHandler(PointerReleasedEvent, (_, _) =>
                {
                    if (dragFrom is null) return;
                    dragFrom = null;
                    _page.SetCell(_row, f, null, field.Value ?? "");
                }, RoutingStrategies.Bubble, handledEventsToo: true);
                field.AddHandler(KeyUpEvent, (_, _) =>
                {
                    if (dragFrom is null) return;
                    dragFrom = null;
                    _page.SetCell(_row, f, null, field.Value ?? "");
                }, RoutingStrategies.Bubble, handledEventsToo: true);
                break;
            default:
                field.TemplateApplied += (_, e) =>
                {
                    if (e.NameScope.Find<TextBox>("PART_Text") is not { } box) return;
                    Ui.CommitOnLeave(box, () => ValueOf(data, t, f, lang), v => _page.SetCell(_row, f, lang, v));
                };
                break;
        }
        return field;
    }

    bool _updating;

    static string ValueOf(ProjectData data, TableFile t, FieldDef f, string? lang, int? row = null)
    {
        var raw = t.Rows.Count == 0 ? "" : t.Rows[Math.Clamp(row ?? 0, 0, t.Rows.Count - 1)].GetValueOrDefault(f.Name) ?? "";
        return f.Type == "text" && f.Localized ? data.Text(lang ?? data.Source, raw) ?? "" : f.Type == "bool" ? (TableGrid.IsTrue(raw) ? "yes" : "no") : raw;
    }

    string ValueOf(ProjectData data, TableFile t, FieldDef f, string? lang) => ValueOf(data, t, f, lang, _row);

    void Update(ProjectData data, TableFile t, int r)
    {
        _updating = true;
        try
        {
            var row = t.Rows[r];
            var id = row.GetValueOrDefault(t.KeyField) ?? "";
            var nameField = t.Fields.FirstOrDefault(f => f.Type == "text" && f.Name != t.KeyField && !TableGrid.IsDescription(f));
            var title = nameField != null ? TableGrid.Display(data, nameField, row.GetValueOrDefault(nameField.Name)) : id;
            if (title is "—" or "–") title = id;
            _body.GetLogicalDescendantsOfType<TextBlock>().First(x => x.Name == "RecordTitle").Text = title;
            _body.GetLogicalDescendantsOfType<TextBlock>().First(x => x.Name == "RecordSubtitle").Text =
                $"{t.Name} · row {(r + 1).ToString(CultureInfo.InvariantCulture)} · {DatabaseCodegen.IdEnum(t.Name)}.{id}";
            var icon = _body.GetLogicalDescendantsOfType<PixelImage>().First(x => x.Name == "RecordIcon");
            icon.Pixels = t.Fields.FirstOrDefault(f => f.Type == "sprite") is { } sf ? SpriteLibrary.Frame(data.Project, row.GetValueOrDefault(sf.Name)) : null;

            foreach (var f in InspectorFields(t))
            {
                var langs = f.Type == "text" && f.Localized ? RecordLanguages(data).Select(l => (string?)l).ToList() : [null];
                foreach (var lang in langs)
                {
                    if (!_fields.TryGetValue(LabelOf(f, lang), out var field)) continue;
                    var value = ValueOf(data, t, f, lang, r);
                    switch (field.Kind)
                    {
                        case FieldKind.Toggle: field.IsOn = TableGrid.IsTrue(row.GetValueOrDefault(f.Name)); break;
                        case FieldKind.Slider:
                            var v = ParseFixed(row.GetValueOrDefault(f.Name));
                            field.Value = Format(v);
                            field.SliderValue = FixedToSlider(t, f, v);
                            break;
                        default:
                            if (!IsEditing(field)) field.Value = value;
                            break;
                    }
                }
            }
            if (_description != null && !_description.IsFocused && t.Fields.FirstOrDefault(TableGrid.IsDescription) is { } d)
                _description.Text = ValueOf(data, t, d, d.Localized ? data.Source : null, r);

            var used = _body.GetLogicalDescendantsOfType<StackPanel>().First(x => x.Name == "UsedByList");
            used.Children.Clear();
            foreach (var u in UsedBy.Find(data, t, id))
            {
                var item = new ListRow { Label = u.Label, Glyph = "link", Padding = new Thickness(0), Name = "UsedBy_" + used.Children.Count };
                var target = u;
                item.Activated += (_, _) => _page.OpenReference(target);
                used.Children.Add(item);
            }
        }
        finally { _updating = false; }
    }

    static bool IsEditing(InspectorField f) => f.GetVisualDescendantsOfType<TextBox>().Any(b => b.IsFocused);

    // fixed fields: the slider spans 0 to the largest value of the column (at least 1)
    static double Max(TableFile t, FieldDef f) => Math.Max(1, Math.Ceiling(t.Rows.Select(r => ParseFixed(r.GetValueOrDefault(f.Name))).DefaultIfEmpty(0).Max()));
    static double FixedToSlider(TableFile t, FieldDef f, double v) => Math.Clamp(v / Max(t, f) * 100, 0, 100);
    static double SliderToFixed(TableFile t, FieldDef f, double s) => Math.Round(s / 100 * Max(t, f) * 256) / 256;
    public static double ParseFixed(string? s) => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;
    public static string Format(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
}

static class VisualHelpers
{
    public static IEnumerable<T> GetVisualDescendantsOfType<T>(this Visual v) => Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(v).OfType<T>();
}
