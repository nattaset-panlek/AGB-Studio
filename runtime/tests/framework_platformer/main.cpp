// Game framework end to end: a two-scene platformer driven by scripted input. Checks staged loading,
// gravity and tile collision, a script behaviour (written like the C# translator's output), coin pickup,
// patrol turning at a ledge, damage and invincibility, the camera, a scene link, and 60 fps throughout.
#include "agb/hw.hpp"

#include "../test.hpp"
#include "agb/api.hpp"
#include "agb/behaviours.hpp"
#include "agb/format.hpp"
#include "agb/game.hpp"

using namespace agb;

namespace {
// ---- game state (generated from Project Settings · Save data in a real game) -----------------------
struct GameState {
    SaveHeader header;
    s32 score;
    u8 lives;
};
GameState g_state;

// ---- art: tile 0 empty, tile 1 solid colour 1, tile 2 colour 2 ---------------------------------------
AGB_EWRAM_BSS u32 g_tiles[3 * 8];
const u16 kColors[16] = {0x7E10, 0x0200, 0x01FF, 0x7FFF};
const data::Palette kPal{kColors, 1};
const data::Metatile kMeta[3] = {{{0, 0, 0, 0}}, {{1, 1, 1, 1}}, {{2, 2, 0, 0}}};
const data::Collision kCol[3] = {data::Collision::None, data::Collision::Solid, data::Collision::OneWay};
data::Tileset g_tileset;
constexpr int kW = 30, kH = 10;
u16 g_cells[kW * kH];
u16 g_cells2[16 * 10];
data::MapLayer g_layers[1], g_layers2[1];

AGB_EWRAM_BSS u32 g_sprite[2 * 4 * 8];  // 2 frames 16x16
const u16 kSprColors[16] = {0, 0x7FFF, 0x001F};
const data::Palette kSprPal{kSprColors, 1};
const u8 kIdle[] = {0}, kIdleT[] = {10};
const data::AnimDef kAnims[] = {{"idle", kIdle, kIdleT, 1, true, false}};
data::SpriteSheet g_sheet;

// ---- behaviours as the C# translator writes them --------------------------------------------------
class Coin final : public Behaviour {
public:
    static constexpr u16 kTypeId = 65;
    u16 typeId() const override { return kTypeId; }
    const char* typeName() const override { return "Coin"; }
};

class PlayerController final : public Behaviour {
public:
    static constexpr u16 kTypeId = 64;
    u16 typeId() const override { return kTypeId; }
    const char* typeName() const override { return "PlayerController"; }
    fixed speed = fixed::lit(1.5);
    fixed jumpPower = fixed::lit(3.5);
    s32 lives = 3;
    PlatformerBody* body = nullptr;
    void start() override { body = actor->get<PlatformerBody>(); }
    void update() override {
        body->vx = api::Input::AxisX() * speed;
        if (api::Input::Pressed(Key::A) && body->onGround) body->vy = -jumpPower;
    }
    void onTouch(Actor& other) override {
        if (other.is<Coin>()) {
            g_state.score += 10;
            other.destroy();
        }
    }
};

Behaviour* makePlayerController(Arena& a) { return a.make<PlayerController>(); }
Behaviour* makeBody(Arena& a) { return a.make<PlatformerBody>(); }
Behaviour* makeHealth(Arena& a) { return a.make<Health>(); }
Behaviour* makeCoin(Arena& a) { return a.make<Coin>(); }
Behaviour* makePatrol(Arena& a) {
    auto* p = a.make<Patrol>();
    p->speed = fixed::lit(0.5);
    return p;
}
Behaviour* makeDamage(Arena& a) { return a.make<DamageOnTouch>(); }

const BehaviourCreate kPlayerB[] = {makePlayerController, makeBody, makeHealth};
const BehaviourCreate kSlimeB[] = {makeBody, makePatrol, makeDamage};
const BehaviourCreate kCoinB[] = {makeCoin};

ActorDef g_actors[4];
ActorDef g_actors2[1];
const LinkDef kLinks[] = {{3, LinkKind::OnTouch, 1, 40, 100, render::effects::Transition::Fade, 8, nullptr}};
SceneDef g_scene0, g_scene1;
const SceneDef* g_scenes[2];
GameDef g_game;

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
    // Ground on row 9 with a gap at x 12-13, a wall at x 20 rows 7-8, a one-way platform at row 6.
    for (int y = 0; y < kH; ++y)
        for (int x = 0; x < kW; ++x) {
            u16 v = 0;
            if (y == 9 && (x < 12 || x > 13)) v = 1;
            if (x == 20 && (y == 7 || y == 8)) v = 1;
            if (y == 6 && x >= 5 && x <= 8) v = 2;
            g_cells[y * kW + x] = v;
        }
    for (int i = 0; i < 16 * 10; ++i) g_cells2[i] = i >= 16 * 9 ? 1 : 0;
    g_layers[0] = {0, data::LayerType::Tile, 1, false, 256, 256, kW, kH, g_cells, &g_tileset};
    g_layers2[0] = {0, data::LayerType::Tile, 1, false, 256, 256, 16, 10, g_cells2, &g_tileset};
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
    // Player, Slime (between the gap and the wall), Coin, Goal Flag trigger.
    g_actors[0] = {"Player", ActorKind::Player, &g_sheet, 40, 100, 1, 1, "idle", {}, kPlayerB, 3, nullptr, false, -1};
    g_actors[1] = {"Slime", ActorKind::Enemy, &g_sheet, 260, 144, 2, 2, nullptr, {}, kSlimeB, 3, nullptr, false, -1};
    g_actors[2] = {"Coin", ActorKind::Pickup, &g_sheet, 90, 144, 3, 2, nullptr, {}, kCoinB, 1, nullptr, false, -1};
    g_actors[3] = {"Goal Flag", ActorKind::TriggerZone, nullptr, 440, 144, 4, 2, nullptr, {-8, -32, 16, 32}, nullptr, 0, nullptr, false, -1};
    g_actors2[0] = {"Player", ActorKind::Player, &g_sheet, 30, 30, 1, 1, "idle", {}, kPlayerB, 3, nullptr, false, -1};
    g_scene0 = {"Forest_01", kW, kH, 0, g_layers, 1, 0, g_actors, 4, {0, 32, 24, true, 0, 0}, -1, false, kLinks, 1,
                nullptr, nullptr, nullptr, 0x7E10, false, false, nullptr};
    g_scene1 = {"Forest_02", 16, 10, 0, g_layers2, 1, 0, g_actors2, 1, {0, 32, 24, true, 0, 0}, -1, false, nullptr, 0,
                nullptr, nullptr, nullptr, 0x7E10, false, false, nullptr};
    g_scenes[0] = &g_scene0;
    g_scenes[1] = &g_scene1;
    g_game = {};
    g_game.title = "FRAMEWORK";
    g_game.scenes = g_scenes;
    g_game.sceneCount = 2;
    g_game.state = &g_state;
    g_game.stateSize = sizeof g_state;
}

int g_framesRun = 0;
void frames(int n, u16 keys = 0) {
    for (int i = 0; i < n; ++i) {
        input::injectState(keys);
        game::frame();
        ++g_framesRun;
    }
}

Actor* player() { return actors::find("Player"); }
}  // namespace

AGB_TEST(platformer_scene_end_to_end) {
    setupData();
    game::boot(g_game);
    int loadFrames = 0;
    while (game::loading() && loadFrames < 30) {
        frames(1);
        ++loadFrames;
    }
    CHECK(!game::loading());
    CHECK(loadFrames <= 8);  // 7 staged steps, one per frame
    CHECK_EQ(actors::count(), 4);
    const u32 overrunsBefore = profile::stats().overruns;

    // Falls and lands on the ground (row 9 top = y 144).
    frames(60);
    Actor* p = player();
    CHECK(p != nullptr);
    CHECK(p->onGround);
    CHECK_EQ(p->pos.y.floor(), 144 - 14 - 2 + 16);  // origin y = box bottom (box 2..16 below origin -16)
    debug::logf("player landed at %d,%d", p->pos.x.floor(), p->pos.y.floor());

    // Walk right into the coin.
    const int x0 = p->pos.x.floor();
    frames(40, static_cast<u16>(Key::Right));
    CHECK(p->pos.x.floor() >= x0 + 55);
    CHECK_EQ(g_state.score, 10);
    CHECK(actors::find("Coin") == nullptr);
    CHECK(p->facingLeft == false);

    // Jump: leaves the ground, then lands again.
    frames(1, static_cast<u16>(Key::A));
    frames(2);
    CHECK(!p->onGround);
    CHECK(p->vel.y.raw < 0);
    frames(60);
    CHECK(p->onGround);

    // The slime patrols between the wall (x 320) and the ledge (gap at x 192..223): it must turn.
    Actor* slime = actors::find("Slime");
    CHECK(slime != nullptr);
    Patrol* patrol = slime->get<Patrol>();
    const s8 dir0 = patrol->dir;
    int turns = 0;
    s8 last = dir0;
    for (int i = 0; i < 400; ++i) {
        frames(1);
        if (patrol->dir != last) ++turns, last = patrol->dir;
    }
    CHECK(turns >= 2);
    CHECK(slime->pos.x.floor() > 224 && slime->pos.x.floor() < 320);
    CHECK(slime->onGround);

    // Touching the slime costs one heart and makes the player invincible.
    Health* h = p->get<Health>();
    p->pos = slime->pos;
    frames(2);
    CHECK_EQ(h->hearts, 2);
    CHECK(h->invincibleLeft > 0);
    frames(2);
    CHECK_EQ(h->hearts, 2);

    // Camera follows and stays inside the map (480 px wide).
    CHECK(scene::cameraX() >= 0 && scene::cameraX() <= 480 - 240);

    // Save checkpoint writes the header.
    api::Save::Checkpoint();
    CHECK_EQ(g_state.header.scene, 0);

    // Walk into the goal flag: link to Forest_02 with a fade, player at the spawn point.
    p->pos = {fixed(400), fixed(144)};
    for (int i = 0; i < 120 && game::currentScene() == 0; ++i) frames(1, static_cast<u16>(Key::Right));
    for (int i = 0; i < 60 && game::loading(); ++i) frames(1);
    CHECK_EQ(game::currentScene(), 1);
    CHECK(!game::loading());
    p = player();
    CHECK(p != nullptr);
    CHECK_EQ(p->pos.x.floor(), 40);
    CHECK_EQ(actors::count(), 1);
    frames(30);
    CHECK(p->onGround);
    CHECK_EQ(profile::stats().overruns, overrunsBefore);
    debug::logf("frames run %d, scene load took %lu frames", g_framesRun, scene::lastLoadFrames());
    for (int i = 0; i < 7; ++i) debug::logf("  load step %d (%s): %lu cycles", i, scene::loadStepName(i), scene::loadStepCycles(i));
}

AGB_TEST(frame_phases_follow_blueprint_order) {
    // P2.03: the frame-order trace of a running scene (the second scene of the test above) must follow blueprint 04:
    // input, Screen.Update, behaviours, Event Script VM, tasks, physics, animate, build OAM, palette fade, mark dirty
    // tilemaps, then the commit in VBlank.
    CHECK(!game::loading());
    using P = debug::Phase;
    const P order[] = {P::Input,     P::ScreenUpdate, P::Behaviours,  P::ScriptVm, P::Tasks,       P::Physics,
                       P::Animation, P::BuildOam,     P::PaletteFade, P::Tilemaps, P::VBlankCommit};
    constexpr int kPerFrame = sizeof order / sizeof order[0];
    debug::clearPhaseTrace();
    frames(2);
    P trace[32];
    const int n = debug::phaseTrace(trace, 32);
    CHECK_EQ(n, 2 * kPerFrame);
    int wrong = 0;
    for (int i = 0; i < n && i < 2 * kPerFrame; ++i)
        if (trace[i] != order[i % kPerFrame] && wrong++ < 3)
            debug::logf("  phase %d: %s, expected %s", i, debug::phaseName(trace[i]), debug::phaseName(order[i % kPerFrame]));
    CHECK_EQ(wrong, 0);
    char line[160];
    int len = 0;
    for (int i = 0; i < kPerFrame && i < n && len < 140; ++i) len += format(line + len, sizeof line - len, i ? ", %s" : "%s", debug::phaseName(trace[i]));
    debug::logf("frame order: %s", line);
}

int main() { test::runAll(); }
