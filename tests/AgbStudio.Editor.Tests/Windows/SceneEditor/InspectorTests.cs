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

    /// <summary>
    /// Several actors selected (Ctrl + click in the Hierarchy): Figma has no Inspector state for it, so the Inspector
    /// shows "2 actors selected" and the first actor's fields read-only; a plain click makes them editable again.
    /// </summary>
    [AvaloniaFact]
    public void SeveralActors_ShowTheFirstReadOnlyWithTheCount()
    {
        _w.Click(_w.Find<TreeItem>("Row_actor:0"));
        _w.Click(_w.Find<TreeItem>("Row_actor:3"), mods: RawInputModifiers.Control);
        Assert.Equal("2 actors selected", _w.Find<TextBlock>("MultiSelectionText").Text);
        Assert.Equal("Player", _w.Find<TextBlock>("ActorName").Text);
        var fields = _w.Find<StackPanel>("MultiSelectionFields");
        Assert.False(fields.IsEnabled);
        var boxes = fields.GetVisualDescendants().OfType<TextBox>().ToList();
        Assert.NotEmpty(boxes);
        Assert.All(boxes, b => Assert.False(b.IsEffectivelyEnabled));
        Assert.False(_w.Find<Button>("PriorityButton").IsEffectivelyEnabled);
        // a click on the priority does nothing: nothing is edited while several actors are selected
        var before = File.ReadAllText(_w.M.ScenePath!);
        _w.Click(_w.Find<Button>("PriorityButton"));
        Assert.Equal(before, File.ReadAllText(_w.M.ScenePath!));
        // one actor again: its fields can be edited
        _w.Click(_w.Find<TreeItem>("Row_actor:3"));
        Assert.DoesNotContain(_w.All<TextBlock>(), t => t.Name == "MultiSelectionText");
        Assert.Equal("Question Block", _w.Find<TextBlock>("ActorName").Text);
        Assert.True(_w.Find<Button>("PriorityButton").IsEffectivelyEnabled);
    }

    /// <summary>The Components list under the header names every part of the actor and what kind it is; a click on a
    /// behaviour scrolls to its section (Templates, docs/evidence/ui-changes-since-figma.md).</summary>
    [AvaloniaFact]
    public void ComponentList_NamesEveryPartOfTheActor()
    {
        _w.Click(_w.Find<TreeItem>("Row_actor:0"));
        var list = _w.Find<Border>("ComponentList");
        var rows = list.GetVisualDescendants().OfType<Button>().Where(b => b.Name?.StartsWith("Component_", StringComparison.Ordinal) == true).Select(b => b.Name).ToList();
        Assert.Equal(["Component_Sprite", "Component_Animation_idle", "Component_Platformer_Body", "Component_PlayerController", "Component_Collider", "Component_Camera_follows"], rows);
        Assert.Equal("6", _w.Find<SectionHeader>("Section_Components").Meta);
        // the kind of each part: the runtime's Platformer Body, the project's PlayerController script
        var kinds = list.GetVisualDescendants().OfType<Button>().Where(b => b.Name?.StartsWith("Component_", StringComparison.Ordinal) == true).Select(b => b.Tag as string).ToList();
        Assert.Equal(["sprite", "animation", "built-in", "script", "collider", "camera"], kinds);
        Assert.Equal("Sprite sheet hero.png", Assert.IsType<TipContent>(ToolTip.GetTip(_w.Find<Button>("Component_Sprite"))).Text);
        // every behaviour row has its own section further down
        Assert.NotNull(_w.Find<SectionHeader>("Section_PlatformerBody"));
        Assert.NotNull(_w.Find<SectionHeader>("Section_PlayerController"));
        _w.Click(_w.Find<Button>("Component_PlayerController"));
        // a text label lists its text, a trigger zone its zone and link
        var exit = _w.M.Scene!.Actors.FindIndex(a => a.Name.StartsWith("Exit", StringComparison.Ordinal));
        _w.M.SelectActor(exit);
        Capture.Pump();
        var names = _w.Find<Border>("ComponentList").GetVisualDescendants().OfType<Button>().Select(b => b.Name).Where(n => n?.StartsWith("Component_", StringComparison.Ordinal) == true).ToList();
        Assert.Equal(["Component_Trigger_zone", "Component_Link_to_Cave_01"], names);
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
        Assert.Equal(("48", "288"), (pos.Value, pos.Value2));
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

        // P6.06: script fields with the script's names; [Range(0.5, 3)] speed is a slider over its range, the others text
        var speed = Field("speed");
        Assert.Equal(FieldKind.Slider, speed.Kind);
        Assert.Equal("1.50", speed.Value);
        Assert.Equal(40, speed.SliderValue, 3);   // 1.5 in 0.5..3
        var speedSlider = speed.GetVisualDescendants().OfType<AgbSlider>().Single();
        var at = speedSlider.TranslatePoint(new Point(0, 6), _w.Win)!.Value;
        undo = _w.S.Undo.UndoCount;
        _w.Drag(new Point(at.X + speedSlider.Bounds.Width * 0.4, at.Y), new Point(at.X + speedSlider.Bounds.Width * 0.8, at.Y));
        var dragged = double.Parse(OnDisk.Actors[0].Components[1].Fields["speed"], System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange(dragged, 2.3, 2.7);   // 80% of 0.5..3
        Assert.Equal(undo + 1, _w.S.Undo.UndoCount);
        _w.S.Commands.Execute("edit.undo");
        Assert.Equal("1.5", OnDisk.Actors[0].Components[1].Fields["speed"]);
        Assert.Equal((FieldKind.Text, FieldKind.Text), (Field("jumpPower").Kind, Field("lives").Kind));
        TypeInto(Field("jumpPower").GetVisualDescendants().OfType<TextBox>().Single(b => b.IsEffectivelyVisible), "4.25");
        Assert.Equal("4.25", OnDisk.Actors[0].Components[1].Fields["jumpPower"]);
        Assert.Equal(("4.25", "3"), (Field("jumpPower").Value, Field("lives").Value));

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
        Assert.Equal("PlayerController.cs", Assert.IsType<AgbStudio.Editor.Windows.Scripts.ScriptEditorPage>(_w.Win.CurrentPage).Buffer!.Name);
        // back to the Scene Editor
        _ = _w.S.Windows.Open("T-P01", source: "test");
        Capture.Pump();
        // the button sits under the Inspector fold of a long actor: scroll to it as a user would
        _w.Find<Button>("AddComponentButton").BringIntoView();
        Capture.Pump();
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
