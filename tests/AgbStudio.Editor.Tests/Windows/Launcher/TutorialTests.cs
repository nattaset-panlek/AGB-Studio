using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Support;
using AgbStudio.Editor.Windows.Launcher;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.Launcher;

/// <summary>Tutorial Steps (P11.04): "Your first platformer" step 3 ticks itself from editor actions; Back / Skip / Next step.</summary>
[Collection(EmulatorCollection.Name)]
public class TutorialTests
{
    [AvaloniaFact]
    public void P11_04_FirstPlatformerStep3_SubStepsTickThemselves_BackSkipNext()
    {
        using var h = new LauncherHarness(startPage: null, recent: false);
        h.Fx.OpenSkyHopper();
        h.Show("T-P01");
        var progress = h.Launcher.Tutorials.Get("first-platformer");
        progress.Started = true;
        progress.Step = 2;
        progress.Done = ["0.0", "0.1", "0.2", "1.0", "1.1", "1.2"];
        h.Launcher.Tutorials.Save();
        h.Launcher.TutorialHost.Show(TutorialCatalog.All[0], "test");
        Capture.Pump();

        // shown beside the editor, not as a modal overlay: the Scene Editor stays usable
        var view = h.Find<TutorialStepsPopover>("TutorialSteps");
        Assert.False(h.Win.Overlays.IsOpen);
        Assert.Equal("Your first platformer · 3 of 8", view.Title);
        Assert.Contains("Make the player jump", h.Texts(view));
        Assert.Contains("Select Player, then in the Inspector raise Jump force to about 3.5. Press Play and try the A button (Z on your keyboard).", h.Texts(view));
        Assert.Equal(["Select Player in the Hierarchy", "Set Jump force to 3.5", "Press Play and jump"], view.SubStepBoxes.Select(b => (string)b.Content!));
        Assert.All(view.SubStepBoxes, b => Assert.False(b.IsChecked));
        Assert.Equal(30, h.Find<AgbProgress>("TutorialProgress").Value);

        // 1. the Hierarchy selects Player (any panel built from Tree Items; stand-ins are placed in the window here)
        var layer = (Panel)h.Win.Overlays.Parent!;
        var hierarchy = new StackPanel { Width = 240, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top, Margin = new Avalonia.Thickness(10, 200, 0, 0) };
        layer.Children.Insert(layer.Children.IndexOf(h.Win.Overlays), hierarchy);
        var other = new TreeItem { Label = "Slime" };
        var player = new TreeItem { Label = "Player" };
        foreach (var t in new[] { other, player })
        {
            var item = t;
            item.Activated += (_, _) => item.IsSelected = true;
        }
        hierarchy.Children.AddRange([other, player]);
        Capture.Pump();
        h.Click(other);
        Assert.False(view.SubStepBoxes[0].IsChecked);
        h.Click(player);
        Assert.True(view.SubStepBoxes[0].IsChecked);
        Assert.False(view.SubStepBoxes[1].IsChecked);

        // 2. the Inspector shows Jump force 3.50
        var jump = new InspectorField { Kind = FieldKind.Slider, Label = "Jump force", Value = "2.80" };
        hierarchy.Children.Add(jump);
        Capture.Pump();
        jump.Value = "3.20";
        Assert.False(view.SubStepBoxes[1].IsChecked);
        jump.Value = "3.50";
        Capture.Pump();
        Assert.True(view.SubStepBoxes[1].IsChecked);

        // 3. Play, then the key the project maps to A (Z) jumps; before Play the key does nothing
        h.Key(PhysicalKey.Z);
        Assert.False(view.SubStepBoxes[2].IsChecked);
        h.S.Commands.Execute("build.play");
        Capture.Pump();
        h.Key(PhysicalKey.Escape);   // Play Mode is another group's window; its notice closes
        h.Key(PhysicalKey.X);        // B (Run) is not the jump
        Assert.False(view.SubStepBoxes[2].IsChecked);
        h.Key(PhysicalKey.Z);
        Assert.True(view.SubStepBoxes[2].IsChecked);
        Assert.Equal(45, h.Find<AgbProgress>("TutorialProgress").Value);
        Assert.Equal(["0.0", "0.1", "0.2", "1.0", "1.1", "1.2", "2.0", "2.1", "2.2"], h.Launcher.Tutorials.Get("first-platformer").Done);

        // Next step / Back / Skip
        h.Click(h.Find<AgbButton>("TutorialNext"));
        Assert.Equal("Your first platformer · 4 of 8", view.Title);
        Assert.Contains("Add a coin", h.Texts(view));
        h.Click(h.Find<AgbButton>("TutorialBack"));
        Assert.Equal("Your first platformer · 3 of 8", view.Title);
        Assert.All(view.SubStepBoxes, b => Assert.True(b.IsChecked));
        h.Click(h.Find<AgbButton>("TutorialNext"));
        h.Click(h.Find<AgbButton>("TutorialSkip"));
        Assert.Equal("Your first platformer · 5 of 8", view.Title);
        Assert.Contains("3.0", h.Launcher.Tutorials.Get("first-platformer").Done);
        Assert.Contains("3.1", h.Launcher.Tutorials.Get("first-platformer").Done);

        // step 5: opening the script ticks "Open PlayerController.cs"; a command of another step does not
        h.S.Commands.Execute("scene.toggleGrid");
        Assert.All(view.SubStepBoxes, b => Assert.False(b.IsChecked));
        _ = h.S.Windows.Open("T-P04", source: "test");
        Capture.Pump();
        Assert.True(view.SubStepBoxes[0].IsChecked);
        h.Key(PhysicalKey.Escape);

        // the X closes Tutorial Steps; the progress stays and the Learn page offers Continue
        view.GetVisualDescendants().OfType<PopoverHeader>().Single().RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(PopoverHeader.CloseRequestedEvent));
        Capture.Pump();
        Assert.Null(h.TryFind<TutorialStepsPopover>("TutorialSteps"));
        Assert.False(h.Launcher.TutorialHost.Session.IsActive);
        Assert.Equal(("60% · in progress", false, "Continue"), h.Launcher.Tutorials.Row(TutorialCatalog.All[0]));
        var reloaded = new TutorialProgressStore(h.Launcher.Tutorials.FilePath);
        Assert.Equal(4, reloaded.Get("first-platformer").Step);
    }

    [AvaloniaFact]
    public void P11_04_FinishingTheLastStepMarksTheTutorialDone()
    {
        using var h = new LauncherHarness(startPage: "T-P21", recent: false);
        h.Click(h.Find<AgbButton>("Tutorial_palettes"));
        var view = h.Find<TutorialStepsPopover>("TutorialSteps");
        Assert.False(h.Find<AgbButton>("TutorialBack").IsEnabled);
        _ = h.S.Windows.Open("T-P10", source: "test");   // Palette Manager
        Capture.Pump();
        h.Key(PhysicalKey.Escape);
        Assert.True(view.SubStepBoxes[0].IsChecked);
        h.Click(h.Find<AgbButton>("TutorialNext"));
        Assert.Equal("Palettes & VRAM · 2 of 2", view.Title);
        h.Click(h.Find<AgbButton>("TutorialNext"));
        Assert.Null(h.TryFind<TutorialStepsPopover>("TutorialSteps"));
        Assert.Equal(("Done", true, "Review"), h.Launcher.Tutorials.Row(TutorialCatalog.Find("palettes")!));
        Assert.Equal(100, h.Launcher.Tutorials.Percent(TutorialCatalog.Find("palettes")!));
    }

    [AvaloniaFact]
    public void P11_04_OpenedThroughTheWindowServiceItShowsTheTutorial()
    {
        using var h = new LauncherHarness(startPage: "T-P12", recent: false);
        _ = h.S.Windows.Open("T-O13", "sound", source: "test");
        Capture.Pump();
        var view = Assert.IsType<TutorialStepsPopover>(h.Win.Overlays.Open.Single());
        Assert.Equal("Sound & music · 1 of 3", view.Title);
        Assert.Equal(380, view.Bounds.Width);
    }
}
