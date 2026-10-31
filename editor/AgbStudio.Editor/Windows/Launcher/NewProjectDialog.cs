using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Windows.Launcher;

/// <summary>
/// New Project (T-D01, 1180 x 668; T-L02 in the light theme): genre template cards (the 8 of D-09, the grid scrolls),
/// project name, location with the devkitPro warning, ROM title (12) and game code (4) with validation, save type,
/// include options and "Initialize git repository". Create Project makes the project and opens it. Args: a template id.
/// </summary>
[EditorWindow("T-D01")]
public sealed class NewProjectDialog : FigmaDialog
{
    readonly LauncherService _launcher;
    readonly StackPanel _grid = new() { Spacing = 12, Name = "TemplateGrid" };
    readonly DialogFooter _footer = new() { CancelText = "Cancel", ConfirmText = "Create Project", Name = "NewProjectFooter" };
    string _locationBase;
    bool _romEdited, _codeEdited, _busy, _syncing;
    TextBlock? _locationHelp, _romHelp, _codeHelp;
    TextBox? _nameBox;
    Button? _confirm;

    public TextField NameField { get; } = new() { Label = "Project name", Text = "My Game", Name = "ProjectName" };
    public SelectField Location { get; } = new() { Label = "Location", Name = "Location" };
    public TextField RomTitle { get; } = new() { Label = "ROM title", Help = "12 characters max", Name = "RomTitle" };
    public TextField GameCode { get; } = new() { Label = "Game code", Help = "4 characters", Name = "GameCode" };
    public SelectField SaveTypeField { get; } = new() { Label = "Save type", Name = "SaveType" };
    public CheckBox SampleLevel { get; } = new() { Content = "Sample level (Forest_01)", IsChecked = true, Name = "IncludeSampleLevel" };
    public CheckBox ThaiFont { get; } = new() { Content = "Thai variable-width font", IsChecked = true, Name = "IncludeThaiFont" };
    public CheckBox LogicGraph { get; } = new() { Content = "Logic Graph examples", IsChecked = false, Name = "IncludeLogicGraph" };
    public CheckBox Git { get; } = new() { Content = "Initialize git repository", IsChecked = true, Name = "InitGit" };
    public TemplateInfo Template { get; private set; }
    public SaveType SelectedSaveType { get; private set; } = SaveType.Sram32K;
    /// <summary>The project file of the created project (also the dialog result).</summary>
    public string? Created { get; private set; }

    public NewProjectDialog(WindowContext ctx) : this(ctx.Services, ctx.Args as string) { }

    public NewProjectDialog(EditorServices s, string? templateId = null)
    {
        Name = "NewProjectDialog";
        _launcher = LauncherService.Of(s);
        Template = TemplateCatalog.Find(templateId);
        _locationBase = _launcher.DefaultLocationBase();

        var choose = UI.Text("Choose a genre template", "Overline/11", "text/secondary");
        choose.Margin = new Thickness(0, 0, 0, 10);
        DockPanel.SetDock(choose, Dock.Top);
        var scroll = new ScrollViewer { Content = _grid, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Margin = new Thickness(0, 0, 0, 20), Name = "TemplateScroll" };
        var left = new DockPanel { Width = 780, Children = { choose, scroll } };

        var codes = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("*,10,*") };
        Grid.SetColumn(GameCode, 2);
        codes.Children.AddRange([RomTitle, GameCode]);
        foreach (var box in new[] { SampleLevel, ThaiFont, LogicGraph, Git }) box.Height = 16;
        var includes = UI.Column(12, SampleLevel, ThaiFont, LogicGraph, Git);
        var right = UI.Column(12, UI.Text("Project", "Overline/11", "text/secondary"), NameField, Location, codes, SaveTypeField, UI.Text("Include", "Overline/11", "text/secondary"), includes);
        right.Width = 338;
        var body = new DockPanel { Margin = new Thickness(20, 20, 20, 0), HorizontalSpacing = 20 };
        DockPanel.SetDock(left, Dock.Left);
        body.Children.AddRange([left, right]);

        _footer.Cancel += (_, _) => Close(null);
        _footer.Confirm += async (_, _) => await CreateAsync();
        _footer.TemplateApplied += (_, e) => { _confirm = e.NameScope.Find<Button>("PART_Confirm"); Validate(); };
        Build("New Project", "folderplus", body, _footer, 1180);
        Height = 668;

        Hook(NameField, _ => { }, box => { box.MaxLength = 64; _nameBox = box; });
        Hook(RomTitle, help => _romHelp = help, box => box.MaxLength = NewProjectRules.RomTitleMax);
        Hook(GameCode, help => _codeHelp = help, box => box.MaxLength = NewProjectRules.GameCodeLength);
        Location.TemplateApplied += (_, e) =>
        {
            if (e.NameScope.Find<Button>("PART_Box") is { } b) b.Click += async (_, _) => await PickLocationAsync();
            _locationHelp = HelpOf(Location);
            Validate();
        };
        SaveTypeField.Options = NewProjectRules.SaveTypes.Select(NewProjectRules.SaveTypeLabel).ToList();
        SaveTypeField.Selected = NewProjectRules.SaveTypeLabel(SelectedSaveType);
        SaveTypeField.TemplateApplied += (_, e) => { if (e.NameScope.Find<Button>("PART_Box") is { } b) b.Click += (_, _) => OpenSaveTypes(b); };

        NameField.PropertyChanged += (_, e) => { if (e.Property == TextField.TextProperty) Derive(); };
        RomTitle.PropertyChanged += (_, e) =>
        {
            if (e.Property != TextField.TextProperty || _syncing) return;
            _romEdited = true;
            Upper(RomTitle);
            if (!_codeEdited) Sync(GameCode, NewProjectRules.GameCodeFrom(RomTitle.Text ?? ""));
            Validate();
        };
        GameCode.PropertyChanged += (_, e) =>
        {
            if (e.Property != TextField.TextProperty || _syncing) return;
            _codeEdited = true;
            Upper(GameCode);
            Validate();
        };
        FillTemplates();
        Derive();
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // the project name has the focus, as in Figma
        Avalonia.Threading.Dispatcher.UIThread.Post(() => { _nameBox?.Focus(); _nameBox?.SelectAll(); });
    }

    // ---- fields ---------------------------------------------------------------------------------------

    static TextBlock? HelpOf(TemplatedControl field) =>
        field.GetVisualChildren().OfType<StackPanel>().FirstOrDefault()?.Children.OfType<TextBlock>().Skip(1).FirstOrDefault();

    void Hook(TextField field, Action<TextBlock?> help, Action<TextBox> box) =>
        field.TemplateApplied += (_, e) =>
        {
            if (e.NameScope.Find<TextBox>("PART_Box") is { } b) box(b);
            help(HelpOf(field));
            Validate();
        };

    void Sync(TextField field, string text)
    {
        _syncing = true;
        field.Text = text;
        _syncing = false;
    }

    void Upper(TextField field)
    {
        var t = field.Text ?? "";
        if (t != t.ToUpperInvariant()) Sync(field, t.ToUpperInvariant());
    }

    /// <summary>Name -> location, ROM title and game code, until the user edits those.</summary>
    void Derive()
    {
        var name = NameField.Text ?? "";
        Location.Selected = NewProjectRules.FolderFrom(_locationBase, name);
        if (!_romEdited) Sync(RomTitle, NewProjectRules.RomTitleFrom(name));
        if (!_codeEdited) Sync(GameCode, NewProjectRules.GameCodeFrom(RomTitle.Text ?? ""));
        Validate();
    }

    /// <summary>Sets the field values (tests and the Figma fixture), as typing them would.</summary>
    public void SetLocationBase(string folder)
    {
        _locationBase = folder;
        Derive();
    }

    async Task PickLocationAsync()
    {
        var folder = await _launcher.PickFolder("Location");
        if (folder != null) SetLocationBase(folder);
    }

    void OpenSaveTypes(Control anchor)
    {
        var menu = new ContextMenu();
        foreach (var t in NewProjectRules.SaveTypes)
        {
            var type = t;
            var item = new MenuItem { Header = NewProjectRules.SaveTypeLabel(t), ToggleType = MenuItemToggleType.CheckBox, IsChecked = t == SelectedSaveType };
            item.Click += (_, _) => SetSaveType(type);
            menu.Items.Add(item);
        }
        menu.Open(anchor);
    }

    public void SetSaveType(SaveType t)
    {
        SelectedSaveType = t;
        SaveTypeField.Selected = NewProjectRules.SaveTypeLabel(t);
    }

    public string? LocationHelpText => Location.Help;
    public bool LocationWarns => NewProjectRules.IsInside(Location.Selected ?? "", _launcher.DevkitPro);

    /// <summary>Checks every field, colours the help texts and enables Create Project.</summary>
    public bool Validate()
    {
        var folder = Location.Selected ?? "";
        var usable = NewProjectRules.IsUsableFolder(folder);
        Location.Help = usable ? $"A folder outside {_launcher.DevkitPro} is safer for updates" : $"{folder} is not empty";
        var romOk = NewProjectRules.IsValidRomTitle(RomTitle.Text);
        var codeOk = NewProjectRules.IsValidGameCode(GameCode.Text);
        _locationHelp?.Token(TextBlock.ForegroundProperty, !usable ? "status/danger" : LocationWarns ? "status/warning" : "text/muted");
        _romHelp?.Token(TextBlock.ForegroundProperty, romOk ? "text/muted" : "status/danger");
        _codeHelp?.Token(TextBlock.ForegroundProperty, codeOk ? "text/muted" : "status/danger");
        var ok = NewProjectRules.IsValidName(NameField.Text) && usable && romOk && codeOk && !_busy;
        if (_confirm != null) _confirm.IsEnabled = ok;
        return ok;
    }

    // ---- templates ------------------------------------------------------------------------------------

    void FillTemplates()
    {
        _grid.Children.Clear();
        for (var i = 0; i < TemplateCatalog.All.Count; i += 3)
        {
            var row = UI.Row(12);
            foreach (var t in TemplateCatalog.All.Skip(i).Take(3))
            {
                var card = new TemplateCard { Title = t.Title, Description = t.Description, Preview = t.Preview, IsSelected = t == Template, VerticalAlignment = VerticalAlignment.Center, Name = "Template_" + t.Id };
                var info = t;
                card.Activated += (_, _) => SelectTemplate(info);
                row.Children.Add(card);
            }
            _grid.Children.Add(row);
        }
        _footer.Hint = $"Template adds: {Template.Adds}.";
    }

    public IReadOnlyList<TemplateCard> Cards => _grid.Children.OfType<StackPanel>().SelectMany(r => r.Children.OfType<TemplateCard>()).ToList();

    public void SelectTemplate(TemplateInfo t)
    {
        Template = t;
        foreach (var c in Cards) c.IsSelected = c.Name == "Template_" + t.Id;
        _footer.Hint = $"Template adds: {Template.Adds}.";
    }

    public NewProjectOptions Options() => new()
    {
        Template = Template, Name = (NameField.Text ?? "").Trim(), Folder = Location.Selected ?? "", RomTitle = RomTitle.Text ?? "", GameCode = GameCode.Text ?? "",
        SaveType = SelectedSaveType, SampleLevel = SampleLevel.IsChecked == true, ThaiFont = ThaiFont.IsChecked == true,
        LogicGraphExamples = LogicGraph.IsChecked == true, InitGit = Git.IsChecked == true,
    };

    /// <summary>Create Project: asks about unsaved files of the open project, creates the project and opens it.</summary>
    public async Task<string?> CreateAsync()
    {
        if (_busy || !Validate()) return null;
        if (!await _launcher.ConfirmUnsavedAsync(UnsavedReason.CloseProject)) return null;
        _busy = true;
        Validate();
        try
        {
            var created = await _launcher.CreateAsync(Options());
            Created = created.ProjectPath;
            Close(created.ProjectPath);
            return created.ProjectPath;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException or System.Text.Json.JsonException)
        {
            _launcher.Services.Toasts.Show("Could not create the project: " + e.Message, ToastStatus.Error);
            return null;
        }
        finally
        {
            _busy = false;
            Validate();
        }
    }
}
