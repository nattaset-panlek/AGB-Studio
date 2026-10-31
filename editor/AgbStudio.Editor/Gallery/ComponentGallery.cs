using AgbStudio.Editor.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace AgbStudio.Editor.Gallery;

/// <summary>
/// Development window that lays out every component variant like the Figma "AGB Studio · Components" section
/// (start with <c>AGBStudio --gallery</c>). Not part of the product UI.
/// </summary>
public sealed class ComponentGallery : Window
{
    public ComponentGallery()
    {
        Title = "AGB Studio · Components";
        Icon = Graphics.WindowIcons.App();
        Width = 1440;
        Height = 900;
        Avalonia.Media.RenderOptions.SetTextRenderingMode(this, Avalonia.Media.TextRenderingMode.Antialias);
        var root = new StackPanel { Spacing = 24, Margin = new Thickness(32) };
        foreach (var group in GalleryCatalog.Items.GroupBy(i => i.Set))
        {
            var wrap = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach (var item in group)
            {
                var cell = new StackPanel { Spacing = 6, Margin = new Thickness(0, 0, 16, 16), Name = item.Id };
                var host = new Panel { Width = item.Width, Height = item.Height, HorizontalAlignment = HorizontalAlignment.Left, Children = { item.Create() } };
                cell.Children.Add(host);
                cell.Children.Add(UI.Text(item.Variant.Length == 0 ? item.Set : item.Variant, "Caption/10", "text/muted"));
                wrap.Children.Add(cell);
            }
            root.Children.Add(UI.Column(12, UI.Text(group.Key, "Title/14", "text/primary"), wrap));
        }
        Content = new ScrollViewer { Content = root };
    }
}
