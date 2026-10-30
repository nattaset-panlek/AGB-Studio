using System.Collections.ObjectModel;
using System.Globalization;
using AgbStudio.Editor.Controls;

namespace AgbStudio.Editor.Windows.Play;

/// <summary>One Console line: local time, level and text (Figma "Console Line").</summary>
public sealed record ConsoleEntry(DateTime Time, ConsoleLevel Level, string Text)
{
    public string TimeText => Time.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
}

/// <summary>
/// The Play Mode console (P12.07): build results, engine logs and Debug.Log lines from the running game, each with the
/// time it arrived. Shared through <c>Services.Get&lt;PlayConsole&gt;()</c> so the Console panel (T-P05) can show the same lines.
/// </summary>
public sealed class PlayConsole
{
    public const int MaxLines = 500;

    public ObservableCollection<ConsoleEntry> Entries { get; } = [];
    public Func<DateTime> Clock { get; set; } = () => DateTime.Now;
    public event Action<ConsoleEntry>? Added;

    public int Warnings => Entries.Count(e => e.Level == ConsoleLevel.Warning);
    public int Errors => Entries.Count(e => e.Level == ConsoleLevel.Error);

    public ConsoleEntry Add(ConsoleLevel level, string text)
    {
        var e = new ConsoleEntry(Clock(), level, text);
        Entries.Add(e);
        while (Entries.Count > MaxLines) Entries.RemoveAt(0);
        Added?.Invoke(e);
        return e;
    }

    /// <summary>A line from the game's debug log: "warning: ..." is a warning, failed asserts are errors, the rest (Debug.Log, engine) is info.</summary>
    public ConsoleEntry AddGameLine(string line)
    {
        if (line.StartsWith("warning: ", StringComparison.Ordinal)) return Add(ConsoleLevel.Warning, line[9..]);
        if (line.StartsWith("assert failed", StringComparison.Ordinal) || line.StartsWith("TEST FAIL", StringComparison.Ordinal) || line.StartsWith("error: ", StringComparison.Ordinal))
            return Add(ConsoleLevel.Error, line.StartsWith("error: ", StringComparison.Ordinal) ? line[7..] : line);
        return Add(ConsoleLevel.Info, line);
    }

    public void Clear() => Entries.Clear();
}
