using System.Text.Json;
using AgbStudio.Emu;
using AgbStudio.Mcp.Protocol;

namespace AgbStudio.Mcp.Tools;

public sealed class StartSimulationTool : IMcpToolHandler
{
    #region [Properties]
    public string Name => "agb_start_simulation";
    public string Description => "Boots the built GBA ROM in the embedded mGBA emulation core.";
    public McpToolInputSchema InputSchema => new()
    {
        Properties = new()
        {
            ["romPath"] = new McpPropertySchema { Type = "string", Description = "Optional path to .gba ROM. If omitted, uses active project's built ROM." }
        }
    };
    #endregion

    #region [Public Methods]
    public Task<McpToolResult> ExecuteAsync(McpContext context, JsonElement? arguments)
    {
        var romPath = ToolHelpers.GetString(arguments, "romPath");
        try
        {
            var session = context.StartSimulation(romPath);
            var result = new
            {
                success = true,
                rom = session.RomPath,
                running = session.Running,
                frame = session.Frame
            };
            return Task.FromResult(McpToolResult.Json(result));
        }
        catch (Exception ex)
        {
            return Task.FromResult(McpToolResult.Text($"Failed to start simulation: {ex.Message}", isError: true));
        }
    }
    #endregion
}

public sealed class StepSimulationTool : IMcpToolHandler
{
    #region [Properties]
    public string Name => "agb_step_simulation";
    public string Description => "Steps the running mGBA simulation by a specified number of frames (1 to 600) and returns current frame stats.";
    public McpToolInputSchema InputSchema => new()
    {
        Properties = new()
        {
            ["frames"] = new McpPropertySchema { Type = "number", Description = "Number of frames to step (default: 1, max: 600)" }
        }
    };
    #endregion

    #region [Public Methods]
    public Task<McpToolResult> ExecuteAsync(McpContext context, JsonElement? arguments)
    {
        if (!context.IsSimulating || context.Session == null)
            return Task.FromResult(McpToolResult.Text("Simulation is not running. Call 'agb_start_simulation' first.", isError: true));

        var frames = ToolHelpers.GetInt(arguments, "frames") ?? 1;
        var stepped = context.StepSimulation(frames);

        var s = context.Session;
        var profiler = s.Profiler();
        var result = new
        {
            steppedFrames = stepped,
            currentFrame = s.Frame,
            running = s.Running,
            cpuPercent = profiler?.CpuPercent ?? 0,
            busyMs = profiler?.BusyMs ?? 0,
            logLines = s.Log.TakeLast(10).ToList()
        };

        return Task.FromResult(McpToolResult.Json(result));
    }
    #endregion
}

public sealed class CaptureScreenTool : IMcpToolHandler
{
    #region [Properties]
    public string Name => "agb_capture_screen";
    public string Description => "Captures the current 240x160 GBA frame buffer as a Base64-encoded PNG image for visual inspection.";
    public McpToolInputSchema InputSchema => new();
    #endregion

    #region [Public Methods]
    public Task<McpToolResult> ExecuteAsync(McpContext context, JsonElement? arguments)
    {
        if (context.Core == null)
            return Task.FromResult(McpToolResult.Text("Simulation core is not running.", isError: true));

        try
        {
            var base64 = context.CaptureScreenBase64();
            var frame = context.Session?.Frame ?? 0;
            return Task.FromResult(McpToolResult.Image(base64, $"GBA Screen at Frame {frame} (240x160)"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(McpToolResult.Text($"Failed to capture screen: {ex.Message}", isError: true));
        }
    }
    #endregion
}

public sealed class ReadMemoryTool : IMcpToolHandler
{
    #region [Properties]
    public string Name => "agb_read_memory";
    public string Description => "Reads raw bytes from GBA memory (EWRAM 0x02000000, IWRAM 0x03000000, VRAM 0x06000000, OAM 0x07000000).";
    public McpToolInputSchema InputSchema => new()
    {
        Properties = new()
        {
            ["address"] = new McpPropertySchema { Type = "string", Description = "Hexadecimal or decimal memory address (e.g. '0x02000000' or '33554432')" },
            ["length"] = new McpPropertySchema { Type = "number", Description = "Number of bytes to read (max: 4096, default: 64)" }
        },
        Required = ["address"]
    };
    #endregion

    #region [Public Methods]
    public Task<McpToolResult> ExecuteAsync(McpContext context, JsonElement? arguments)
    {
        if (context.Core == null)
            return Task.FromResult(McpToolResult.Text("Simulation core is not running.", isError: true));

        var addrStr = ToolHelpers.GetString(arguments, "address");
        if (string.IsNullOrWhiteSpace(addrStr))
            return Task.FromResult(McpToolResult.Text("Error: 'address' is required.", isError: true));

        uint address;
        if (addrStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            address = Convert.ToUInt32(addrStr[2..], 16);
        else
            address = uint.Parse(addrStr);

        var length = Math.Clamp(ToolHelpers.GetInt(arguments, "length") ?? 64, 1, 4096);

        try
        {
            var bytes = context.Core.Memory(address, length).ToArray();
            var hex = Convert.ToHexString(bytes);
            return Task.FromResult(McpToolResult.Json(new
            {
                address = $"0x{address:X8}",
                length = bytes.Length,
                hex,
                bytes = bytes.ToList()
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(McpToolResult.Text($"Failed to read memory: {ex.Message}", isError: true));
        }
    }
    #endregion
}

public sealed class ReadLiveActorsTool : IMcpToolHandler
{
    #region [Properties]
    public string Name => "agb_read_live_actors";
    public string Description => "Reads live actor telemetry (names, positions, velocities, onGround states) directly from the running game.";
    public McpToolInputSchema InputSchema => new();
    #endregion

    #region [Public Methods]
    public Task<McpToolResult> ExecuteAsync(McpContext context, JsonElement? arguments)
    {
        if (!context.IsSimulating || context.Session == null)
            return Task.FromResult(McpToolResult.Text("Simulation is not running.", isError: true));

        var actors = context.Session.Actors();
        var list = actors.Select(a => new
        {
            name = a.Name,
            x = a.X,
            y = a.Y,
            vx = a.Vx,
            vy = a.Vy,
            alive = a.Alive,
            onGround = a.OnGround,
            visible = a.Visible,
            facingLeft = a.FacingLeft,
            state = a.State
        }).ToList();

        return Task.FromResult(McpToolResult.Json(new { total = list.Count, actors = list }));
    }
    #endregion
}

public sealed class SendInputTool : IMcpToolHandler
{
    #region [Properties]
    public string Name => "agb_send_input";
    public string Description => "Simulates pressing buttons on the GBA joypad (A, B, Up, Down, Left, Right, Start, Select, L, R).";
    public McpToolInputSchema InputSchema => new()
    {
        Properties = new()
        {
            ["buttons"] = new McpPropertySchema { Type = "string", Description = "Comma-separated list of buttons to press (e.g. 'A', 'Right', 'Start', or 'None' to release)" }
        },
        Required = ["buttons"]
    };
    #endregion

    #region [Public Methods]
    public Task<McpToolResult> ExecuteAsync(McpContext context, JsonElement? arguments)
    {
        if (!context.IsSimulating || context.Session == null)
            return Task.FromResult(McpToolResult.Text("Simulation is not running.", isError: true));

        var buttons = ToolHelpers.GetString(arguments, "buttons") ?? "";
        GbaKeys keys = GbaKeys.None;
        foreach (var part in buttons.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Enum.TryParse<GbaKeys>(part, true, out var k))
                keys |= k;
        }

        context.SendInput(keys);
        return Task.FromResult(McpToolResult.Json(new { success = true, keys = keys.ToString() }));
    }
    #endregion
}

public sealed class StopSimulationTool : IMcpToolHandler
{
    #region [Properties]
    public string Name => "agb_stop_simulation";
    public string Description => "Stops the active GBA emulation session and releases emulator resources.";
    public McpToolInputSchema InputSchema => new();
    #endregion

    #region [Public Methods]
    public Task<McpToolResult> ExecuteAsync(McpContext context, JsonElement? arguments)
    {
        context.StopSimulation();
        return Task.FromResult(McpToolResult.Json(new { success = true, running = false }));
    }
    #endregion
}
