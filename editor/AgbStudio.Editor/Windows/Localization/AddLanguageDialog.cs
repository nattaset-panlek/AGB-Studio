using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Windows.Database;
using Avalonia;
using Avalonia.Controls;

namespace AgbStudio.Editor.Windows.Localization;

/// <summary>
/// Add Language (T-D20, 520 x 416; Localization › Languages +, Project Settings › Localization › Add language): the
/// language, the language it is translated from, its font (a new font when the current one lacks the script, e.g.
/// "jp_kana · new font" for Japanese kana), "Mark all N strings as missing" and "Fall back to … until translated".
/// Adds the language to the project settings and writes text/strings.&lt;code&gt;.json (undoable).
/// </summary>
[EditorWindow("T-D20")]
public sealed class AddLanguageDialog : FigmaDialog
{
    const string NewFont = " · new font";

    readonly ProjectData? _data;
    readonly WindowContext _ctx;

    public SelectField LanguageField { get; } = new() { Label = "Language", Name = "Language" };
    public SelectField FromField { get; } = new() { Label = "Translate from", Name = "TranslateFrom" };
    public SelectField FontField { get; } = new() { Label = "Font", Name = "Font" };
    public CheckBox MarkMissing { get; } = new() { IsChecked = true, Name = "MarkMissing" };
    public CheckBox Fallback { get; } = new() { IsChecked = true, Name = "Fallback" };
    public string? Error { get; private set; }

    public AddLanguageDialog(WindowContext ctx)
    {
        _ctx = ctx;
        Name = "AddLanguageDialog";
        _data = (ctx.Args as LocalizationPage)?.Data ?? ProjectData.For(ctx.Services);
        LanguageField.Options = Available().Select(l => l.Label).ToList();
        LanguageField.Selected = LanguageField.Options.FirstOrDefault();
        FromField.Options = _data?.Languages.Select(l => LabelOf(l.Code)).ToList() ?? [];
        FromField.Selected = _data is null ? null : LabelOf(_data.Source);
        Dropdown.Attach(LanguageField, () => LanguageField.Options ?? [], _ => Update());
        Dropdown.Attach(FromField, () => FromField.Options ?? [], _ => Update());
        Dropdown.Attach(FontField, () => FontField.Options ?? [], _ => Update());

        var content = UI.Column(0,
            LanguageField,
            FromField.Margin(0, 14, 0, 0),
            FontField.Margin(0, 14, 0, 0),
            MarkMissing.Margin(0, 14, 0, 0),
            Fallback.Margin(0, 15, 0, 0));
        content.Margin = new Thickness(20, 20, 20, 0);
        var footer = new DialogFooter { ConfirmText = "Add Language" };
        footer.Cancel += (_, _) => Close(null);
        footer.Confirm += (_, _) => { if (Create() is { } code) Close(code); };
        Build("Add Language", "globe", content, footer, 520);
        Update(resetFont: true);
    }

    /// <summary>Languages that are not in the project yet.</summary>
    public IReadOnlyList<LanguageName> Available() =>
        LanguageNames.All.Where(l => _data is null || !_data.Loc.Languages.Any(x => x.Code == l.Code)).ToList();

    string LabelOf(string code) => LanguageNames.Find(code)?.English ?? _data?.LanguageName(code) ?? code;

    LanguageName? Selected => LanguageNames.ByLabel(LanguageField.Selected ?? "");
    string? FromCode => _data?.Loc.Languages.FirstOrDefault(l => LabelOf(l.Code) == FromField.Selected)?.Code;

    public void Choose(string code)
    {
        if (LanguageNames.Find(code) is { } l) LanguageField.Selected = l.Label;
        Update(resetFont: true);
    }

    /// <summary>The font of the language translated from (or the default font), and whether it has the new language's characters.</summary>
    (string Font, bool Covers) BaseFont()
    {
        if (_data is null || Selected is not { } lang) return ("", true);
        var from = FromCode ?? _data.Source;
        var font = new GbaText(_data).FontName(from);
        var path = _data.Project.FontFiles().FirstOrDefault(f => GameProject.AssetName(f) == font);
        if (path is null) return (font, false);
        try { return (font, GameProject.Load<FontFile>(path).CharacterSets.Contains(lang.Script)); }
        catch (Exception e) when (e is IOException or System.Text.Json.JsonException or InvalidDataException) { return (font, false); }
    }

    /// <summary>Name of the font created for a language whose script the current font lacks ("jp_kana").</summary>
    public static string NewFontName(LanguageName l) => l.Code == "ja" ? "jp_kana" : $"{l.Code}_{LanguageNames.ScriptShort(l.Script).ToLowerInvariant()}";

    void Update(bool resetFont = false)
    {
        var lang = Selected;
        var (font, covers) = BaseFont();
        var fonts = _data is null ? [] : new GbaText(_data).FontNames().ToList();
        var options = new List<string>();
        if (lang != null && !covers) options.Add(NewFontName(lang) + NewFont);
        options.AddRange(fonts);
        FontField.Options = options;
        if (resetFont || FontField.Selected is null || !options.Contains(FontField.Selected))
            FontField.Selected = lang != null && !covers ? NewFontName(lang) + NewFont : font.Length > 0 ? font : options.FirstOrDefault();
        FontField.Help = lang != null && !covers && FontField.Selected?.EndsWith(NewFont, StringComparison.Ordinal) == true
            ? $"{font} has no {LanguageNames.ScriptShort(lang.Script)} — a new font will be created" : null;
        var count = _data?.Keys().Count ?? 0;
        MarkMissing.Content = $"Mark all {count} strings as missing";
        Fallback.Content = $"Fall back to {FromField.Selected ?? "English"} until translated";
    }

    /// <summary>Adds the language; returns its code, or null with <see cref="Error"/> set.</summary>
    public string? Create()
    {
        Error = _data is null ? "Open a project first." : Selected is null ? "Choose a language." : FromCode is null ? "Choose the language to translate from." : null;
        if (Error != null) return null;
        var data = _data!;
        var lang = Selected!;
        var from = FromCode!;
        var fontChoice = FontField.Selected ?? "";
        var createFont = fontChoice.EndsWith(NewFont, StringComparison.Ordinal);
        var fontName = createFont ? fontChoice[..^NewFont.Length] : fontChoice;
        var info = new LanguageInfo { Code = lang.Code, Name = lang.Native, Font = fontName, Fallback = Fallback.IsChecked == true ? from : "" };
        var table = new StringTable { Language = lang.Code };
        if (MarkMissing.IsChecked != true)
            foreach (var k in data.Keys()) if (data.Text(from, k) is { } t) table.Strings[k] = t;
        var stringsPath = data.StringsPath(lang.Code);
        var fontPath = Path.Combine(data.Project.FontsDir, fontName + ".font");
        FontFile? newFont = null;
        if (createFont && !File.Exists(fontPath))
        {
            var basePath = data.Project.FontFiles().FirstOrDefault(f => GameProject.AssetName(f) == new GbaText(data).FontName(from));
            newFont = basePath is null ? new FontFile() : GameProject.Load<FontFile>(basePath);
            newFont.Name = fontName;
            newFont.CharacterSets = ["Basic Latin", lang.Script];
            newFont.ExtraCharacters = "";
            newFont.Glyphs = [];
        }
        var settings = _ctx.Services.ProjectSettings;
        data.Edit("Add language", null, () =>
        {
            data.Loc.Languages.Add(info);
            data.Strings[lang.Code] = table;
            GameProject.Save(stringsPath, table);
            if (newFont != null) GameProject.Save(fontPath, newFont);
            settings.Save();
        }, () =>
        {
            data.Loc.Languages.Remove(info);
            data.Strings.Remove(lang.Code);
            if (File.Exists(stringsPath)) File.Delete(stringsPath);
            if (newFont != null && File.Exists(fontPath)) File.Delete(fontPath);
            settings.Save();
        });
        return lang.Code;
    }
}
