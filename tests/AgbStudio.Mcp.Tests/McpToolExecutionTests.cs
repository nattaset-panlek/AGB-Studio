using System.Text.Json;
using AgbStudio.Build.Content;
using AgbStudio.Core.Project;
using AgbStudio.Mcp.Protocol;
using Xunit;

namespace AgbStudio.Mcp.Tests;

public sealed class McpToolExecutionTests
{
    #region [Public Methods]
    [Fact]
    public async Task SystemAndProjectTools_WorkCorrectly()
    {
        var tempDir = Directory.CreateTempSubdirectory("mcp_test_sys").FullName;
        try
        {
            using var server = new McpServer();

            // 1. agb_server_info
            var res = await CallToolAsync(server, "agb_server_info", new { });
            Assert.False(res.IsError);
            Assert.Contains("agb-studio-mcp", res.Content[0].Text);

            // 2. agb_toolchain_status
            res = await CallToolAsync(server, "agb_toolchain_status", new { });
            Assert.False(res.IsError);
            Assert.Contains("devkitArm", res.Content[0].Text);

            // 3. agb_create_project
            var projFolder = Path.Combine(tempDir, "TestProject");
            res = await CallToolAsync(server, "agb_create_project", new { folder = projFolder, name = "TestProject" });
            Assert.False(res.IsError);
            Assert.Contains("TestProject.agbproj", res.Content[0].Text);

            // 4. agb_get_project_summary
            res = await CallToolAsync(server, "agb_get_project_summary", new { });
            Assert.False(res.IsError);
            Assert.Contains("TestProject", res.Content[0].Text);

            // 5. agb_update_settings
            res = await CallToolAsync(server, "agb_update_settings", new { name = "UpdatedProjectName", lastScene = "Main" });
            Assert.False(res.IsError);
            Assert.Equal("UpdatedProjectName", server.Context.CurrentProject?.Settings.Name);
            Assert.Equal("Main", server.Context.CurrentProject?.Settings.LastScene);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task SceneAndActorTools_CreateAndManipulateActors()
    {
        var tempDir = Directory.CreateTempSubdirectory("mcp_test_scene").FullName;
        try
        {
            using var server = new McpServer();
            server.Context.CreateProject(tempDir, "SceneTest");

            // 1. Create scene
            var res = await CallToolAsync(server, "agb_create_scene", new { name = "Level_01", width = 40, height = 25 });
            Assert.False(res.IsError);

            // 2. Add actor
            res = await CallToolAsync(server, "agb_add_actor", new
            {
                scene = "Level_01",
                name = "Hero",
                kind = "Player",
                x = 32,
                y = 64,
                sprite = "hero"
            });
            Assert.False(res.IsError);
            Assert.Contains("Hero", res.Content[0].Text);

            // 3. Get scene
            res = await CallToolAsync(server, "agb_get_scene", new { name = "Level_01" });
            Assert.False(res.IsError);
            Assert.Contains("Hero", res.Content[0].Text);
            Assert.Contains("Player", res.Content[0].Text);

            // 4. Update actor
            res = await CallToolAsync(server, "agb_update_actor", new
            {
                scene = "Level_01",
                name = "Hero",
                x = 100,
                y = 120,
                hidden = true
            });
            Assert.False(res.IsError);
            Assert.Contains("100", res.Content[0].Text);

            // 5. Remove actor
            res = await CallToolAsync(server, "agb_remove_actor", new { scene = "Level_01", name = "Hero" });
            Assert.False(res.IsError);

            // Verify removed
            res = await CallToolAsync(server, "agb_get_scene", new { name = "Level_01" });
            Assert.DoesNotContain("\"name\": \"Hero\"", res.Content[0].Text);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task TilemapTools_ListAndSetTiles()
    {
        var tempDir = Directory.CreateTempSubdirectory("mcp_test_map").FullName;
        try
        {
            using var server = new McpServer();
            var p = server.Context.CreateProject(tempDir, "MapTest");

            var map = new MapFile
            {
                Name = "Overworld",
                Width = 10,
                Height = 10,
                Layers = [new MapLayerData { Name = "Ground", Cells = Enumerable.Repeat(0, 100).ToList() }]
            };
            GameProject.Save(Path.Combine(p.MapsDir, "Overworld.map"), map);

            // 1. List tilemaps
            var res = await CallToolAsync(server, "agb_list_tilemaps", new { });
            Assert.False(res.IsError);
            Assert.Contains("Overworld", res.Content[0].Text);

            // 2. Set tile
            res = await CallToolAsync(server, "agb_set_tile", new { map = "Overworld", x = 3, y = 4, tileId = 42 });
            Assert.False(res.IsError);

            // 3. Get tilemap and verify
            res = await CallToolAsync(server, "agb_get_tilemap", new { name = "Overworld" });
            Assert.False(res.IsError);
            Assert.Contains("42", res.Content[0].Text);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task ScriptTools_ReadWriteAndAnalyze()
    {
        var tempDir = Directory.CreateTempSubdirectory("mcp_test_scripts").FullName;
        try
        {
            using var server = new McpServer();
            server.Context.CreateProject(tempDir, "ScriptTest");

            // 1. Write script
            var code = @"
using AgbStudio.Api;

public class Mover : Behaviour
{
    public fixed speed = 1.5;
    public override void Update()
    {
        x += speed;
    }
}
";
            var res = await CallToolAsync(server, "agb_write_script", new { name = "Mover.cs", content = code });
            Assert.False(res.IsError);

            // 2. Read script
            res = await CallToolAsync(server, "agb_read_script", new { name = "Mover.cs" });
            Assert.False(res.IsError);
            Assert.Contains("speed = 1.5", res.Content[0].Text);

            // 3. List scripts
            res = await CallToolAsync(server, "agb_list_scripts", new { });
            Assert.False(res.IsError);
            Assert.Contains("Mover.cs", res.Content[0].Text);

            // 4. Analyze scripts
            res = await CallToolAsync(server, "agb_analyze_scripts", new { });
            Assert.False(res.IsError);
            Assert.Contains("Mover", res.Content[0].Text);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task DatabaseTools_InsertUpdateAndDeleteRows()
    {
        var tempDir = Directory.CreateTempSubdirectory("mcp_test_db").FullName;
        try
        {
            using var server = new McpServer();
            var p = server.Context.CreateProject(tempDir, "DbTest");

            var table = new TableFile
            {
                Name = "Weapons",
                KeyField = "id",
                Fields =
                [
                    new FieldDef { Name = "id", Type = "string" },
                    new FieldDef { Name = "damage", Type = "int" }
                ],
                Rows = []
            };
            GameProject.Save(Path.Combine(p.DataDir, "Weapons.db"), table);

            // 1. Insert row
            var res = await CallToolAsync(server, "agb_insert_table_row", new
            {
                table = "Weapons",
                row = new { id = "sword", damage = "15" }
            });
            Assert.False(res.IsError);

            // 2. Get table
            res = await CallToolAsync(server, "agb_get_table", new { name = "Weapons" });
            Assert.False(res.IsError);
            Assert.Contains("sword", res.Content[0].Text);

            // 3. Update row
            res = await CallToolAsync(server, "agb_update_table_row", new
            {
                table = "Weapons",
                keyValue = "sword",
                values = new { damage = "25" }
            });
            Assert.False(res.IsError);
            Assert.Contains("25", res.Content[0].Text);

            // 4. Delete row
            res = await CallToolAsync(server, "agb_delete_table_row", new
            {
                table = "Weapons",
                keyValue = "sword"
            });
            Assert.False(res.IsError);

            // Verify deleted
            res = await CallToolAsync(server, "agb_get_table", new { name = "Weapons" });
            Assert.DoesNotContain("sword", res.Content[0].Text);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task LocalizationTools_ThaiWrapAndFitCheck()
    {
        var tempDir = Directory.CreateTempSubdirectory("mcp_test_loc").FullName;
        try
        {
            using var server = new McpServer();
            server.Context.CreateProject(tempDir, "LocTest");

            // 1. Set string
            var res = await CallToolAsync(server, "agb_set_localization_string", new
            {
                language = "th",
                key = "dialogue.welcome",
                text = "ยินดีต้อนรับสู่โลกของ Game Boy Advance"
            });
            Assert.False(res.IsError);

            // 2. Get localization
            res = await CallToolAsync(server, "agb_get_localization", new { });
            Assert.False(res.IsError);
            Assert.Contains("dialogue.welcome", res.Content[0].Text);

            // 3. Check fit with Thai text
            res = await CallToolAsync(server, "agb_check_text_fit", new
            {
                text = "ยินดีต้อนรับสู่โลกของ Game Boy Advance",
                boxWidth = 224,
                maxLines = 3
            });
            Assert.False(res.IsError);
            Assert.Contains("\"fits\":true", res.Content[0].Text);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }
    #endregion

    #region [Private Methods]
    static async Task<McpToolResult> CallToolAsync(McpServer server, string toolName, object args)
    {
        var json = JsonSerializer.Serialize(args, McpJson.Options);
        using var doc = JsonDocument.Parse(json);
        return await server.Registry.ExecuteToolAsync(toolName, server.Context, doc.RootElement);
    }
    #endregion
}
