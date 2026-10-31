using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Tests.Prototype;
using AgbStudio.Editor.Tests.Support;
using Avalonia.Headless.XUnit;

namespace AgbStudio.Editor.Tests.Windows.Build;

/// <summary>Play and Build ROM are greyed out while a build runs and come back as soon as it ends (P10.12, T-D06).</summary>
[Collection(EmulatorCollection.Name)]
public class BuildEndsTests
{
    [AvaloniaFact]
    public void PlayAndBuildRom_AreEnabledAgainTheMomentTheBuildEnds()
    {
        using var s = new AppSession();
        var play = s.S.Commands.Get("build.play");
        var buildRom = s.S.Commands.Get("build.buildRom");
        Assert.True(play.CanExecute() && buildRom.CanExecute());
        // Debug starts Play from Here: a build (held in progress by the session) while the Debugger shows
        s.Click(s.Named<AgbButton>("PART_Debug"));
        Assert.Equal("T-P19", s.Win.CurrentPageId);
        Assert.True(s.S.Build.IsBuilding);
        s.ShowPage("T-P01");
        var button = s.Named<PlayControl>("PlayButton");
        Assert.False(button.IsEffectivelyEnabled);
        Assert.False(buildRom.CanExecute());
        // the build ends: the Play button and the Build button of the title bar work again without another click
        s.S.Build.Cancel();
        AppSession.Pump();
        Assert.False(s.S.Build.IsBuilding);
        Assert.True(button.IsEffectivelyEnabled);
        Assert.True(s.Named<AgbButton>("PART_Build").IsEffectivelyEnabled);
    }
}
