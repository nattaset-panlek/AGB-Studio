using System.Globalization;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Support;
using Avalonia.Headless.XUnit;
using SkiaSharp;
using Xunit.Abstractions;

namespace AgbStudio.Editor.Tests.Windows.Sprite;

/// <summary>SHOT of this window group alone (the full run is ShotTests): renders each id, writes app / diff PNGs and checks the layout offset.</summary>
public class SpriteShotTests(ITestOutputHelper log)
{
    [AvaloniaFact]
    public void Shot_SpriteAndPaletteWindows()
    {
        foreach (var (id, render) in new SpriteShots().Windows())
        {
            using var figma = SKBitmap.Decode(Path.Combine(Repo.FigmaEvidence, id + ".png"));
            var app = Path.Combine(Repo.Evidence, "app", id + ".png");
            render(app, figma.Width, figma.Height);
            var r = ImageDiff.Compare(id, Path.Combine(Repo.FigmaEvidence, id + ".png"), app, Path.Combine(Repo.Evidence, "diff", id + ".png"), SKColors.Black);
            log.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{id}: diff {r.DiffPercent:0.00} %, best shift {r.BestShiftDiffPercent:0.00} % at {r.ShiftX},{r.ShiftY}, offset {r.LayoutOffset} px"));
            Assert.True(r.LayoutOffset <= 2, $"{id} layout offset {r.LayoutOffset} px");
        }
    }
}
