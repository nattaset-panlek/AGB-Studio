namespace AgbStudio.Editor.Tests.Prototype;

/// <summary>Clicks the app control of a prototype link and checks the app did what the link does in Figma.</summary>
public static class LinkRunner
{
    /// <summary>
    /// Triggers <paramref name="l"/> on the window that is shown and checks the result: NAVIGATE / SWAP show the
    /// destination page, OVERLAY opens the destination on top of the source, CLOSE closes the source dialog or popover,
    /// BACK returns to the page shown before.
    /// </summary>
    public static void Follow(AppSession s, ProtoLink l)
    {
        var control = LinkControls.For(l) ?? throw new Xunit.Sdk.XunitException($"{l}: no app control is known for this node");
        var pageBefore = s.Win.CurrentPageId;
        var overlaysBefore = s.Win.Overlays.Open.ToList();
        var source = AppSession.IsPage(l.FromId) ? null : overlaysBefore.LastOrDefault();
        if (source != null)
            Assert.True(AppSession.TypeOf(l.FromId).IsInstanceOfType(source), $"{l}: {l.From} is not the top dialog or popover ({s.Describe()})");
        else
            Assert.True(pageBefore == l.FromId && overlaysBefore.Count == 0, $"{l}: {l.From} is not the shown page ({s.Describe()})");
        var previousPage = s.Pages.Count > 1 ? s.Pages[^2] : null;
        s.Steps.Add($"link {l}");

        control.Prepare?.Invoke(s);
        var target = control.Find(s);
        if (control.Gesture == Gesture.DoubleClick) s.DoubleClick(target);
        else s.Click(target);

        var now = s.Win.Overlays.Open.ToList();
        string Why() => $"{l} via {control.Name}: {s.Describe()}";
        switch (l.Action)
        {
            case "NAVIGATE":
                Assert.True(l.ToId == s.Win.CurrentPageId, "page: " + Why());
                Assert.True(AppSession.TypeOf(l.ToId!).IsInstanceOfType(s.Win.CurrentPage), "page class: " + Why());
                Assert.True(now.Count == 0, "a dialog stayed open: " + Why());
                break;
            case "SWAP":
                if (source is null) goto case "NAVIGATE";
                Assert.DoesNotContain(source, now);
                Assert.True(now.Count == overlaysBefore.Count && AppSession.TypeOf(l.ToId!).IsInstanceOfType(now[^1]), "swap: " + Why());
                break;
            case "OVERLAY":
                Assert.True(now.Count == overlaysBefore.Count + 1, "no new dialog or popover: " + Why());
                Assert.True(AppSession.TypeOf(l.ToId!).IsInstanceOfType(now[^1]), $"{now[^1].GetType().Name} is on top instead: " + Why());
                Assert.True(pageBefore == s.Win.CurrentPageId, "the page changed under the overlay: " + Why());
                break;
            case "CLOSE":
                Assert.NotNull(source);
                Assert.True(!now.Contains(source!) && now.Count == overlaysBefore.Count - 1, "still open: " + Why());
                Assert.True(pageBefore == s.Win.CurrentPageId, "the page changed: " + Why());
                break;
            case "BACK":
                Assert.True(previousPage == s.Win.CurrentPageId, "back: " + Why());
                break;
            default:
                throw new Xunit.Sdk.XunitException($"{l}: action {l.Action} is not checked yet");
        }
    }
}
