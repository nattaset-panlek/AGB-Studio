namespace AgbStudio.Editor.Services;

/// <summary>
/// The order in which the user added project files (Sprites list, Scripts list, tables, conversations): by creation
/// time, files created at the same time (one copy, one generated project) by name, so the order never depends on how
/// the file system lists them. Files written within one clock tick share a time and fall back to name order, so a
/// generated project whose lists must keep an order (the Sky Hopper fixture's Figma lists) gives its files distinct
/// creation times.
/// </summary>
public static class CreationOrder
{
    public static IEnumerable<string> Of(IEnumerable<string> files, StringComparer? names = null) =>
        files.Select(f => (File: f, Time: File.GetCreationTimeUtc(f))).OrderBy(x => x.Time).ThenBy(x => x.File, names ?? StringComparer.OrdinalIgnoreCase)
            .Select(x => x.File).ToList();
}
