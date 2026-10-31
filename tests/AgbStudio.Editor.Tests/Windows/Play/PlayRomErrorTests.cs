using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Tests.Support;
using AgbStudio.Editor.Windows.Play;
using Avalonia.Headless.XUnit;

namespace AgbStudio.Editor.Tests.Windows.Play;

/// <summary>G-ERR: Run in mGBA with a file the emulator core refuses reports it and leaves the editor able to stop and close.</summary>
[Collection(EmulatorCollection.Name)]
public class PlayRomErrorTests
{
    [AvaloniaFact]
    public void Run_AFileTheCoreRefuses_ReportsIt_AndStopAndCloseStillWork()
    {
        var fx = new Fixture();
        try
        {
            fx.OpenSkyHopper();
            var play = PlayController.Of(fx.Services);
            play.UseTimer = false;
            play.AudioFactory = () => new CountingSink();
            play.Gamepad = () => null;
            // a .gba of zeros has no GBA header: mGBA does not load it
            var rom = Path.Combine(fx.Folder, "broken.gba");
            File.WriteAllBytes(rom, new byte[64 * 1024]);
            Assert.False(play.Run(rom));
            Assert.Null(play.Session);
            Assert.False(play.IsRunning);
            Assert.Equal(PlayState.Stopped, play.State);
            Assert.Contains(play.Console.Entries, e => e.Level == ConsoleLevel.Error && e.Text.StartsWith("broken.gba: ", StringComparison.Ordinal));
            Assert.Contains(fx.Services.Toasts.Visible, t => t.Text == "Could not run broken.gba · see Console" && t.Status == ToastStatus.Error);
            // Stop and the editor closing (Dispose) used to ask the core for save RAM with no game loaded and crash
            play.Stop();
            Assert.Null(play.Session);
        }
        finally { fx.Dispose(); }
    }
}
