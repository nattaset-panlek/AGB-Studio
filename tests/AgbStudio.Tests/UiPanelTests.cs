using System.Text.Json;
using AgbStudio.Build;
using AgbStudio.Build.Content;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;
using GBA;

namespace AgbStudio.Tests;

public sealed class UiPanelTests
{
    #region [Public Methods]
    [Fact]
    public void ActorData_PanelProperties_DefaultAndSerialization()
    {
        var actor = new ActorData
        {
            Name = "StatusPanel",
            Kind = ActorKind.Panel,
            X = 16,
            Y = 16,
            Text = "status.title"
        };

        Assert.Equal(ActorKind.Panel, actor.Kind);
        Assert.Equal(new UiPadding(4, 4, 4, 4), actor.Padding);
        Assert.Equal(new UiMargin(0, 0, 0, 0), actor.Margin);
        Assert.Equal(PanelBackground.SolidPaper, actor.Background);
        Assert.Equal((byte)5, actor.PaperColorIndex);
        Assert.Equal((byte)1, actor.BorderColorIndex);
        Assert.True(actor.TileSnap);

        var json = JsonSerializer.Serialize(actor);
        var restored = JsonSerializer.Deserialize<ActorData>(json);

        Assert.NotNull(restored);
        Assert.Equal(ActorKind.Panel, restored.Kind);
        Assert.Equal(4, restored.Padding.Top);
        Assert.Equal(PanelBackground.SolidPaper, restored.Background);
    }

    [Fact]
    public void TileOverlapGuard_EmitsWarning_WhenLabelsShareVramTileRows()
    {
        var dir = Path.Combine(NativeBuild.RepoRoot, "build", "test-projects", "overlap_test");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);

        var p = SampleProjects.CreatePlatformerMini(dir);
        SampleText.AddTo(p, "en");

        var scene = new SceneFile
        {
            Name = "OverlapScene",
            Width = 15,
            Height = 10,
            Actors =
            {
                new ActorData { Name = "LabelA", Kind = ActorKind.TextLabel, X = 16, Y = 16, Text = "ui.menu.start" },
                new ActorData { Name = "LabelB", Kind = ActorKind.TextLabel, X = 16, Y = 20, Text = "ui.menu.start" }
            }
        };

        GameProject.Save(Path.Combine(p.ScenesDir, "OverlapScene.scene"), scene);
        p.Settings.General.StartScene = "OverlapScene";
        p.SaveSettings();

        var builder = new GameBuilder(p, new Toolchain());
        var r = builder.Build(new BuildOptions { GenerateOnly = true });

        Assert.Contains(r.Diagnostics, d => d.Severity == Severity.Warning && d.Message.Contains("share VRAM tile rows"));
    }

    [Fact]
    public void TileOverlapGuard_NoWarning_WhenPaddedSafely()
    {
        var dir = Path.Combine(NativeBuild.RepoRoot, "build", "test-projects", "safe_overlap_test");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);

        var p = SampleProjects.CreatePlatformerMini(dir);
        SampleText.AddTo(p, "en");

        var scene = new SceneFile
        {
            Name = "SafeScene",
            Width = 15,
            Height = 10,
            Actors =
            {
                new ActorData { Name = "LabelA", Kind = ActorKind.TextLabel, X = 16, Y = 8, Text = "ui.menu.start" },
                new ActorData { Name = "LabelB", Kind = ActorKind.TextLabel, X = 16, Y = 40, Text = "ui.menu.start" }
            }
        };

        GameProject.Save(Path.Combine(p.ScenesDir, "SafeScene.scene"), scene);
        p.Settings.General.StartScene = "SafeScene";
        p.SaveSettings();

        var builder = new GameBuilder(p, new Toolchain());
        var r = builder.Build(new BuildOptions { GenerateOnly = true });

        Assert.DoesNotContain(r.Diagnostics, d => d.Severity == Severity.Warning && d.Message.Contains("share VRAM tile rows"));
    }

    [Fact]
    public void UI_ScriptingApi_CreatesPanelWithProperties()
    {
        var panel = UI.CreatePanel(10, 20, 80, 40, PanelStyle.NineSlice);

        Assert.NotNull(panel);
        Assert.Equal(10, panel.X);
        Assert.Equal(20, panel.Y);
        Assert.Equal(80, panel.Width);
        Assert.Equal(40, panel.Height);
        Assert.Equal(PanelStyle.NineSlice, panel.Style);

        panel.SetPosition(30, 50);
        Assert.Equal(30, panel.X);
        Assert.Equal(50, panel.Y);

        panel.SetSize(100, 60);
        Assert.Equal(100, panel.Width);
        Assert.Equal(60, panel.Height);
    }

    [Fact]
    public void ActorData_CanvasAndParentHierarchy_ResolvesWorldPosition()
    {
        var canvas = new ActorData { Name = "MainCanvas", Kind = ActorKind.Canvas, X = 0, Y = 0 };
        var panel = new ActorData { Name = "DialogPanel", Kind = ActorKind.Panel, Parent = "MainCanvas", X = 20, Y = 30 };
        var label = new ActorData { Name = "TitleText", Kind = ActorKind.TextLabel, Parent = "DialogPanel", X = 10, Y = 15 };
        var badge = new ActorData { Name = "BadgeIcon", Kind = ActorKind.Empty, Parent = "TitleText", X = 5, Y = 5 };

        var all = new List<ActorData> { canvas, panel, label, badge };

        Assert.True(canvas.IsUi);
        Assert.True(panel.IsUi);
        Assert.True(label.IsUi);
        Assert.False(badge.IsUi);

        Assert.Equal((0, 0), canvas.GetWorldPosition(all));
        Assert.Equal((20, 30), panel.GetWorldPosition(all));
        Assert.Equal((30, 45), label.GetWorldPosition(all));
        Assert.Equal((35, 50), badge.GetWorldPosition(all));

        // Cycle protection test: Circular parent references should terminate safely
        panel.Parent = "BadgeIcon";
        var pos = panel.GetWorldPosition(all);
        Assert.True(pos.X >= 0);
    }

    [Fact]
    public void GameBuilder_EmitsWorldCoordinates_ForParentedUiElements()
    {
        var dir = Path.Combine(NativeBuild.RepoRoot, "build", "test-projects", "parented_ui_test");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);

        var p = SampleProjects.CreatePlatformerMini(dir);
        SampleText.AddTo(p, "en");

        var scene = new SceneFile
        {
            Name = "UiScene",
            Width = 15,
            Height = 10,
            Actors =
            {
                new ActorData { Name = "UICanvas", Kind = ActorKind.Canvas, X = 0, Y = 0 },
                new ActorData { Name = "UIPanel", Kind = ActorKind.Panel, Parent = "UICanvas", X = 16, Y = 24 },
                new ActorData { Name = "ScoreText", Kind = ActorKind.TextLabel, Parent = "UIPanel", X = 8, Y = 16, Text = "ui.menu.start" }
            }
        };

        GameProject.Save(Path.Combine(p.ScenesDir, "UiScene.scene"), scene);
        p.Settings.General.StartScene = "UiScene";
        p.SaveSettings();

        var builder = new GameBuilder(p, new Toolchain());
        var r = builder.Build(new BuildOptions { GenerateOnly = true });

        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        var genScene = File.ReadAllText(Path.Combine(builder.GenDir, "scenes", "scene_UiScene.cpp"));

        // ScoreText World coordinates should be 16 + 8 = 24, and 24 + 16 = 40
        Assert.Contains("ScoreText", genScene);
        Assert.Contains("24, 40", genScene);
        // the children name their parent by its index in the scene, the root has none
        var lines = genScene.Split('\n');
        Assert.EndsWith(", 1},", lines.First(l => l.Contains("\"ScoreText\"")).TrimEnd());
        Assert.EndsWith(", 0},", lines.First(l => l.Contains("\"UIPanel\"")).TrimEnd());
        Assert.EndsWith("-1},", lines.First(l => l.Contains("\"UICanvas\"")).TrimEnd());
    }
    #endregion
}
