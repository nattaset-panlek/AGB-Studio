// ROM test of translated C# scripts: the Figma PlayerController, Coin and SlimeController run in a real scene
// (behaviour events, Inspector overrides, typed fields), then cutscenes ([Cutscene] async), a waiting
// [ScriptNative] and the forest_intro dialogue run in the Event Script VM with both choices.
#include "test.hpp"

#include "Coin.hpp"
#include "PlayerController.hpp"
#include "SlimeController.hpp"
#include "agb/api.hpp"
#include "agb/behaviours.hpp"
#include "agb/game.hpp"
#include "agb/natives.hpp"
#include "agb_scripts.hpp"

using agb::Actor;
using agb::fixed;
using agb::Key;

namespace {
// ---- art and map (same layout as the framework test: ground row 9, gap at x 12-13, wall at x 20) --------
AGB_EWRAM_BSS agb::u32 g_tiles[3 * 8];
const agb::u16 kColors[16] = {0x7E10, 0x0200, 0x01FF, 0x7FFF};
const agb::data::Palette kPal{kColors, 1};
const agb::data::Metatile kMeta[3] = {{{0, 0, 0, 0}}, {{1, 1, 1, 1}}, {{2, 2, 0, 0}}};
const agb::data::Collision kCol[3] = {agb::data::Collision::None, agb::data::Collision::Solid, agb::data::Collision::OneWay};
agb::data::Tileset g_tileset;
constexpr int kW = 30, kH = 10;
agb::u16 g_cells[kW * kH];
agb::u16 g_cells2[16 * 10];
agb::data::MapLayer g_layers[1], g_layers2[1];

AGB_EWRAM_BSS agb::u32 g_sprite[2 * 4 * 8];
const agb::u16 kSprColors[16] = {0, 0x7FFF, 0x001F};
const agb::data::Palette kSprPal{kSprColors, 1};
const agb::u8 kIdle[] = {0}, kIdleT[] = {10};
const agb::data::AnimDef kAnims[] = {{"idle", kIdle, kIdleT, 1, true, false}};
agb::data::SpriteSheet g_sheet;

// ---- behaviours: generated factories, as the scene generator calls them ---------------------------------
agb::Behaviour* makePlayer(agb::Arena& arena) { return agb_make_PlayerController_with(arena, agb_init_PlayerController{.lives = 5}); }
agb::Behaviour* makeBody(agb::Arena& arena) { return arena.make<agb::PlatformerBody>(); }
agb::Behaviour* makeHealth(agb::Arena& arena) { return arena.make<agb::Health>(); }
agb::Behaviour* makeSlime(agb::Arena& arena) { return agb_make_SlimeController_with(arena, agb_init_SlimeController{.speed = fixed::lit(0.75), .turnEvery = 20}); }
agb::Behaviour* makeCoin(agb::Arena& arena) { return agb_make_Coin(arena); }

const agb::BehaviourCreate kPlayerB[] = {makePlayer, makeBody, makeHealth};
const agb::BehaviourCreate kSlimeB[] = {makeBody, makeSlime};
const agb::BehaviourCreate kCoinB[] = {makeCoin};

agb::ActorDef g_actors[3];
agb::ActorDef g_actors2[1];
agb::SceneDef g_scene0, g_scene1;
const agb::SceneDef* g_scenes[2];
agb::GameDef g_game;

void setupData() {
    for (int i = 0; i < 8; ++i) {
        g_tiles[8 + i] = 0x11111111u;
        g_tiles[16 + i] = 0x22222222u;
    }
    g_tileset = {};
    g_tileset.gfx = {g_tiles, sizeof g_tiles, 3, 4, false};
    g_tileset.palette = &kPal;
    g_tileset.metatiles = kMeta;
    g_tileset.collision = kCol;
    g_tileset.metatileCount = 3;
    for (int y = 0; y < kH; ++y)
        for (int x = 0; x < kW; ++x) {
            agb::u16 v = 0;
            if (y == 9 && (x < 12 || x > 13)) v = 1;
            if (x == 20 && (y == 7 || y == 8)) v = 1;
            g_cells[y * kW + x] = v;
        }
    for (int i = 0; i < 16 * 10; ++i) g_cells2[i] = i >= 16 * 9 ? 1 : 0;
    g_layers[0] = {0, agb::data::LayerType::Tile, 1, false, 256, 256, kW, kH, g_cells, &g_tileset};
    g_layers2[0] = {0, agb::data::LayerType::Tile, 1, false, 256, 256, 16, 10, g_cells2, &g_tileset};
    for (int i = 0; i < 32; ++i) g_sprite[i] = 0x11111111u;
    g_sheet = {};
    g_sheet.gfx = {g_sprite, sizeof g_sprite, 8, 4, false};
    g_sheet.palette = &kSprPal;
    g_sheet.width = g_sheet.height = 16;
    g_sheet.sizeCode = 1;
    g_sheet.tilesPerFrame = 4;
    g_sheet.frameCount = 2;
    g_sheet.anims = kAnims;
    g_sheet.animCount = 1;
    g_sheet.originX = 8;
    g_sheet.originY = 16;
    g_sheet.colliderX = 2;
    g_sheet.colliderY = 2;
    g_sheet.colliderW = 12;
    g_sheet.colliderH = 14;
    // Tags: Player 1, Enemy 2, Coin 3 (agb_tag ids of the project). Members are set one by one so runtime structs
    // can grow trailing members without breaking the harness.
    auto actor = [](agb::ActorDef& a, const char* name, agb::ActorKind kind, int x, int y, agb::u16 tag, agb::u8 prio,
                    const agb::BehaviourCreate* bs, agb::u8 count) {
        a = {};
        a.name = name;
        a.kind = kind;
        a.sheet = &g_sheet;
        a.x = static_cast<agb::s16>(x);
        a.y = static_cast<agb::s16>(y);
        a.tag = tag;
        a.priority = prio;
        a.anim = kind == agb::ActorKind::Player ? "idle" : nullptr;
        a.behaviours = bs;
        a.behaviourCount = count;
        a.labelKey = -1;
    };
    actor(g_actors[0], "Player", agb::ActorKind::Player, 40, 100, agb_tag::Player, 1, kPlayerB, 3);
    actor(g_actors[1], "Slime", agb::ActorKind::Enemy, 260, 144, agb_tag::Enemy, 2, kSlimeB, 2);
    actor(g_actors[2], "Coin", agb::ActorKind::Pickup, 90, 144, agb_tag::Coin, 2, kCoinB, 1);
    actor(g_actors2[0], "Player", agb::ActorKind::Player, 30, 30, agb_tag::Player, 1, kPlayerB, 3);
    auto scene = [](agb::SceneDef& s, const char* name, int w, int h, const agb::data::MapLayer* layers, const agb::ActorDef* actors,
                    agb::u16 count, void (*onLoad)(), void (*onEnter)(), void (*onExit)()) {
        s = {};
        s.name = name;
        s.widthTiles = static_cast<agb::u16>(w);
        s.heightTiles = static_cast<agb::u16>(h);
        s.layers = layers;
        s.layerCount = 1;
        s.collisionLayer = 0;
        s.actors = actors;
        s.actorCount = count;
        s.camera = {0, 32, 24, true, 0, 0};
        s.music = -1;
        s.onLoad = onLoad;  // generated scene hooks (Scene Settings events and the SceneScript of the scene)
        s.onEnter = onEnter;
        s.onExit = onExit;
        s.backdrop = 0x7E10;
    };
    scene(g_scene0, "Forest_01", kW, kH, g_layers, g_actors, 3, agb_scene_Forest_01_onLoad, agb_scene_Forest_01_onEnter, agb_scene_Forest_01_onExit);
    scene(g_scene1, "Forest_02", 16, 10, g_layers2, g_actors2, 1, agb_scene_Forest_02_onLoad, agb_scene_Forest_02_onEnter, agb_scene_Forest_02_onExit);
    g_scenes[0] = &g_scene0;
    g_scenes[1] = &g_scene1;
    g_game = {};
    g_game.title = "SCRIPTS";
    g_game.scenes = g_scenes;
    g_game.sceneCount = 2;
    g_game.state = &agb_state;
    g_game.stateSize = sizeof agb_state;
    g_game.init = agb_scripts_init;  // registers script / native tables, paths, names and debugger names
    g_game.skipSplash = true;
}

void frames(int n, agb::u16 keys = 0) {
    for (int i = 0; i < n; ++i) {
        agb::input::injectState(keys);
        agb::game::frame();
    }
}

Actor* find(const char* name) { return agb::actors::find(name); }

// Runs a script until it ends (or the frame limit); returns the frames it took, -1 on timeout.
int runScript(agb::u16 id, const agb::s32* args = nullptr, int argc = 0, int limit = 600) {
    const int ctx = agb::script::start(id, agb::script::Priority::Cutscene, args, argc);
    if (ctx < 0) return -1;
    for (int f = 1; f <= limit; ++f) {
        frames(1);
        if (!agb::script::running(ctx)) return f;
    }
    return -1;
}
}  // namespace

AGB_TEST(translated_behaviours_play_like_the_figma_code) {
    setupData();
    agb::game::boot(g_game);
    for (int i = 0; i < 30 && agb::game::loading(); ++i) frames(1);
    CHECK(!agb::game::loading());
    CHECK_EQ(agb::actors::count(), 3);
    frames(60);
    Actor* p = find("Player");
    CHECK(p != nullptr);
    CHECK(p->onGround);

    // Generated class: type id, Inspector fields, the scene's override (lives = 5) and the generic setter.
    auto* pc = p->get<PlayerController>();
    CHECK(pc != nullptr);
    CHECK_EQ(PlayerController::kTypeId, 65);
    CHECK_EQ(pc->lives, 5);
    CHECK_EQ(pc->speed.raw, fixed::lit(1.5).raw);
    CHECK_EQ(pc->fieldCount(), 3);
    CHECK(pc->fieldName(1)[0] == 'j');
    CHECK_EQ(pc->fieldKind(0), 1);
    CHECK_EQ(pc->fieldKind(2), 0);
    CHECK(agb_find_behaviour("Coin") == agb_make_Coin);
    CHECK_EQ(agb_script_type_count, 3);

    // Update(): body.vx = Input.AxisX * speed -> 1.5 px per frame; OnTouch(Coin) adds 10 to Game.score.
    const int x0 = p->pos.x.floor();
    frames(40, static_cast<agb::u16>(Key::Right));
    CHECK(p->pos.x.floor() >= x0 + 55);
    CHECK_EQ(agb_state.score, 10);
    CHECK(find("Coin") == nullptr);

    // Jump with A on the ground: vy = -jumpPower.
    frames(1, static_cast<agb::u16>(Key::A));
    CHECK(!p->onGround || p->vel.y.raw < 0);
    CHECK(p->get<agb::PlatformerBody>()->vy.raw < 0);
    frames(60);
    CHECK(p->onGround);

    // A changed Inspector value through the generic setter (live Inspector edits).
    CHECK(agb_set_field(pc, 0, fixed(2).raw));
    CHECK_EQ(pc->speed.raw, fixed(2).raw);

    // SlimeController turns every 20 frames (or at the wall) and faces where it walks.
    Actor* slime = find("Slime");
    CHECK(slime != nullptr);
    auto* sc = slime->get<SlimeController>();
    CHECK_EQ(sc->speed.raw, fixed::lit(0.75).raw);
    bool sawLeft = false, sawRight = false;
    for (int i = 0; i < 90; ++i) {
        frames(1);
        if (slime->facingLeft) sawLeft = true;
        else sawRight = true;
    }
    CHECK(sawLeft && sawRight);

    // Touching the slime: SlimeController.OnTouch damages the player's Health and gets angry.
    auto* health = p->get<agb::Health>();
    const int hearts = health->hearts;
    p->pos = slime->pos;
    frames(2);
    CHECK_EQ(health->hearts, hearts - 1);
    CHECK(sc->mood == SlimeMood::Angry);
}

AGB_TEST(cutscenes_and_dialogue_run_in_the_vm) {
    using agb::script::natives::stats;
    Actor* p = find("Player");
    CHECK(p != nullptr);

    // Forest.RescueIntro (blueprint 06): LockAll, Music.Play, Say, Move(Player, Path.EnterScene), Story.Forest = Rescued.
    const int x0 = p->pos.x.floor();
    const agb::u16 says = stats().says;
    const int took = runScript(agb_cutscene::Forest_RescueIntro);
    CHECK(took > 0);
    CHECK_EQ(stats().says, says + 1);
    CHECK_EQ(stats().moves, 1);
    CHECK(p->pos.x.floor() >= x0 + 14);
    CHECK_EQ(agb::script::var(1), 2);  // ForestState.Rescued
    CHECK(!agb::script::natives::locked());

    // Forest.AskHelp(3): Choice -> option 0 adds 5 * times to Game.score and waits 30 frames; option 1 plays a sound.
    const agb::s32 three[] = {3};
    agb::script::natives::setAutoChoice(0);
    const int score = agb_state.score;
    const int waited = runScript(agb_cutscene::Forest_AskHelp, three, 1);
    CHECK(waited >= 30);
    CHECK_EQ(agb_state.score, score + 15);
    agb::script::natives::setAutoChoice(1);
    const agb::u16 sounds = stats().sounds;
    CHECK(runScript(agb_cutscene::Forest_AskHelp, three, 1) > 0);
    CHECK_EQ(agb_state.score, score + 15);
    CHECK_EQ(stats().sounds, sounds + 1);
    CHECK_EQ(stats().lastSound, 1);  // Sounds.Coin

    // [ScriptNative(waits: true)] OpenShop waits until EventScript.Continue (natives::resume).
    const int ctx = agb::script::start(agb_cutscene::Shopkeeper_Visit, agb::script::Priority::Cutscene);
    frames(10);
    CHECK(agb::script::running(ctx));
    agb::script::natives::resume(0);
    frames(3);
    CHECK(!agb::script::running(ctx));
    CHECK_EQ(agb_state.coins, 42);

    // forest_intro dialogue, choice "Yes": thanks, Then set Flags.Helped, Story.Visits + 1, Give item, sound, shake,
    // wait, Set flag MetElder, end (no scene change).
    agb::script::natives::setAutoChoice(0);
    const agb::u16 dsays = stats().says;
    CHECK(runScript(agb_cutscene::forest_intro_intro) >= 12);
    CHECK_EQ(stats().says, dsays + 3);
    CHECK(agb::script::flag(0));  // Flags.Helped
    CHECK(agb::script::flag(1));  // Flags.MetElder
    CHECK_EQ(agb::script::var(2), 1);  // Story.Visits
    CHECK_EQ(stats().lastItem, 0);  // ItemId.Potion
    CHECK_EQ(agb::game::currentScene(), 0);

    // Choice "No" (shown only while !Flags.Helped): bye (Show if Story.Forest != Rescued) and Go to scene Forest_02.
    agb::script::setFlag(0, false);
    agb::script::setVar(1, 0);
    agb::script::natives::setAutoChoice(1);
    const agb::u16 nsays = stats().says;
    CHECK(runScript(agb_cutscene::forest_intro_intro) > 0);
    CHECK_EQ(stats().says, nsays + 3);  // hello, ask, bye
    CHECK_EQ(agb::script::var(2), 1);   // Then set of "Yes" did not run
    for (int i = 0; i < 90 && agb::game::currentScene() != 1; ++i) frames(1);
    for (int i = 0; i < 60 && agb::game::loading(); ++i) frames(1);
    CHECK_EQ(agb::game::currentScene(), 1);
    agb::api::Debug::Log("scripts test done");
}

int main() { agb::test::runAll(); }
