using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Windows.Scripts;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.Scripts;

/// <summary>Scripting API (T-P18, P11.56): tree, search, member table, example and Copy example, see also, offline.</summary>
public sealed class ScriptingApiTests : IDisposable
{
    readonly EditorHarness _h = new();

    public void Dispose() => _h.Dispose();

    [AvaloniaFact]
    public void HelpScriptingApi_F1_ShowsTheTreeAndTheInputPage()
    {
        _h.S.Commands.Execute("help.scriptingApi");
        Capture.Pump();
        var page = _h.Page<ScriptingApiPage>();
        Assert.Equal(["Behaviour", "Actor", "Input", "Key", "Sound", "Music", "Scene", "Camera", "Save", "Text & Dialogue", "Items (Database)", "fixed (math)"], page.TreeLabels);
        Assert.Equal("Scripting API", _h.Win.TitleBar.Document);
        // the table is generated from the reference assembly (ApiDocs): AxisX / AxisY share a row
        Assert.Equal(["Pressed(Key key)", "Held(Key key)", "Released(Key key)", "AxisX / AxisY", "AnyKey"], page.Rows.Select(r => r.Member));
        Assert.Equal(["bool", "bool", "bool", "fixed", "bool"], page.Rows.Select(r => r.Returns));
        Assert.Equal("true on the frame the button goes down", page.Rows[0].Description);
        Assert.Equal(["Overview", "Members", "Key enum", "Example", "See also: Actor, Scene"], page.OnThisPage);
        Assert.Contains("Input.Pressed(Key.Start)", page.Example.Text);
        Assert.Contains(page.Example.Spans, s => s.Kind == CodeKind.Method);
        // the tree opens other pages, including project tables (Items) from the generated project API
        _h.Click(_h.Find<TreeItem>("Page_Items"));
        Assert.Contains(page.Rows, r => r.Member == "Get(ItemId id)" && r.Returns == "Item");
        _h.Click(_h.Find<TreeItem>("Page_fixed"));
        Assert.Contains(page.Rows, r => r.Member.StartsWith("Clamp(", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public void SearchSeeAlsoKeyEnumAndCopyExample()
    {
        _h.Open("T-P18", "Input.Pressed");
        var page = _h.Page<ScriptingApiPage>();
        Assert.Equal("Input", page.Page.Title);
        page.Search.Text = "shake";
        Capture.Pump();
        Assert.Contains("Camera.Shake", page.TreeLabels);
        _h.Click(_h.Find<TreeItem>("Result_Camera.Shake"));
        Assert.Equal("Camera", page.Page.Title);
        Assert.Contains(page.Rows, r => r.Name == "Shake");
        page.Search.Text = "";
        Capture.Pump();
        _h.Click(_h.Find<TreeItem>("Page_Input"));
        _h.Click(_h.Find<ListRow>("OnPage_Key"));
        Assert.Equal("Key", page.Page.Title);
        Assert.Contains(page.Rows, r => r.Member == "Start");
        _h.Click(_h.Find<TreeItem>("Page_Input"));
        _h.Click(_h.Find<AgbButton>("SeeAlso_Scene"));
        Assert.Equal("Scene", page.Page.Title);
        var copied = Phase11ScriptShots.Wait(page.CopyExampleAsync());
        Assert.Equal(page.Page.Example, copied);
        Assert.Contains(_h.S.Toasts.Visible, t => t.Text == "Example copied");
    }

    [AvaloniaFact]
    public void WorksOffline_NoNetworkNeeded()
    {
        // network off: any HTTP request would fail through this proxy; the page is built from local files only
        var old = Environment.GetEnvironmentVariable("HTTP_PROXY");
        Environment.SetEnvironmentVariable("HTTP_PROXY", "http://127.0.0.1:9");
        Environment.SetEnvironmentVariable("HTTPS_PROXY", "http://127.0.0.1:9");
        try
        {
            _h.Open("T-P18");
            var page = _h.Page<ScriptingApiPage>();
            Assert.Equal("Docs v1.0", page.StatusVersion);   // the API reference assembly, version 1.0.0 (Directory.Build.props)
            Assert.Equal("Offline", _h.Find<StatusItem>("Status_Offline").Content);
            Assert.True(File.Exists(AgbStudio.Scripting.ApiReference.ApiDocumentationPath));
            Assert.Equal(5, page.Rows.Count);
            // every page of the tree has members from the local reference assembly (Items comes from the project)
            Assert.All(ScriptingApi.Pages.Where(p => p.Title != "Items (Database)"), p => Assert.NotEmpty(ScriptingApi.Rows(p)));
        }
        finally
        {
            Environment.SetEnvironmentVariable("HTTP_PROXY", old);
            Environment.SetEnvironmentVariable("HTTPS_PROXY", null);
        }
    }
}
