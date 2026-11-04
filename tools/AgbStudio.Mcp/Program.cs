using System.Text;

namespace AgbStudio.Mcp;

public static class Program
{
    #region [Public Methods]
    public static async Task<int> Main(string[] args)
    {
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;

        string? projectPath = null;
        for (var i = 0; i < args.Length; i++)
        {
            if ((args[i] == "--project" || args[i] == "-p") && i + 1 < args.Length)
            {
                projectPath = args[i + 1];
                i++;
            }
        }

        using var context = new McpContext();
        if (!string.IsNullOrWhiteSpace(projectPath))
        {
            try
            {
                context.OpenProject(projectPath);
                Console.Error.WriteLine($"[AgbStudio.Mcp] Opened project: {context.CurrentProject?.Settings.Name} ({projectPath})");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[AgbStudio.Mcp] Warning: Failed to open project '{projectPath}': {ex.Message}");
            }
        }

        using var server = new McpServer(context);
        server.Registry.RemoveDisabled(McpSettings.Load());
        using var cts = new CancellationTokenSource();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            server.Stop();
        };

        try
        {
            await server.RunAsync(cts.Token);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[AgbStudio.Mcp] Fatal error: {ex.Message}");
            return 1;
        }
    }
    #endregion
}
