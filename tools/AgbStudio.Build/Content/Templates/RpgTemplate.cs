using AgbStudio.Build.Audio;
using AgbStudio.Core.Project;

namespace AgbStudio.Build.Content.Templates;

/// <summary>
/// Turn-based RPG template (Launcher · Templates, P15.03): a party of two walks a meadow, meets slimes in
/// turn-based battles (FIGHT / MAGIC / ITEM / RUN), earns gold and experience, and buys potions from a shop. Heroes,
/// enemies and items are Database tables; the battle reads their stats with the generated C# API.
/// </summary>
public static class RpgTemplate
{
    public static GameProject Create(string dir, string name)
    {
        var p = TemplateKit.NewProject(dir, name, name, RomTitle(name), "ARPE", "Title", s =>
        {
            s.Save.Fields.AddRange([
                new SaveField { Name = "gold", Type = "int" }, new SaveField { Name = "exp", Type = "int" },
                new SaveField { Name = "heroHp", Type = "int" }, new SaveField { Name = "mageHp", Type = "int" },
                new SaveField { Name = "mageMp", Type = "int" }, new SaveField { Name = "potions", Type = "int" },
                new SaveField { Name = "level", Type = "int" }, new SaveField { Name = "battles", Type = "int" },
            ]);
            s.Tags = ["Player", "NPC", "Encounter"];
        });
        p.Settings.Scripting.ShopCurrency = "gold";
        p.SaveSettings();
        TemplateKit.AddThaiFont(p);
        TemplateKit.AddStrings(p, TemplateKit.MenuStrings(name.ToUpperInvariant(), name).Concat(
        [
            ("cmd.fight", "FIGHT", "สู้", "UI & menus"), ("cmd.magic", "MAGIC", "เวทมนตร์", "UI & menus"),
            ("cmd.item", "ITEM", "ไอเทม", "UI & menus"), ("cmd.run", "RUN", "หนี", "UI & menus"),
            ("hud.hero", "HERO HP", "ฮีโร่ HP", "UI & menus"), ("hud.mage", "MAGE HP", "จอมเวท HP", "UI & menus"),
            ("hud.enemy", "SLIME HP", "สไลม์ HP", "UI & menus"), ("hud.gold", "GOLD", "ทอง", "UI & menus"),
            ("battle.appears", "A slime appears!", "สไลม์โผล่มาแล้ว!", "Dialogue"),
            ("battle.hero", "The hero strikes!", "ฮีโร่โจมตี!", "Dialogue"),
            ("battle.magic", "The mage casts a fire spell!", "จอมเวทร่ายเวทไฟ!", "Dialogue"),
            ("battle.nomp", "Not enough magic.", "พลังเวทไม่พอ", "Dialogue"),
            ("battle.potion", "A potion restores the party.", "ยาฟื้นพลังให้ทั้งทีม", "Dialogue"),
            ("battle.noitem", "No potions left.", "ยาหมดแล้ว", "Dialogue"),
            ("battle.run", "The party runs away.", "ทีมหนีไปได้", "Dialogue"),
            ("battle.enemy", "The slime bounces at the party!", "สไลม์กระโดดใส่ทีม!", "Dialogue"),
            ("battle.win", "Victory! Gold and experience gained.", "ชนะแล้ว! ได้ทองและค่าประสบการณ์", "Dialogue"),
            ("battle.lose", "The party falls... back to town.", "ทีมพ่ายแพ้... กลับเมือง", "Dialogue"),
            ("shop.hello", "Potions, 30 gold each. Buy one?", "ยาขวดละ 30 ทอง ซื้อไหม?", "Dialogue"),
            ("shop.yes", "Buy", "ซื้อ", "Dialogue"), ("shop.no", "Leave", "ไม่ซื้อ", "Dialogue"),
            ("shop.thanks", "Thank you!", "ขอบคุณครับ!", "Dialogue"), ("shop.poor", "Not enough gold.", "ทองไม่พอ", "Dialogue"),
            ("item.potion.name", "Potion", "ยาฟื้นพลัง", "Items"), ("item.ether.name", "Ether", "ยาเวท", "Items"),
            ("enemy.slime.name", "Slime", "สไลม์", "Items"), ("enemy.bigslime.name", "Big Slime", "สไลม์ยักษ์", "Items"),
            ("hero.hero.name", "Hero", "ฮีโร่", "Items"), ("hero.mage.name", "Mage", "จอมเวท", "Items"),
        ]));
        WriteDatabase(p);
        WriteArt(p);
        TemplateKit.Music(p, "field_theme", XmWriter.Tune("Field theme", 112, 4, 51, "C"));
        TemplateKit.Music(p, "battle_theme", XmWriter.Tune("Battle theme", 152, 4, 53, "A"));
        TemplateKit.Music(p, "title_theme", XmWriter.Tune("Title theme", 96, 2, 55, "F"));
        TemplateKit.Sfx(p, "hit", 300, 120, 0.12);
        TemplateKit.Sfx(p, "spell", 400, 1600, 0.3);
        TemplateKit.Sfx(p, "heal", 600, 1200, 0.25);
        TemplateKit.Sfx(p, "coin", 980, 1480, 0.10);
        WriteMaps(p);
        WriteScenes(p);
        WriteScripts(p);
        return p;
    }

    static string RomTitle(string name)
    {
        var t = new string(name.ToUpperInvariant().Where(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9').ToArray());
        return t.Length == 0 ? "QUEST" : t[..Math.Min(12, t.Length)];
    }

    // ---- database ---------------------------------------------------------------------------------------

    static void WriteDatabase(GameProject p)
    {
        TableFile Table(string name, params FieldDef[] fields)
        {
            var t = new TableFile { Name = name, KeyField = "id" };
            t.Fields.Add(new FieldDef { Name = "id", Type = "enum" });
            t.Fields.AddRange(fields);
            return t;
        }
        var heroes = Table("Heroes", new FieldDef { Name = "name", Type = "text", Localized = true }, new FieldDef { Name = "hp", Type = "int" },
            new FieldDef { Name = "atk", Type = "int" }, new FieldDef { Name = "mp", Type = "int" });
        heroes.Rows.Add(new() { ["id"] = "Hero", ["name"] = "hero.hero.name", ["hp"] = "30", ["atk"] = "6", ["mp"] = "0" });
        heroes.Rows.Add(new() { ["id"] = "Mage", ["name"] = "hero.mage.name", ["hp"] = "20", ["atk"] = "3", ["mp"] = "12" });
        var enemies = Table("Enemies", new FieldDef { Name = "name", Type = "text", Localized = true }, new FieldDef { Name = "hp", Type = "int" },
            new FieldDef { Name = "atk", Type = "int" }, new FieldDef { Name = "gold", Type = "int" }, new FieldDef { Name = "exp", Type = "int" });
        enemies.Rows.Add(new() { ["id"] = "Slime", ["name"] = "enemy.slime.name", ["hp"] = "18", ["atk"] = "3", ["gold"] = "12", ["exp"] = "5" });
        enemies.Rows.Add(new() { ["id"] = "BigSlime", ["name"] = "enemy.bigslime.name", ["hp"] = "40", ["atk"] = "5", ["gold"] = "30", ["exp"] = "14" });
        var items = Table("Items", new FieldDef { Name = "name", Type = "text", Localized = true }, new FieldDef { Name = "power", Type = "int" },
            new FieldDef { Name = "price", Type = "int" });
        items.Rows.Add(new() { ["id"] = "Potion", ["name"] = "item.potion.name", ["power"] = "15", ["price"] = "30" });
        items.Rows.Add(new() { ["id"] = "Ether", ["name"] = "item.ether.name", ["power"] = "8", ["price"] = "60" });
        foreach (var t in new[] { heroes, enemies, items }) GameProject.Save(Path.Combine(p.DataDir, t.Name + ".db"), t);
    }

    // ---- art --------------------------------------------------------------------------------------------

    public const int TGrass = 0, TPath = 1, TTree = 2, TWater = 3, TFloor = 4, TWall = 5, TTall = 6;

    public static Canvas Tiles()
    {
        var c = new Canvas(16 * 7, 16);
        var tiles = TopDownTemplate.Tiles();
        c.Blit(Crop(tiles, TopDownTemplate.TGrass), TGrass * 16, 0);
        c.Blit(Crop(tiles, TopDownTemplate.TPath), TPath * 16, 0);
        c.Blit(Crop(tiles, TopDownTemplate.TTree), TTree * 16, 0);
        c.Blit(Crop(tiles, TopDownTemplate.TWater), TWater * 16, 0);
        c.Blit(Crop(tiles, TopDownTemplate.TFloor), TFloor * 16, 0);
        c.Blit(Crop(tiles, TopDownTemplate.TWall), TWall * 16, 0);
        // Tall grass: where slimes hide (an encounter happens after some steps in it).
        c.Blit(Crop(tiles, TopDownTemplate.TGrass), TTall * 16, 0);
        for (var k = 0; k < 6; k++) c.Rect(TTall * 16 + 1 + k * 2 + (k % 2), 4 + (k % 3) * 3, 1, 5, Canvas.Rgb(48, 128, 64));
        return c;
    }

    static Canvas Crop(Canvas src, int tile)
    {
        var c = new Canvas(16, 16);
        for (var y = 0; y < 16; y++)
            for (var x = 0; x < 16; x++) c.Set(x, y, src.Get(tile * 16 + x, y));
        return c;
    }

    static TilesetMeta Meta() => new()
    {
        Tiles =
        {
            [TGrass] = new TileProperties { Name = "grass" }, [TPath] = new TileProperties { Name = "path" },
            [TTree] = new TileProperties { Name = "tree", Collision = CollisionType.Solid }, [TWater] = new TileProperties { Name = "water", Collision = CollisionType.Solid },
            [TFloor] = new TileProperties { Name = "floor" }, [TWall] = new TileProperties { Name = "wall", Collision = CollisionType.Solid },
            [TTall] = new TileProperties { Name = "tall_grass", Tag = "encounter" },
        },
    };

    /// <summary>Battle backdrop: night sky over a purple field with the ground line (240 x 160).</summary>
    public static Canvas BattleBack()
    {
        var c = new Canvas(240, 160);
        c.Rect(0, 0, 240, 80, Canvas.Rgb(28, 24, 52));
        c.Rect(0, 80, 240, 80, Canvas.Rgb(52, 40, 88));
        c.Rect(0, 80, 240, 2, Canvas.Rgb(88, 72, 136));
        c.Ellipse(120, 92, 70, 8, Canvas.Rgb(72, 56, 116));
        return c;
    }

    static void WriteArt(GameProject p)
    {
        Directory.CreateDirectory(Path.Combine(p.AssetsDir, "images"));
        TemplateKit.Tileset(p, "meadow_tiles", Tiles(), Meta());
        TemplateKit.Image(p, "battle_back", BattleBack());
        TemplateKit.Sprite(p, "hero_topdown", TopDownTemplate.Hero(), new SpriteMeta
        {
            FrameWidth = 16, FrameHeight = 16, OriginX = 8, OriginY = 16, Collider = new ColliderData { X = 3, Y = 8, W = 10, H = 8 },
            Animations =
            {
                new AnimationData { Name = "down", Frames = { 0, 1 }, Ticks = 10 },
                new AnimationData { Name = "up", Frames = { 2, 3 }, Ticks = 10 },
                new AnimationData { Name = "side", Frames = { 4, 5 }, Ticks = 10 },
            },
        });
        TemplateKit.Sprite(p, "shopkeeper", TopDownTemplate.Elder(), new SpriteMeta
        {
            FrameWidth = 16, FrameHeight = 16, OriginX = 8, OriginY = 16, Collider = new ColliderData { X = 2, Y = 4, W = 12, H = 12 },
            Animations = { new AnimationData { Name = "idle", Frames = { 0, 1 }, Ticks = 30 } },
        });
        TemplateKit.Sprite(p, "battle_slime", BigSlime(), new SpriteMeta
        {
            FrameWidth = 32, FrameHeight = 32, OriginX = 16, OriginY = 32, Collider = new ColliderData { X = 0, Y = 0, W = 0, H = 0 },
            Animations = { new AnimationData { Name = "idle", Frames = { 0, 1 }, Ticks = 16 } },
        });
    }

    /// <summary>The battle slime (two 32 x 32 frames, squash and stretch).</summary>
    public static Canvas BigSlime()
    {
        var c = new Canvas(64, 32);
        for (var f = 0; f < 2; f++)
        {
            var x = f * 32;
            c.Ellipse(x + 15.5, 20 + f, 14 + f, 11 - f, Canvas.Rgb(176, 104, 232));
            c.Rect(x + 2, 26, 28, 5, Canvas.Rgb(176, 104, 232));
            c.Rect(x + 2, 30, 28, 1, Canvas.Rgb(112, 56, 176));
            c.Rect(x + 9, 15 + f, 4, 6, PixelArt.Ink);
            c.Rect(x + 19, 15 + f, 4, 6, PixelArt.Ink);
            c.Rect(x + 10, 16 + f, 1, 2, PixelArt.White);
            c.Rect(x + 20, 16 + f, 1, 2, PixelArt.White);
        }
        return c;
    }

    // ---- maps and scenes --------------------------------------------------------------------------------

    static void WriteMaps(GameProject p)
    {
        const int w = 20, h = 14;
        var field = new int[w * h];
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
                field[y * w + x] = x == 0 || y == 0 || x == w - 1 || y == h - 1 ? TTree : TGrass;
        for (var x = 1; x < w - 1; x++) field[6 * w + x] = TPath;
        for (var y = 7; y <= 11; y++) for (var x = 11; x <= 16; x++) field[y * w + x] = TTall;
        for (var y = 2; y <= 3; y++) for (var x = 14; x <= 16; x++) field[y * w + x] = TWater;
        // The shop: a small building with an open front at (4, 4).
        for (var x = 2; x <= 6; x++) { field[2 * w + x] = TWall; field[3 * w + x] = TFloor; field[4 * w + x] = TWall; }
        field[3 * w + 2] = TWall;
        field[3 * w + 6] = TWall;
        field[4 * w + 4] = TFloor;
        var fieldMap = new MapFile { Name = "Meadow", Tileset = "meadow_tiles", Width = w, Height = h, CollisionLayer = 0 };
        fieldMap.Layers.Add(new MapLayerData { Name = "Ground", Type = LayerType.Tile, Bg = 1, Priority = 1, Cells = field.ToList() });
        GameProject.Save(Path.Combine(p.MapsDir, "Meadow.map"), fieldMap);

        var battle = new MapFile { Name = "Battle", Width = 15, Height = 10, CollisionLayer = -1 };
        battle.Layers.Add(new MapLayerData { Name = "Backdrop", Type = LayerType.Image, Bg = 2, Priority = 2, Image = "battle_back" });
        GameProject.Save(Path.Combine(p.MapsDir, "Battle.map"), battle);

        var title = new int[150];
        for (var i = 0; i < 150; i++) title[i] = i / 15 is 0 or 9 || i % 15 is 0 or 14 ? TTree : TGrass;
        var titleMap = new MapFile { Name = "Title", Tileset = "meadow_tiles", Width = 15, Height = 10, CollisionLayer = 0 };
        titleMap.Layers.Add(new MapLayerData { Name = "Ground", Type = LayerType.Tile, Bg = 1, Priority = 1, Cells = title.ToList() });
        GameProject.Save(Path.Combine(p.MapsDir, "Title.map"), titleMap);
    }

    static void WriteScenes(GameProject p)
    {
        GameProject.Save(Path.Combine(p.ScenesDir, "Title.scene"), TemplateKit.TitleScene("Title", "#60B858", "Meadow", "title_theme"));
        var meadow = new SceneFile
        {
            Name = "Meadow", Width = 20, Height = 14, Map = "Meadow", Backdrop = "#60B858", MapX = 260, MapY = 60,
            Music = new MusicSettings { Track = "field_theme" },
            Camera = new CameraSettings { Follow = "Player", DeadZoneW = 16, DeadZoneH = 16, ClampToMap = true },
            Actors =
            {
                new ActorData
                {
                    Name = "Player", Kind = ActorKind.Player, Sprite = "hero_topdown", Animation = "down", X = 72, Y = 120, Tag = "Player",
                    Components = { new ComponentData { Type = "TopDownMover", Fields = { ["speed"] = "1.25" } }, new ComponentData { Type = "Explorer" } },
                },
                new ActorData { Name = "Shopkeeper", Kind = ActorKind.Empty, Sprite = "shopkeeper", Animation = "idle", X = 72, Y = 56, Tag = "NPC", Components = { new ComponentData { Type = "Shopkeeper" } } },
                new ActorData { Name = "HUD Gold", Kind = ActorKind.TextLabel, X = 8, Y = 4, Text = "hud.gold", Group = "HUD" },
            },
        };
        GameProject.Save(Path.Combine(p.ScenesDir, "Meadow.scene"), meadow);
        var battle = new SceneFile
        {
            Name = "Battle", Width = 15, Height = 10, Map = "Battle", Backdrop = "#1C1834", MapX = 520, MapY = 60,
            Music = new MusicSettings { Track = "battle_theme" },
            Camera = new CameraSettings { Follow = "" },
            Actors =
            {
                new ActorData { Name = "Enemy", Kind = ActorKind.Enemy, Sprite = "battle_slime", Animation = "idle", X = 120, Y = 92, Tag = "Encounter" },
                new ActorData { Name = "HUD Hero", Kind = ActorKind.TextLabel, X = 8, Y = 4, Text = "hud.hero", Group = "HUD" },
                new ActorData { Name = "HUD Mage", Kind = ActorKind.TextLabel, X = 8, Y = 20, Text = "hud.mage", Group = "HUD" },
                new ActorData { Name = "HUD Enemy", Kind = ActorKind.TextLabel, X = 136, Y = 4, Text = "hud.enemy", Group = "HUD" },
            },
        };
        GameProject.Save(Path.Combine(p.ScenesDir, "Battle.scene"), battle);
    }

    // ---- scripts ----------------------------------------------------------------------------------------

    static void WriteScripts(GameProject p)
    {
        TemplateKit.Script(p, "Explorer", """
// Walking in tall grass: after some steps a battle starts. The party is healed and set up on the first walk.
public class Explorer : Behaviour
{
    public int stepsPerBattle = 90;
    int steps;

    void Start()
    {
        if (Game.level == 0)
        {
            Game.level = 1;
            Game.heroHp = Heroes.Get(HeroId.Hero).hp;
            Game.mageHp = Heroes.Get(HeroId.Mage).hp;
            Game.mageMp = Heroes.Get(HeroId.Mage).mp;
            Game.gold = 40;
            Game.potions = 1;
        }
        Actor hud = Scene.Find("HUD Gold");
        if (hud != null) hud.SetLabelNumber(Game.gold, 1);
    }

    void Update()
    {
        if (actor.vel.x == 0 && actor.vel.y == 0) return;
        // Tile 6 of the meadow tileset is tall grass.
        if (Scene.GetTile(0, actor.pos.x.Floor() / 16, (actor.pos.y.Floor() - 4) / 16) != 6) return;
        steps += 1;
        if (steps < stepsPerBattle) return;
        steps = 0;
        Save.Checkpoint();
        Scene.Load("Battle");
    }
}
""");
        TemplateKit.Script(p, "Shopkeeper", """
using System.Threading.Tasks;

// Sells potions (Items.Potion, 30 gold) when the player talks to it with A.
public class Shopkeeper : Behaviour
{
    void Start() => actor.blocking = true;

    void Update()
    {
        if (!Input.Pressed(Key.A)) return;
        Actor player = Scene.Find("Player");
        if (player == null) return;
        int dx = (player.pos.x - actor.pos.x).Floor();
        int dy = (player.pos.y - actor.pos.y).Floor();
        if (dx > -24 && dx < 24 && dy > -24 && dy < 24) Talk();
    }

    [Cutscene]
    static async Task Talk()
    {
        await Say("shop.hello");
        var answer = await Choice("shop.yes", "shop.no");
        if (answer != 0) return;
        if (ShopDesk.Buy() == 1) await Say("shop.thanks");
        else await Say("shop.poor");
    }
}

// The shop counter: buying uses the Items table (price) and Game.gold.
public static class ShopDesk
{
    [ScriptNative]
    public static int Buy()
    {
        if (!Shop.Buy(ItemId.Potion)) return 0;
        Game.potions += 1;
        Sound.Play("coin");
        Actor hud = Scene.Find("HUD Gold");
        if (hud != null) hud.SetLabelNumber(Game.gold, 1);
        return 1;
    }
}
""");
        TemplateKit.Script(p, "Battle", """
using System.Threading.Tasks;

// One battle: the party (Hero and Mage) against a slime from the Enemies table. Each turn the player picks
// FIGHT, MAGIC, ITEM or RUN; then the slime attacks. Winning gives the slime's gold and experience.
public class Battle : SceneScript
{
    void OnEnter()
    {
        Fight();
    }

    [Cutscene]
    static async Task Fight()
    {
        BattleRules.Begin();
        await Say("battle.appears");
        while (BattleRules.Over() == 0)
        {
            var command = await Choice("cmd.fight", "cmd.magic", "cmd.item", "cmd.run");
            if (command == 0)
            {
                BattleRules.Attack();
                await Say("battle.hero");
            }
            else if (command == 1)
            {
                if (BattleRules.Spell() == 1) await Say("battle.magic");
                else await Say("battle.nomp");
            }
            else if (command == 2)
            {
                if (BattleRules.Potion() == 1) await Say("battle.potion");
                else await Say("battle.noitem");
            }
            else
            {
                await Say("battle.run");
                Scene.Load("Meadow");
                return;
            }
            if (BattleRules.Over() != 0) break;
            BattleRules.EnemyTurn();
            await Say("battle.enemy");
        }
        if (BattleRules.Over() == 1)
        {
            BattleRules.Reward();
            await Say("battle.win");
        }
        else
        {
            BattleRules.Defeat();
            await Say("battle.lose");
        }
        Scene.Load("Meadow");
    }
}

// The numbers of a battle, used by the cutscene between lines (stats from the Heroes and Enemies tables).
public static class BattleRules
{
    static int enemyHp;
    static EnemyId enemy;

    [ScriptNative]
    public static void Begin()
    {
        Game.battles += 1;
        enemy = Game.battles % 3 == 0 ? EnemyId.BigSlime : EnemyId.Slime;
        enemyHp = Enemies.Get(enemy).hp;
        Show();
    }

    // 0 still fighting, 1 the enemy is down, 2 the party is down.
    [ScriptNative]
    public static int Over()
    {
        if (enemyHp <= 0) return 1;
        if (Game.heroHp <= 0 && Game.mageHp <= 0) return 2;
        return 0;
    }

    [ScriptNative]
    public static void Attack()
    {
        enemyHp -= Heroes.Get(HeroId.Hero).atk + Random.Range(0, 3) + Heroes.Get(HeroId.Mage).atk;
        Sound.Play("hit");
        Camera.Shake(2, 8);
        Show();
    }

    [ScriptNative]
    public static int Spell()
    {
        if (Game.mageMp < 4) return 0;
        Game.mageMp -= 4;
        enemyHp -= 12;
        Sound.Play("spell");
        Show();
        return 1;
    }

    [ScriptNative]
    public static int Potion()
    {
        if (Game.potions == 0) return 0;
        Game.potions -= 1;
        int power = Items.Get(ItemId.Potion).power;
        Game.heroHp = Heal(Game.heroHp, power, Heroes.Get(HeroId.Hero).hp);
        Game.mageHp = Heal(Game.mageHp, power, Heroes.Get(HeroId.Mage).hp);
        Sound.Play("heal");
        Show();
        return 1;
    }

    static int Heal(int hp, int power, int max) => hp + power > max ? max : hp + power;

    [ScriptNative]
    public static void EnemyTurn()
    {
        int damage = Enemies.Get(enemy).atk + Random.Range(0, 2);
        if (Game.heroHp > 0) Game.heroHp -= damage;
        else Game.mageHp -= damage;
        if (Game.heroHp < 0) Game.heroHp = 0;
        if (Game.mageHp < 0) Game.mageHp = 0;
        Sound.Play("hit");
        Show();
    }

    [ScriptNative]
    public static void Reward()
    {
        Game.gold += Enemies.Get(enemy).gold;
        Game.exp += Enemies.Get(enemy).exp;
        if (Game.exp >= Game.level * 20) Game.level += 1;
        Actor e = Scene.Find("Enemy");
        if (e != null) e.visible = false;
    }

    [ScriptNative]
    public static void Defeat()
    {
        Game.heroHp = Heroes.Get(HeroId.Hero).hp;
        Game.mageHp = Heroes.Get(HeroId.Mage).hp;
        Game.gold = Game.gold / 2;
    }

    static void Show()
    {
        Actor hero = Scene.Find("HUD Hero");
        if (hero != null) hero.SetLabelNumber(Game.heroHp, 2);
        Actor mage = Scene.Find("HUD Mage");
        if (mage != null) mage.SetLabelNumber(Game.mageHp, 2);
        Actor foe = Scene.Find("HUD Enemy");
        if (foe != null) foe.SetLabelNumber(enemyHp < 0 ? 0 : enemyHp, 2);
    }
}
""");
    }
}
