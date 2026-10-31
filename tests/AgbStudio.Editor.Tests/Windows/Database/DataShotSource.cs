using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Gallery;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Support;
using AgbStudio.Editor.Windows.Database;
using AgbStudio.Editor.Windows.Dialogue;
using AgbStudio.Editor.Windows.Localization;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.Database;

/// <summary>SHOT for 11.H, 11.I and 11.K: Database, Localization and Dialogue Editor pages, their dialogs and the Add to Dialogue / Command menus, on the Figma fixture.</summary>
public sealed class DataShotSource : IShotSource
{
    public IEnumerable<(string Id, Action<string, int, int> Render)> Windows()
    {
        yield return ("T-P07", (path, w, h) => Page(path, w, h, "T-P07", win =>
        {
            var page = (DatabasePage)win.CurrentPage!;
            page.SelectRow(2);
        }));
        yield return ("T-D17", (path, w, h) => Dialog(path, w, h, "T-D17", fx => new DatabasePage(fx.Services), (ctx, _) =>
        {
            var d = new NewTableDialog(ctx);
            d.NameField.Text = "Enemies";
            GalleryCatalog.Force(d.NameField, ":focus", "PART_Box");
            return d;
        }));
        yield return ("T-D18", (path, w, h) => Dialog(path, w, h, "T-D18", fx => new DatabasePage(fx.Services), (ctx, _) =>
        {
            var d = new AddFieldDialog(ctx);
            d.NameField.Text = "rarity";
            foreach (var v in new[] { "Common", "Rare", "Epic" }) d.AddValue(v);
            GalleryCatalog.Force(d.NameField, ":focus", "PART_Box");
            return d;
        }));
        yield return ("T-D19", (path, w, h) => Dialog(path, w, h, "T-D19", fx => new DatabasePage(fx.Services), (ctx, fx) =>
            new ImportCsvDialog(new WindowContext { Services = ctx.Services, Info = ctx.Info, Args = new ImportCsvRequest((DatabasePage)ctx.Args!, DataFixture.WriteItemsCsv(fx.Folder)) })));
        yield return ("T-P14", (path, w, h) => Page(path, w, h, "T-P14", win =>
        {
            var page = (LocalizationPage)win.CurrentPage!;
            page.SelectKey("dlg.forest.hopper_02");
            Capture.Pump();
            GalleryCatalog.Force(page.TargetBox!, ":focus");
        }));
        yield return ("T-D20", (path, w, h) => Dialog(path, w, h, "T-D20", fx =>
        {
            // Figma adds Japanese, so the project starts without it
            var loc = fx.Services.Project.Current!.Settings.Localization;
            loc.Languages.RemoveAll(l => l.Code == "ja");
            return null;
        }, (ctx, _) =>
        {
            var d = new AddLanguageDialog(ctx);
            d.Choose("ja");
            GalleryCatalog.Force(d.LanguageField, ":focus-within");
            return d;
        }));
        yield return ("T-P16", (path, w, h) => Page(path, w, h, "T-P16", win =>
        {
            var page = (DialogueEditorPage)win.CurrentPage!;
            page.SelectLine(1);
        }));
        yield return ("T-D21", (path, w, h) => Dialog(path, w, h, "T-D21", fx => new DialogueEditorPage(fx.Services), (ctx, _) =>
        {
            var d = new NewSpeakerDialog(ctx);
            d.NameEn.Text = "Bat";
            d.NameTh.Text = "ค้างคาว";
            d.Blip.Selected = "blip_low.wav";
            return d;
        }, unfocused: true));
        yield return ("T-M23", (path, w, h) => Menu(path, w, h, DialogueMenus.AddToDialogue()));
        yield return ("T-M24", (path, w, h) => Menu(path, w, h, DialogueMenus.CommandMenu()));
    }

    static void Page(string path, int w, int h, string id, Action<MainWindow> setup)
    {
        using var fx = new Fixture();
        DataFixture.Open(fx);
        var win = new MainWindow(fx.Services) { Width = w, Height = h, SystemDecorations = SystemDecorations.None };
        try
        {
            win.Show();
            _ = fx.Services.Windows.Open(id, source: "SHOT");
            Capture.Pump();
            setup(win);
            // Figma shows Undo and Redo enabled
            fx.Services.Undo.Clear();
            fx.Services.Undo.Push(new DelegateAction("a", () => { }, () => { }));
            fx.Services.Undo.Push(new DelegateAction("b", () => { }, () => { }));
            fx.Services.Undo.Undo();
            foreach (var d in fx.Services.Documents.Open) d.IsDirty = false;
            Capture.Save(Capture.Frame(win), path);
        }
        finally { win.Close(); }
    }

    /// <param name="unfocused">The Figma frame shows the dialog after the names were typed, with no field focused.</param>
    static void Dialog(string path, int w, int h, string id, Func<Fixture, object?> args, Func<WindowContext, Fixture, Control> create, bool unfocused = false)
    {
        using var fx = new Fixture();
        DataFixture.Open(fx);
        var ctx = new WindowContext { Services = fx.Services, Info = fx.Services.Windows.Info(id), Args = args(fx) };
        var host = Capture.Host(create(ctx, fx), w, h, Brushes.Black);
        try
        {
            if (unfocused) host.FocusManager?.ClearFocus();
            Capture.Save(Capture.Frame(host), path);
        }
        finally { host.Close(); }
    }

    static void Menu(string path, int w, int h, IReadOnlyList<AgbStudio.Editor.Commands.MenuEntry> entries)
    {
        using var fx = new Fixture(withApp: false);
        var builder = new MenuBuilder(fx.Services.Commands, fx.Services.Shortcuts);
        Capture.ControlToPng(new MenuSurface { ItemsSource = builder.BuildItems(entries) }, w, h, path, Brushes.Black);
    }
}
