using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Windows.Settings;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.Settings;

/// <summary>Real clicks and key presses on a headless window (E2E helpers of the settings and menu tests).</summary>
public static class Drive
{
    public static T Find<T>(this Visual root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == name) ?? throw new InvalidOperationException($"{typeof(T).Name} '{name}' not found");

    public static void Click(this Window w, Control c)
    {
        Capture.Pump();
        var p = c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), w) ?? throw new InvalidOperationException(c.Name + " is not in the window");
        w.MouseMove(p);
        w.MouseDown(p, MouseButton.Left);
        w.MouseUp(p, MouseButton.Left);
        Capture.Pump();
    }

    public static void Key(this Window w, PhysicalKey k, RawInputModifiers m = RawInputModifiers.None)
    {
        w.KeyPressQwerty(k, m);
        w.KeyReleaseQwerty(k, m);
        Capture.Pump();
    }

    /// <summary>Focuses a text box, selects all and types <paramref name="text"/> like a user.</summary>
    public static void Type(this Window w, TextBox box, string text)
    {
        box.Focus();
        box.SelectAll();
        Capture.Pump();
        if (text.Length == 0) w.Key(PhysicalKey.Delete);
        else w.KeyTextInput(text);
        Capture.Pump();
    }

    /// <summary>The text box inside a Figma Text Input or Field.</summary>
    public static TextBox Box(this Control field) => field.GetVisualDescendants().OfType<TextBox>().First(t => t.IsEffectivelyVisible);

    /// <summary>Clicks a settings checkbox by name.</summary>
    public static void Toggle(this Window w, Visual root, string name) => w.Click(root.Find<CheckBox>(name));

    /// <summary>Opens a dropdown with a click and picks an option from its menu (the menu item's command).</summary>
    public static void Pick(this Window w, Visual root, string name, string option)
    {
        var host = root.Find<Control>(name);
        var d = Dropdown.Of(host) ?? throw new InvalidOperationException(name + " has no dropdown");
        var button = host as Button ?? host.GetVisualDescendants().OfType<Button>().First(b => b.Name is "PART_Dropdown" or "PART_Box");
        w.Click(button);
        Assert.NotNull(d.Menu);
        Assert.True(d.Menu!.IsOpen, name + " menu did not open");
        var item = d.Menu.Items.OfType<MenuItem>().First(i => (string?)i.Header == option);
        item.Command!.Execute(null);
        Capture.Pump();
        Assert.Equal(option, d.Value);
    }

    public static AgbButton SaveButton(this SettingsDialog d) => d.Footer.GetVisualDescendants().OfType<AgbButton>().First(b => b.Name == "PART_Confirm");
    public static AgbButton CancelButton(this SettingsDialog d) => d.Footer.GetVisualDescendants().OfType<AgbButton>().First(b => b.Name == "PART_Cancel");
}
