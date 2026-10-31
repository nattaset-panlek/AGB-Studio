using System.Globalization;
using System.Text.RegularExpressions;
using AgbStudio.Build.Data;
using AgbStudio.Core.Project;
using ProjectFile = AgbStudio.Core.Project.ProjectFile;
using AgbStudio.Editor.Shell;
using AgbStudio.Scripting;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using BuildCpp = AgbStudio.Build.Pipeline.Cpp;

namespace AgbStudio.Editor.Windows.Database;

/// <summary>
/// "Use it in a script" (T-P07): C# for the selected row with the names the scripting API generates for the table
/// (<see cref="ProjectApi"/>: <c>Items.Get(ItemId.Potion)</c>, row fields as members, <c>Shop.Buy</c> for tables with a
/// price when the save data has the shop currency field).
/// </summary>
public static partial class ScriptSample
{
    public enum Kind { Plain, Keyword, Type, Method, Comment }

    public sealed record Token(string Text, Kind Kind);

    [GeneratedRegex(@"^\s*([A-Za-z_]\w*)\s*\(\s*([A-Za-z_]\w*)\s*\)\s*;?\s*$")]
    private static partial Regex CallWithField();

    /// <summary>The shop currency save field the scripting API spends (ScriptCompileContext.ShopCurrencyField).</summary>
    public const string ShopCurrencyField = "coins";

    static string Lower(string id)
    {
        var s = BuildCpp.Ident(id);
        return s.Length == 0 ? "row" : char.ToLowerInvariant(s[0]) + s[1..];
    }

    static void Pad(List<Token> line, int column = 40)
    {
        var len = line.Sum(t => t.Text.Length);
        line.Add(new Token(new string(' ', Math.Max(3, column - len)), Kind.Plain));
    }

    public static List<List<Token>> Lines(TableFile t, int row, ProjectFile settings)
    {
        var lines = new List<List<Token>>();
        var cls = BuildCpp.Ident(t.Name);
        var idEnum = DatabaseCodegen.IdEnum(t.Name);
        if (row < 0 || row >= t.Rows.Count)
        {
            lines.Add([new("var", Kind.Keyword), new(" count = ", Kind.Plain), new(cls, Kind.Type), new(".Count;", Kind.Plain)]);
            return lines;
        }
        var r = t.Rows[row];
        var id = ProjectApi.CsIdent(BuildCpp.Ident(r.GetValueOrDefault(t.KeyField) ?? ""));
        var v = ProjectApi.CsIdent(Lower(r.GetValueOrDefault(t.KeyField) ?? ""));
        var l1 = new List<Token> { new("var", Kind.Keyword), new($" {v} = ", Kind.Plain), new(cls, Kind.Type), new(".", Kind.Plain), new("Get", Kind.Method), new("(", Kind.Plain), new(idEnum, Kind.Type), new($".{id});", Kind.Plain) };
        Pad(l1, 41);
        l1.Add(new("// typed, autocompleted", Kind.Comment));
        lines.Add(l1);

        // a script cell calling an action with one of the row's fields ("Heal(power)") shows that call on the player
        var fields = t.Fields.Where(f => f.Name != t.KeyField).ToList();
        var call = fields.Where(f => f.Type == "script").Select(f => CallWithField().Match(r.GetValueOrDefault(f.Name) ?? "")).FirstOrDefault(m => m.Success && fields.Any(x => x.Name == m.Groups[2].Value));
        FieldDef? shown;
        List<Token> l2;
        if (call != null)
        {
            shown = fields.First(x => x.Name == call.Groups[2].Value);
            l2 = [new("player.", Kind.Plain), new(call.Groups[1].Value, Kind.Method), new($"({v}.{ProjectApi.CsIdent(shown.Name)});", Kind.Plain)];
        }
        else
        {
            shown = fields.FirstOrDefault(f => f.Type is "fixed" or "int" or "bool") ?? fields.FirstOrDefault(f => f.Type == "enum");
            l2 = shown is null ? [] : [new("var", Kind.Keyword), new($" {ProjectApi.CsIdent(shown.Name)} = {v}.{ProjectApi.CsIdent(shown.Name)};", Kind.Plain)];
        }
        if (shown != null)
        {
            var value = r.GetValueOrDefault(shown.Name) ?? "";
            if (value.Length == 0) value = shown.Default;
            if (shown.Type == "fixed") value = RecordPanel.Format(RecordPanel.ParseFixed(value));
            Pad(l2);
            l2.Add(new($"// {shown.Type} {value}".TrimEnd(), Kind.Comment));
            lines.Add(l2);
        }

        var price = fields.FirstOrDefault(f => f.Name == "price" && f.Type == "int");
        if (price != null && settings.Save.Fields.Any(f => f.Name == ShopCurrencyField && f.Type.Trim() is "int" or "short"))
            lines.Add([new("if", Kind.Keyword), new(" (", Kind.Plain), new("Game", Kind.Type), new($".{ShopCurrencyField} >= {v}.price) Shop.", Kind.Plain), new("Buy", Kind.Method),
                new("(", Kind.Plain), new(idEnum, Kind.Type), new($".{id});", Kind.Plain)]);
        return lines;
    }

    public static string Text(TableFile t, int row, ProjectFile settings) =>
        string.Join("\n", Lines(t, row, settings).Select(l => string.Concat(l.Select(x => x.Text)).TrimEnd()));

    /// <summary>The sample as Mono/11 text with the Figma syntax colours (keywords pink, types blue, calls green, comments muted).</summary>
    public static TextBlock View(IEnumerable<List<Token>> lines)
    {
        var tb = UI.Text(null, "Mono/11", "text/primary").Named("ScriptSampleText");
        var first = true;
        foreach (var line in lines)
        {
            if (!first) tb.Inlines!.Add(new LineBreak());
            first = false;
            foreach (var tok in line)
            {
                var run = new Run(tok.Text);
                switch (tok.Kind)
                {
                    case Kind.Keyword: run.Foreground = Keyword; break;
                    case Kind.Type: run.Bind(TextElement.ForegroundProperty, tb.GetResourceObservable("accent/hover")); break;
                    case Kind.Method: run.Bind(TextElement.ForegroundProperty, tb.GetResourceObservable("status/success")); break;
                    case Kind.Comment: run.Bind(TextElement.ForegroundProperty, tb.GetResourceObservable("text/muted")); break;
                }
                tb.Inlines!.Add(run);
            }
        }
        return tb;
    }

    /// <summary>Keyword colour: the raw pink Figma uses for C# keywords (Theme/Literals.axaml "lit/accent-pink").</summary>
    static IBrush? Keyword => Avalonia.Application.Current?.FindResource("lit/accent-pink") is Color c ? new SolidColorBrush(c) : null;
}

/// <summary>Numbers as the status bar shows ROM sizes ("ROM 576 B", "ROM 1.1 KB").</summary>
public static class Sizes
{
    public static string Rom(int bytes) => bytes < 1024 ? $"ROM {bytes} B" : string.Create(CultureInfo.InvariantCulture, $"ROM {bytes / 1024.0:0.0} KB");

    /// <summary>The table's bytes in the ROM (P9.07): rows times the row struct, whose size is the field bytes padded to
    /// the largest field, as the build lays it out (DatabaseCodegen.TableBytes), not the sum of the Add Field bytes.</summary>
    public static int TableBytes(TableFile t) => DatabaseCodegen.TableBytes(t);
}
