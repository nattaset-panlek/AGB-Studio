using System.Text.Json.Nodes;
using AgbStudio.Build;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Windows.Database;
using AgbStudio.Editor.Windows.Font;
using AgbStudio.Editor.Windows.LogicGraph;
using AgbStudio.Editor.Windows.Tilemap;
using AgbStudio.Scripting.Dialogue;
using AgbStudio.Scripting.Graph;
using AgbStudio.Tests;

namespace AgbStudio.Editor.Tests.Services;

/// <summary>
/// P1.04: files the editor writes with the fields only the editor sets (Tilemap Editor BG size / Colours, Font Manager
/// Draw to, New Speaker names and colour, Logic Graph comments, collapsed and disabled nodes) validate against
/// docs/schemas, which refuses fields a format does not have, and read back the same.
/// </summary>
public sealed class EditorFileSchemaTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("agb-editor-schema-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch (IOException) { }
    }

    static List<string> Validate(string schema, string json) => JsonSchemaCheck.Validate(Path.Combine(NativeBuild.RepoRoot, "docs", "schemas", schema), json);

    [Fact]
    public void MapFontDialogueAndGraphFilesTheEditorWritesValidate()
    {
        var p = GameProject.Create(_dir, "Schema");

        var mapPath = Path.Combine(p.MapsDir, "Forest_01.map");
        GameProject.Save(mapPath, new MapFile { Name = "Forest_01", Width = 2, Height = 1, Layers = { new MapLayerData { Name = "Ground", Cells = [0, 1] } } });
        var map = MapDocument.Load(p, mapPath, new UndoService(10));
        map.SetBgSize(BgSize.S32x64);
        map.SetBpp(8);
        map.Save();
        var mapText = File.ReadAllText(mapPath);
        Assert.Contains("\"bgSize\": \"32x64\"", mapText);
        Assert.Empty(Validate("map.schema.json", mapText));
        var back = MapDocument.Load(p, mapPath, new UndoService(10));
        Assert.Equal((BgSize.S32x64, 8), (back.BgSize, back.Bpp));

        var fontPath = Path.Combine(p.FontsDir, "thai_vwf.font");
        GameProject.Save(fontPath, new FontFile { Name = "thai_vwf", Source = "missing.ttf" });
        var font = FontEntry.Load(p, fontPath, new UndoService(10));
        font.SetDrawTo(2);
        font.Save();
        var fontText = File.ReadAllText(fontPath);
        Assert.Contains("\"drawTo\": 2", fontText);
        Assert.Empty(Validate("font.schema.json", fontText));
        Assert.Equal(2, FontEntry.Load(p, fontPath, new UndoService(10)).DrawTo);

        // New Speaker (T-D21) keeps the names per language and the name tag colour with the speaker
        var dlgPath = Path.Combine(p.DialogueDir, "intro.dlg");
        var dlg = new DialogueDoc(dlgPath, new DialogueFile { Name = "intro", Conversations = { new Conversation { Id = "main" } } });
        dlg.File.Speakers.Add(new DialogueSpeaker { Name = "Bat", Portrait = "slime" });
        dlg.SpeakerExtras["Bat"] = new JsonObject { ["names"] = new JsonObject { ["en"] = "Bat", ["th"] = "ค้างคาว" }, ["color"] = "#B79CFF" };
        dlg.Save();
        var dlgText = File.ReadAllText(dlgPath);
        Assert.Contains("\"color\": \"#B79CFF\"", dlgText);
        Assert.Empty(Validate("dlg.schema.json", dlgText));
        var dlgBack = DialogueDoc.Load(dlgPath);
        Assert.Equal(("ค้างคาว", "#B79CFF"), (dlgBack.SpeakerName("Bat", "th"), dlgBack.SpeakerColor("Bat")));
        Assert.Equal(dlgText, dlgBack.ToJson());

        var graph = GraphDocument.New(Path.Combine(p.ScriptsDir, "PlayerLogic.graph"), "PlayerLogic");
        graph.Graph.Nodes.Add(new GraphNode { Id = "n1", Type = "OnUpdate", X = 40, Y = 60 });
        graph.Comments.Add(new GraphComment { Title = "Jump", X = 20, Y = 30, W = 300, H = 160 });
        graph.Collapsed.Add("n1");
        graph.DisabledNodes.Add(new GraphNode { Id = "n2", Type = "PlaySound", X = 300, Y = 60 });
        graph.DisabledLinks.Add(new GraphLink("n1.out", "n2.in"));
        var graphText = graph.ToJson();
        Assert.Contains("\"comments\"", graphText);
        Assert.Contains("\"disabled\"", graphText);
        Assert.Empty(Validate("graph.schema.json", graphText));
        var graphBack = new GraphDocument();
        graphBack.FromJson(graphText);
        Assert.Equal(graphText, graphBack.ToJson());
    }
}
