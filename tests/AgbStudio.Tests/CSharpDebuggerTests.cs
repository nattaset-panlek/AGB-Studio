using AgbStudio.Build;
using AgbStudio.Emu;

namespace AgbStudio.Tests;

[Collection("emulator")]
public sealed class CSharpDebuggerTests(EmulatorFixture emu)
{
    [Fact]
    public void BreakpointPausesWithCallStackAndResumes()
    {
        var r = RuntimeTests.BuildAll(["host_probe"]);
        Assert.True(r.Success, r.Log);
        var s = new PlaySession(emu.Core);
        s.Start(RuntimeTests.RomPath("host_probe"));
        for (var i = 0; i < 120 && !(s.Log.Contains("probe test ready")); i++) s.Step();
        for (var i = 0; i < 10; i++) s.Step();
        var dbg = new CSharpDebugger(s.Core, s.Channel!);
        Assert.True(dbg.Available);
        dbg.SetBreakpoints([(1, 30)]);
        for (var i = 0; i < 5 && !dbg.Paused; i++) s.Step();
        Assert.True(dbg.Paused);
        Assert.Equal((1, 30), dbg.PausedAt);
        var stack = dbg.Stack();
        Assert.Equal(["Jump()", "Update()", "Frame()"], stack.Select(f => f.Method).ToArray());
        Assert.Equal("PlayerController.cs", stack[0].File);
        Assert.Equal(30, stack[0].Line);
        Assert.Equal(19, stack[1].Line);
        Assert.Contains(s.Log, l => l == "breakpoint: PlayerController.cs:30");
        // Still paused a few frames later: the game is stopped inside the statement.
        var frame = s.Channel!.GameFrame;
        for (var i = 0; i < 5; i++) s.Step();
        Assert.True(dbg.Paused);
        Assert.Equal(frame, s.Channel!.GameFrame);
        // Step goes to the next probed line (21 in Update).
        dbg.Step();
        for (var i = 0; i < 3 && dbg.PausedAt.Line != 21; i++) s.Step();
        Assert.Equal((1, 21), dbg.PausedAt);
        dbg.SetBreakpoints([]);
        dbg.Resume();
        for (var i = 0; i < 5; i++) s.Step();
        Assert.False(dbg.Paused);
        Assert.True(s.Channel!.GameFrame > frame);
        s.Stop();
    }
}
