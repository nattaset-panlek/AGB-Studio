using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;

namespace AgbStudio.Editor.Tests.Support;

/// <summary>A manual timer for autosave tests.</summary>
public sealed class ManualTimer : IRepeatingTimer
{
    public TimeSpan Interval { get; set; }
    public bool IsRunning { get; private set; }
    public event Action? Tick;
    public void Start() => IsRunning = true;
    public void Stop() => IsRunning = false;
    public void Fire() { if (IsRunning) Tick?.Invoke(); }
}

/// <summary>Temporary folders and editor services for tests; the "Sky Hopper" state shown in the Figma Scene Editor.</summary>
public sealed class Fixture : IDisposable
{
    public string Folder { get; } = Directory.CreateTempSubdirectory("agb-editor-test-").FullName;
    public ManualTimer Timer { get; } = new();
    public DateTime Now { get; set; } = new(2026, 10, 31, 12, 0, 0);
    public EditorServices Services { get; }

    public Fixture(bool withApp = true)
    {
        Services = EditorServices.Create(withApp ? Avalonia.Application.Current : null, Path.Combine(Folder, "appdata"), Timer, () => Now);
    }

    /// <summary>Creates and opens the Sky Hopper project with the documents, status and files of the Figma Scene Editor.</summary>
    public string OpenSkyHopper()
    {
        var root = Path.Combine(Folder, "SkyHopper");
        Services.Project.Create(root, "Sky Hopper");
        // the files of the Figma Command Palette, in the folders AgbStudio.Core creates
        foreach (var file in new[] { @"assets\sprites\player.png", @"scripts\PlayerController.cs", @"assets\tilesets\Platform_tiles.png" })
            File.WriteAllText(Path.Combine(root, file), "");
        // files opened most recently first, as the Figma Command Palette lists them
        var history = new PaletteHistory();
        foreach (var f in new[] { "Platform_tiles.png", "PlayerController.cs", "player.png" })
            history.Touch(Directory.EnumerateFiles(root, f, SearchOption.AllDirectories).Single());
        Services.Set(history);
        var d = Services.Documents;
        d.Add(new EditorDocument { Name = "Scene", Glyph = "map", Path = Path.Combine(root, "scenes", "Forest_01.scene") }, activate: true);
        d.Add(new EditorDocument { Name = "Game", Glyph = "pad" });
        d.Add(new EditorDocument { Name = "Forest_01.map", Glyph = "tiles", Path = Path.Combine(root, "maps", "Forest_01.map") });
        d.Add(new EditorDocument { Name = "PlayerController.cs", Glyph = "code", Path = Path.Combine(root, "scripts", "PlayerController.cs") });
        var s = Services.Status;
        s.Build = "Build OK · 0.8 s";
        s.Rom = "ROM 412 KB / 32 MB";
        s.Performance = "59.7 fps · CPU 38%";
        s.ToolchainOk = true;
        return root;
    }

    public void Dispose()
    {
        try { Directory.Delete(Folder, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
