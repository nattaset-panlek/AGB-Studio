using System.Diagnostics;
using System.Text;

namespace AgbStudio.Build;

public sealed record ProcResult(int ExitCode, string Output);

/// <summary>Runs a process and captures stdout + stderr in order.</summary>
public static class Proc
{
    public static ProcResult Run(string exe, string args, string? workDir, TimeSpan timeout, Action<string>? onLine = null, CancellationToken cancel = default)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workDir ?? Environment.CurrentDirectory,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        var sb = new StringBuilder();
        using var p = new Process { StartInfo = psi };
        void Add(string? line)
        {
            if (line == null) return;
            lock (sb) sb.AppendLine(line);
            onLine?.Invoke(line);
        }
        p.OutputDataReceived += (_, e) => Add(e.Data);
        p.ErrorDataReceived += (_, e) => Add(e.Data);
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        var deadline = DateTime.UtcNow + timeout;
        while (!p.WaitForExit(50))
        {
            if (cancel.IsCancellationRequested || DateTime.UtcNow > deadline)
            {
                try { p.Kill(true); } catch (InvalidOperationException) { }
                p.WaitForExit();
                Add(cancel.IsCancellationRequested ? "cancelled" : "timed out");
                return new ProcResult(-1, sb.ToString());
            }
        }
        p.WaitForExit();
        return new ProcResult(p.ExitCode, sb.ToString());
    }
}
