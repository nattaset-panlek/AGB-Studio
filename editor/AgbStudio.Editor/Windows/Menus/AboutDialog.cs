using System.Globalization;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace AgbStudio.Editor.Windows.Menus;

/// <summary>
/// About AGB Studio (T-D11, 460 x 304; Help › About AGB Studio): app icon, version and build date, credits, Licenses
/// (opens the third-party notices, docs/THIRD_PARTY.md) and Website (editor.json websiteUrl; disabled when not set).
/// </summary>
[EditorWindow("T-D11")]
public sealed class AboutDialog : FigmaDialog
{
    public const string Credits = "Built on devkitPro (devkitARM, libtonc, maxmod) and the mGBA emulator core. Scripts are C#, compiled to C++ with Roslyn.";

    public string VersionLine { get; }
    public AgbButton LicensesButton { get; }
    public AgbButton WebsiteButton { get; }

    public AboutDialog(WindowContext ctx) : this(ctx.Services) { }

    public AboutDialog(EditorServices s, string? version = null, DateTime? date = null)
    {
        Name = "AboutDialog";
        var links = EditorLinks.Of(s);
        VersionLine = $"Version {version ?? About.Version} · {(date ?? About.BuildDate).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
        var icon = new AppIcon { Variant = AppIconTheme.Light, Width = 64, Height = 64, VerticalAlignment = VerticalAlignment.Top };
        // the product name is the wordmark (brand font Nunito ExtraBold, T-K03), not a text style
        var wordmark = new TextBlock { Text = "AGB Studio", FontFamily = AgbStudio.Editor.Theme.Fonts.Wordmark, FontWeight = FontWeight.ExtraBold, FontSize = 22, LineHeight = 26 };
        wordmark.Token(TextBlock.ForegroundProperty, "text/primary");
        var name = UI.Column(4, wordmark, UI.Text(VersionLine, "Body/12", "text/muted"));
        name.VerticalAlignment = VerticalAlignment.Top;
        name.Margin = new Thickness(0, 8, 0, 0);
        var credits = UI.Text(Credits, "Body/12", "text/secondary");
        credits.TextWrapping = TextWrapping.Wrap;
        credits.LineHeight = 16;
        LicensesButton = new AgbButton { Kind = ButtonKind.Secondary, Glyph = "book", Content = "Licenses", Name = "Licenses" };
        LicensesButton.Click += (_, _) =>
        {
            if (About.LicensesFile() is { } f) links.Open(f);
            else s.Toasts.Show("The third-party notices (THIRD_PARTY.md) are missing from this install.", ToastStatus.Error);
        };
        WebsiteButton = new AgbButton { Kind = ButtonKind.Secondary, Glyph = "globe", Content = "Website", Name = "Website", IsEnabled = links.Config.WebsiteUrl.Trim().Length > 0 };
        WebsiteButton.Click += (_, _) => links.Open(links.Config.WebsiteUrl.Trim());
        var content = UI.Column(14, UI.Row(16, icon, name), credits, UI.Row(8, LicensesButton, WebsiteButton));
        content.Margin = new Thickness(20);
        var footer = new DialogFooter { ConfirmText = "Close", CancelText = null };
        footer.Confirm += (_, _) => Close(null);
        Build("About AGB Studio", "star", content, footer, 460);
    }
}
