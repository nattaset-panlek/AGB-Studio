using AgbStudio.Build;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Tests.Support;
using AgbStudio.Editor.Tests.Windows.Build;
using AgbStudio.Editor.Windows.Import;
using Avalonia.Headless.XUnit;

namespace AgbStudio.Editor.Tests.Windows.Import;

/// <summary>P7.02: "Drop PNG / Aseprite here": an Aseprite file is detected as a sprite and imported for the build.</summary>
public class AsepriteImportTests
{
    [AvaloniaFact]
    public void Aseprite_IsASpriteWithItsFrames_AndImportsForTheBuild()
    {
        using var fx = new Fixture();
        var p = GroupFixture.SkyHopper(fx);
        // tests/AgbStudio.Tests/Golden/slime.aseprite: 16 x 12 canvas, 6 frames (one empty), two layers and a hidden one, 2 tags.
        var src = Path.Combine(fx.Folder, "jelly.aseprite");
        File.Copy(Path.Combine(NativeBuild.RepoRoot, "tests", "AgbStudio.Tests", "Golden", "slime.aseprite"), src, true);
        var item = AssetImporter.Inspect(src, p);
        Assert.Equal("Sprite sheet", item.Detected);
        Assert.True(item.IsReady, item.Error ?? item.Warning);
        Assert.Equal((16, 12), (item.FrameWidth, item.FrameHeight));
        Assert.Equal(5, item.FramesFound);  // the empty frame is trimmed
        Assert.Equal("Ready · 6 frames, 2 tags.", item.Ready);
        Assert.Equal("idle", item.AnimationName);
        Assert.False(item.CreateAnimation);  // the build makes the animations from the tags

        var written = AssetImporter.Import(p, item);
        var dest = Path.Combine(p.AssetsDir, "sprites", "jelly.aseprite");
        Assert.Contains(dest, written);
        var meta = p.LoadMeta<SpriteMeta>(dest);
        Assert.Equal((16, 12, true), (meta.FrameWidth, meta.FrameHeight, meta.TrimEmptyFrames));
        Assert.Empty(meta.Animations);
        Assert.Contains(dest, p.SpriteFiles());

        var broken = Path.Combine(fx.Folder, "broken.aseprite");
        File.WriteAllBytes(broken, [1, 2, 3]);
        Assert.StartsWith("broken.aseprite can't be read:", AssetImporter.Inspect(broken, p).Error);
    }
}
