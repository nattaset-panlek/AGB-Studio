using AgbStudio.Build.Content;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Tests.Support;

namespace AgbStudio.Editor.Tests.Windows.Settings;

/// <summary>
/// The Figma state of the settings frames: the Sky Hopper fixture project (AgbStudio.Build.Content.SkyHopper, real
/// scenes, fonts and string tables) with the values the Figma frames show where the fixture differs.
/// </summary>
public static class SettingsFixture
{
    /// <summary>Creates Sky Hopper in the fixture folder and opens it in the editor services.</summary>
    public static GameProject OpenFigmaSkyHopper(this Fixture fx)
    {
        var dir = Path.Combine(fx.Folder, "SkyHopper");
        SkyHopper.Create(dir);
        var p = fx.Services.Project.Open(dir);
        var s = p.Settings;
        // Figma Project Settings · Input: L = A, R = S (InputAction.Defaults has S / D)
        s.Input.First(a => a.Button == "L").Keyboard = "A";
        s.Input.First(a => a.Button == "R").Keyboard = "S";
        s.AllowRemap = true;
        // SkyHopper.Create adds its languages to the default English entry, so "en" is listed twice; keep one of each
        s.Localization.Languages = s.Localization.Languages.GroupBy(l => l.Code).Select(g => g.Last()).ToList();
        // Figma Build: Release; Scripting: GAE003 Error; Localization: English pixel8, Japanese none
        s.Build.Configuration = BuildConfiguration.Release;
        s.Scripting.AnalyzerRules["GAE003"] = "Error";
        s.Localization.Languages.First(l => l.Code == "en").Font = "pixel8";
        s.Localization.Languages.First(l => l.Code == "ja").Font = "";
        return p;
    }
}
