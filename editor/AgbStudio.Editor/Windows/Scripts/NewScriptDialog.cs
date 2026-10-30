using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Graphics;
using AgbStudio.Editor.Shell;
using AgbStudio.Scripting;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace AgbStudio.Editor.Windows.Scripts;

/// <summary>
/// New Script (T-D14, 560 x 564; Script Editor › Scripts +, Component Picker › New C# script…): class name with the file
/// path, template Behaviour / Static helper / Scene script, Attach to (actors of the project), "Open in the external
/// editor after creating" (P14.05). Creates scripts/&lt;Class&gt;.cs from ScriptTemplates, adds the behaviour to the actor
/// and opens it. Args: the actor to attach to ("Bat · Cave_01").
/// </summary>
[EditorWindow("T-D14")]
public sealed class NewScriptDialog : FigmaDialog
{
    readonly EditorServices _s;
    readonly ScriptWorkspace _ws;
    readonly List<(ScriptTemplates.Kind Kind, Border Card, Icon Check)> _cards = [];
    readonly DialogFooter _footer = new() { ConfirmText = "Create Script" };

    public TextField ClassName { get; } = new() { Label = "Class name", Name = "ClassName" };
    public SelectField AttachTo { get; } = new() { Label = "Attach to", Name = "AttachTo" };
    public CheckBox OpenExternally { get; } = new() { Content = "Open in the external editor after creating", Name = "OpenExternallyAfter" };
    public ScriptTemplates.Kind Template { get; private set; } = ScriptTemplates.Kind.Behaviour;
    public string? Error { get; private set; }

    public NewScriptDialog(WindowContext ctx) : this(ctx.Services, ctx.Args as string) { }

    public NewScriptDialog(EditorServices s, string? attachTo = null)
    {
        _s = s;
        _ws = ScriptWorkspace.Of(s);
        Name = "NewScriptDialog";
        var actors = _ws.Actors().Select(a => a.Label).ToList();
        AttachTo.Options = actors;
        AttachTo.Selected = attachTo ?? actors.FirstOrDefault(a => !_ws.Actors().First(x => x.Label == a).Actor.Components.Any(c => c.Type.EndsWith("Controller", StringComparison.Ordinal))) ?? actors.FirstOrDefault();
        ClassName.Text = DefaultName(AttachTo.Selected);
        AttachTo.TemplateApplied += (_, e) =>
        {
            if (e.NameScope.Find<Button>("PART_Box") is { } box)
                box.Click += (_, _) => PageChrome.ShowChoices(box, AttachTo.Options ?? [], AttachTo.Selected, v => AttachTo.Selected = v);
        };
        OpenExternally.IsChecked = false;

        var templates = UI.Column(15,
            Card(ScriptTemplates.Kind.Behaviour, "cube", "Behaviour", "Attach to an actor · Start(), Update(), OnTouch()"),
            Card(ScriptTemplates.Kind.StaticHelper, "code", "Static helper", "Shared functions with no actor, e.g. MathUtil"),
            Card(ScriptTemplates.Kind.SceneScript, "map", "Scene script", "Runs on scene enter / exit and holds scene state"));
        var content = UI.Column(14,
            ClassName,
            UI.Column(16, UI.Text("Template", "Overline/11", "text/secondary"), templates),
            AttachTo,
            OpenExternally);
        content.Margin = new Thickness(20, 20, 20, 18);
        ClassName.PropertyChanged += (_, e) => { if (e.Property == TextField.TextProperty) UpdateHelp(); };
        UpdateHelp();
        SelectTemplate(ScriptTemplates.Kind.Behaviour);
        _footer.Cancel += (_, _) => Close(null);
        _footer.Confirm += (_, _) => _ = CreateAsync();
        Build("New Script", "code", content, _footer, 560);
    }

    static string DefaultName(string? actor)
    {
        var name = actor is null ? "NewBehaviour" : new string(actor.Split(" · ")[0].Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        if (name.Length == 0 || char.IsDigit(name[0])) name = "NewBehaviour";
        return name.EndsWith("Controller", StringComparison.Ordinal) || name == "NewBehaviour" ? name : name + "Controller";
    }

    Border Card(ScriptTemplates.Kind kind, string glyph, string title, string description)
    {
        var icon = PageChrome.Glyph(glyph, 16, "text/secondary");
        var check = PageChrome.Glyph("check", 14, "accent/default");
        check.IsVisible = false;
        var text = UI.Column(3, UI.Text(title, "Body/12 Semibold", "text/primary"), UI.Text(description, "Label/11", "text/muted"));
        text.VerticalAlignment = VerticalAlignment.Center;
        var row = new DockPanel { HorizontalSpacing = 14 };
        DockPanel.SetDock(icon, Dock.Left);
        DockPanel.SetDock(check, Dock.Right);
        row.Children.AddRange([icon, check, text]);
        var card = UI.Frame(row, "bg/base", "border/default", new Thickness(1), new Thickness(14, 0, 16, 0), 8);
        card.Height = 57;
        card.Name = "Template_" + kind;
        card.Cursor = new Cursor(StandardCursorType.Hand);
        card.PointerPressed += (_, e) => { SelectTemplate(kind); e.Handled = true; };
        _cards.Add((kind, card, check));
        return card;
    }

    /// <summary>Selects a template card; Attach to only applies to Behaviour.</summary>
    public void SelectTemplate(ScriptTemplates.Kind kind)
    {
        Template = kind;
        foreach (var (k, card, check) in _cards)
        {
            var on = k == kind;
            card.Token(Border.BackgroundProperty, on ? "accent/selected" : "bg/base");
            card.Token(Border.BorderBrushProperty, on ? "accent/default" : "border/default");
            check.IsVisible = on;
            if (card.Child is DockPanel d && d.Children[0] is Icon i) i.Token(Icon.ForegroundProperty, on ? "text/on-selected" : "text/secondary");
        }
        AttachTo.IsEnabled = kind == ScriptTemplates.Kind.Behaviour;
        AttachTo.Opacity = AttachTo.IsEnabled ? 1 : 0.5;
    }

    void UpdateHelp()
    {
        var name = ClassName.Text?.Trim() ?? "";
        Error = Validate(name);
        ClassName.Help = Error ?? $"File: scripts/{name}.cs";
        _footer.IsEnabled = Error is null;
    }

    string? Validate(string name)
    {
        if (name.Length == 0) return "Enter a class name.";
        if (!Microsoft.CodeAnalysis.CSharp.SyntaxFacts.IsValidIdentifier(name) || Microsoft.CodeAnalysis.CSharp.SyntaxFacts.GetKeywordKind(name) != Microsoft.CodeAnalysis.CSharp.SyntaxKind.None)
            return "Use letters, digits and _ (a C# class name).";
        if (_ws.Resolve(name) != null) return $"{name}.cs already exists.";
        return null;
    }

    /// <summary>Creates the script, opens it in the Script Editor and, when checked, in the external editor. Result: the file path.</summary>
    public async Task<string?> CreateAsync()
    {
        var name = ClassName.Text?.Trim() ?? "";
        if (Validate(name) is { } err) { ClassName.Help = err; return null; }
        string path;
        try { path = _ws.CreateScript(name, Template, Template == ScriptTemplates.Kind.Behaviour ? AttachTo.Selected : null); }
        catch (Exception e) when (e is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            ClassName.Help = e.Message;
            return null;
        }
        Close(path);
        await _s.Windows.Open("T-P04", path, source: "NewScript");
        if (OpenExternally.IsChecked == true)
        {
            var line = File.ReadAllLines(path).Select((l, i) => (l, i)).FirstOrDefault(x => x.l.Contains("class " + name, StringComparison.Ordinal)).i + 1;
            ExternalEditorService.Of(_s).Open(path, line);
        }
        return path;
    }
}
