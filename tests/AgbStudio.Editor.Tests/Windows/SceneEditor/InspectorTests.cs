using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Windows.SceneEditor;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.SceneEditor;

/// <summary>P11.13: Inspector header (layer, priority), Transform, Sprite (sheet, animation, frames, palette), behaviours with
/// fields and Collider, script fields, Add Component, Component Menu and the GBA Budget meters.</summary>
public sealed class InspectorTests : IDisposable
{
    readonly SceneWindow _w = new();
    public void Dispose() => _w.Dispose();

    InspectorField Field(string name) => _w.Find<InspectorField>("Field_" + name);
    SceneFile OnDisk => GameProject.Load<SceneFile>(_w.M.ScenePath!);

    void TypeInto(TextBox box, string text)
    {
        box.Focus();
        Capture.Pump();
        box.SelectAll();
        _w.Type(text);
    }

    [AvaloniaFact]
    public void Header_TransformAndSpriteOfThePlayer()
    {
        Assert.Equal("Player", _w.Find<TextBlock>("ActorName").Text);
        Assert.Equal("ACTOR", _w.Find<Chip>("ActorChip").Content);
        var prio = _w.Find<Button>("PriorityButton");
        Assert.Equal("Layer OBJ · Prio 1", ((TextBlock)prio.Content!).Text);
        _w.Click(prio);
        Assert.Equal(2, _w.M.Scene!.Actors[0].Priority);
        Assert.Equal(2, OnDisk.Actors[0].Priority);
        Assert.Equal("Layer OBJ · Prio 2", ((TextBlock)_w.Find<Button>("PriorityButton").Content!).Text);

        var pos = Field("Position");
        Assert.Equal(("40", "112"), (pos.Value, pos.Value2));
        var boxes = pos.GetVisualDescendants().OfType<TextBox>().Where(b => b.IsEffectivelyVisible).ToList();
        TypeInto(boxes[0], "64");
        Assert.Equal(64, _w.M.Scene.Actors[0].X);
        Assert.Equal(64, OnDisk.Actors[0].X);
        TypeInto(boxes[1], "96");
        Assert.Equal(96, OnDisk.Actors[0].Y);

        Assert.Equal("hero.png", Field("Sheet").Value);
        Assert.Equal("idle", Field("Animation").Value);
        Assert.NotEmpty(_w.Find<StackPanel>("FrameStrip").Children);
        Assert.Equal(_w.M.Assets!.Sprite("hero")!.ColorsUsed, _w.Find<StackPanel>("PaletteSwatches").Children.Count);
        _w.Click(_w.Find<Button>("PaletteButton"));
        Assert.Equal(("T-P10", "InspectorPalette"), _w.S.Windows.Requests[^1]);
    }

    [AvaloniaFact]
    public void SheetAndAnimationDropdowns()
    {
        var sheet = Field("Sheet").GetVisualDescendants().OfType<Button>().First(b => b.Name == "PART_Dropdown");
        _w.Click(sheet);
        Assert.Equal(("T-O04", "SheetDropdown"), _w.S.Windows.Requests[^1]);
        var picker = Assert.IsType<AssetPicker>(_w.Win.Overlays.Open.Single());
        Assert.Equal("Sprites", picker.Tab);
        Assert.True(picker.Items.Any(p => Path.GetFileName(p) == "slime.png"), string.Join(", ", picker.Items));
        picker.Pick(picker.Items.First(p => Path.GetFileName(p) == "slime.png"));
        Capture.Pump();
        Assert.Equal("slime", _w.M.Scene!.Actors[0].Sprite);
        Assert.Equal("idle", _w.M.Scene.Actors[0].Animation);
        Assert.Equal("slime.png", Field("Sheet").Value);
        _w.S.Commands.Execute("edit.undo");
        Capture.Pump();
        Assert.Equal("hero", _w.M.Scene.Actors[0].Sprite);
        var anim = Field("Animation").GetVisualDescendants().OfType<Button>().First(b => b.Name == "PART_Dropdown");
        _w.Click(anim);
        Assert.Equal(("T-M16", "AnimationDropdown"), _w.S.Windows.Requests[^1]);
    }

    [AvaloniaFact]
    public void BehaviourFields_SliderTextColliderAndUndo()
    {
        var gravity = Field("gravity");
        Assert.Equal(FieldKind.Slider, gravity.Kind);
        Assert.Equal("0.25", gravity.Value);
        Assert.Equal("Gravity", gravity.Label);
        Assert.Equal(["Gravity", "Jump force", "Max speed", "Collider"],
            _w.Find<SectionHeader>("Section_PlatformerBody").GetVisualAncestors().OfType<StackPanel>().First().GetVisualDescendants().OfType<InspectorField>().Select(f => f.Label));
        // drag the slider to the middle: gravity 0.40 (range 0..0.8), one undo step for the drag
        var slider = gravity.GetVisualDescendants().OfType<AgbSlider>().Single();
        var undo = _w.S.Undo.UndoCount;
        var left = slider.TranslatePoint(new Point(0, 6), _w.Win)!.Value;
        _w.Drag(new Point(left.X + slider.Bounds.Width * 0.3, left.Y), new Point(left.X + slider.Bounds.Width * 0.5, left.Y));
        Assert.InRange(double.Parse(_w.M.Scene!.Actors[0].Components[0].Fields["gravity"], System.Globalization.CultureInfo.InvariantCulture), 0.38, 0.42);
        Assert.Equal(undo + 1, _w.S.Undo.UndoCount);
        Assert.Equal(Behaviours.Display(Behaviours.Builtins[0].Fields[0], _w.M.Scene.Actors[0].Components[0].Fields["gravity"]), Field("gravity").Value);
        _w.S.Commands.Execute("edit.undo");
        Assert.Equal("0.25", OnDisk.Actors[0].Components[0].Fields["gravity"]);

        // script fields are text fields with the script's names
        var speed = Field("speed");
        Assert.Equal(FieldKind.Text, speed.Kind);
        Assert.Equal("1.5", speed.Value);
        TypeInto(speed.GetVisualDescendants().OfType<TextBox>().Single(b => b.IsEffectivelyVisible), "2.25");
        Assert.Equal("2.25", OnDisk.Actors[0].Components[1].Fields["speed"]);
        Assert.Equal(("3.5", "3"), (Field("jumpPower").Value, Field("lives").Value));

        // Collider: "w × h" and "x, y" from the sprite, an override once edited
        var col = Field("Collider");
        Assert.Equal(("12 × 14", "2, 2"), (col.Value, col.Value2));
        TypeInto(col.GetVisualDescendants().OfType<TextBox>().First(b => b.IsEffectivelyVisible), "10 × 12");
        var c = OnDisk.Actors[0].Collider!;
        Assert.Equal((2, 2, 10, 12), (c.X, c.Y, c.W, c.H));
    }

    [AvaloniaFact]
    public void ScriptHeaderOpensTheScriptEditor_AddComponentAndComponentMenu()
    {
        var header = _w.Find<SectionHeader>("Section_PlayerController");
        Assert.Equal("script", header.Meta);
        _w.DoubleClick(header);
        Assert.Equal(("T-P04", "ScriptComponent"), _w.S.Windows.Requests[^1]);
        Assert.Equal(1, _w.M.SelectedComponent);
        Assert.True(header.IsOpen);
        _w.Key(PhysicalKey.Escape);   // the Script Editor (another group) shows its notice here
        _w.Click(_w.Find<Button>("AddComponentButton"));
        Assert.Equal(("T-O05", "AddComponent"), _w.S.Windows.Requests[^1]);
        var picker = Assert.IsType<ComponentPicker>(_w.Win.Overlays.Open.Single());
        picker.Pick(picker.Items.First(b => b.Name == "Health"));
        Capture.Pump();
        Assert.Equal("Health", _w.M.Scene!.Actors[0].Components[^1].Type);
        Assert.NotNull(_w.Find<SectionHeader>("Section_Health"));
        Assert.Equal("Hearts", Field("hearts").Label);
        // Inspector … opens the Component Menu (#27)
        var menuButton = _w.Find<PanelHeader>("InspectorHeader").GetVisualDescendants().OfType<Button>().First(b => b.Name == "PART_Action");
        _w.Click(menuButton);
        Assert.Equal(("T-M14", "InspectorMenu"), _w.S.Windows.Requests[^1]);
    }

    [AvaloniaFact]
    public void GbaBudget_ShowsLiveMetersAndOpensBudgetDetails()
    {
        var meters = _w.Find<StackPanel>("BudgetMeters").Children.OfType<Meter>().ToList();
        Assert.Equal(["VRAM BG", "VRAM OBJ", "OAM"], meters.Select(m => m.Label));
        var budget = SceneBudget.For(_w.M);
        Assert.Equal(budget["OAM"].Text, meters[2].ValueText);
        Assert.Equal($"{_w.M.Scene!.Actors.Count(a => a.Sprite.Length > 0)} / 128", meters[2].ValueText);
        // adding a sprite actor changes the numbers at once
        SceneCommands.AddActor(_w.S, ActorKind.Pickup, "coin");
        Capture.Pump();
        Assert.Equal($"{_w.M.Scene.Actors.Count(a => a.Sprite.Length > 0)} / 128", _w.Find<StackPanel>("BudgetMeters").Children.OfType<Meter>().ElementAt(2).ValueText);
        _w.Click(_w.Find<Button>("BudgetButton"));
        Assert.Equal(("T-O06", "GbaBudget"), _w.S.Windows.Requests[^1]);
        Assert.IsType<BudgetDetails>(_w.Win.Overlays.Open.Single());
    }
}
