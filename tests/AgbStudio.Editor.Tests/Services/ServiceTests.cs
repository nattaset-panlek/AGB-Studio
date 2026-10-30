using AgbStudio.Core.Project;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Tests.Support;

namespace AgbStudio.Editor.Tests.Services;

/// <summary>P10.09 undo, P10.10 autosave, P10.14 storage, P10.16 toolchain, P10.19 recent projects.</summary>
public class ServiceTests
{
    [Fact]
    public void Preferences_DefaultPathIsAppDataAgbStudio()
    {
        var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AGBStudio", "preferences.json");
        Assert.Equal(expected, PreferencesStore.DefaultPath);
    }

    [Fact]
    public void Preferences_DefaultsMatchFigmaAndRoundTrip()
    {
        using var fx = new Fixture(withApp: false);
        var p = fx.Services.Preferences.Current;
        Assert.Equal((ThemeMode.Dark, "#4C8DFF", 100, 13, false, true), (p.Theme, p.Accent, p.UiScalePercent, p.CodeFontSize, p.ReduceMotion, p.ShowPixelGrid));
        Assert.Equal((2, 200, 16, true), (p.AutosaveMinutes, p.UndoHistory, p.DefaultSnap, p.OpenLastProjectOnStart));
        fx.Services.Preferences.Update(x => { x.Theme = ThemeMode.Light; x.UiScalePercent = 125; x.Accent = "#B392FF"; });
        var again = new PreferencesStore(fx.Services.Preferences.FilePath).Current;
        Assert.Equal((ThemeMode.Light, 125, "#B392FF"), (again.Theme, again.UiScalePercent, again.Accent));
        File.WriteAllText(fx.Services.Preferences.FilePath, "{ broken");
        Assert.Equal(ThemeMode.Dark, new PreferencesStore(fx.Services.Preferences.FilePath).Current.Theme);
    }

    [Fact]
    public void ProjectSettings_AreStoredPerProjectInTheAgbproj()
    {
        using var fx = new Fixture(withApp: false);
        var root = Path.Combine(fx.Folder, "Proj");
        fx.Services.Project.Create(root, "Sky Hopper");
        var store = fx.Services.ProjectSettings;
        Assert.EndsWith("SkyHopper.agbproj", store.Location);
        store.Settings.Name = "Sky Hopper DX";
        store.Save();
        Assert.Equal("Sky Hopper DX", GameProject.Open(root).Settings.Name);
        // user preferences are not touched
        Assert.False(File.Exists(fx.Services.Preferences.FilePath) && File.ReadAllText(fx.Services.Preferences.FilePath).Contains("Sky Hopper DX"));
        fx.Services.Project.Close();
        Assert.IsType<MemoryProjectSettingsStore>(fx.Services.ProjectSettings);
    }

    [Fact]
    public void Undo_RespectsHistoryLimitFromPreferences()
    {
        using var fx = new Fixture(withApp: false);
        var undo = fx.Services.Undo;
        var value = 0;
        for (var i = 1; i <= 250; i++)
        {
            var (from, to) = (value, i);
            undo.Do(new DelegateAction("set " + i, () => value = from, () => value = to));
        }
        Assert.Equal(200, undo.UndoCount);
        while (undo.Undo()) { }
        Assert.Equal(50, value);
        Assert.True(undo.Redo());
        Assert.Equal(51, value);
        undo.Push(new DelegateAction("x", () => { }, () => { }));
        Assert.False(undo.CanRedo);
        for (var i = 0; i < 50; i++) undo.Push(new DelegateAction("y", () => { }, () => { }));
        fx.Services.Preferences.Update(p => p.UndoHistory = 10);
        undo.Trim();
        Assert.Equal(10, undo.UndoCount);
    }

    [Fact]
    public async Task Autosave_SavesDirtyDocumentsEveryNMinutes()
    {
        using var fx = new Fixture(withApp: false);
        var saved = new List<string>();
        var doc = fx.Services.Documents.Add(new EditorDocument { Name = "Forest_01.scene", SaveHandler = d => { saved.Add(d.Name); return Task.CompletedTask; } });
        var autosave = fx.Services.Autosave;
        autosave.Refresh();
        Assert.True(fx.Timer.IsRunning);
        Assert.Equal(TimeSpan.FromMinutes(2), fx.Timer.Interval);
        fx.Timer.Fire();
        await Task.Yield();
        Assert.Empty(saved);
        doc.IsDirty = true;
        fx.Timer.Fire();
        await Task.Delay(10);
        Assert.Equal(["Forest_01.scene"], saved);
        Assert.False(doc.IsDirty);
        Assert.Equal(1, autosave.LastSavedCount);
        fx.Services.Preferences.Update(p => p.AutosaveMinutes = 0);
        autosave.Refresh();
        Assert.False(fx.Timer.IsRunning);
    }

    [Fact]
    public void Recent_OpenRecentClearAndTimes()
    {
        using var fx = new Fixture(withApp: false);
        var r = fx.Services.Recent;
        fx.Now = new DateTime(2026, 10, 24, 9, 0, 0); r.Touch("Dusk Novel", Path.Combine(fx.Folder, "a.agbproj"));
        fx.Now = new DateTime(2026, 10, 28, 9, 0, 0); r.Touch("Snow Run", Path.Combine(fx.Folder, "b.agbproj"));
        fx.Now = new DateTime(2026, 10, 30, 9, 0, 0); r.Touch("Cave Quest RPG", Path.Combine(fx.Folder, "c.agbproj"));
        fx.Now = new DateTime(2026, 10, 31, 10, 0, 0); r.Touch("Sky Hopper", Path.Combine(fx.Folder, "d.agbproj"));
        fx.Now = new DateTime(2026, 10, 31, 12, 0, 0);
        Assert.Equal(["Sky Hopper", "Cave Quest RPG", "Snow Run", "Dusk Novel"], r.Items.Select(p => p.Name));
        Assert.Equal(["2 h ago", "yesterday", "3 days ago", "last week"], r.Items.Select(r.Ago));
        r.Touch("Snow Run", Path.Combine(fx.Folder, "b.agbproj"));
        Assert.Equal("Snow Run", r.Items[0].Name);
        var again = new RecentProjectsStore(r.FilePath);
        Assert.Equal(4, again.Items.Count);
        r.Clear();
        Assert.Empty(new RecentProjectsStore(r.FilePath).Items);
    }

    [Fact]
    public void Toolchain_ReportsFoundAndNotFoundWithFakeFolders()
    {
        using var fx = new Fixture(withApp: false);
        var dkp = Path.Combine(fx.Folder, "devkitPro");
        foreach (var f in new[] { @"libtonc\lib\libtonc.a", @"libgba\lib\libmm.a", @"tools\bin\gbafix.exe" })
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(dkp, f))!);
            File.WriteAllText(Path.Combine(dkp, f), "");
        }
        var svc = new ToolchainStatusService(() => dkp);
        var tools = svc.CheckNow();
        Assert.True(svc.Find("devkitPro")!.Found);
        Assert.True(svc.Find("libtonc")!.Found);
        Assert.True(svc.Find("maxmod")!.Found);
        Assert.True(svc.Find("gbafix")!.Found);
        Assert.False(svc.Find("devkitARM")!.Found);
        Assert.False(svc.IsComplete);
        Assert.False(new ToolchainStatusService(() => Path.Combine(fx.Folder, "missing")).CheckNow().First(t => t.Name == "devkitPro").Found);
        Assert.NotEmpty(tools);
    }
}
