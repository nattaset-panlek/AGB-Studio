// Component Picker behaviours and tile collision types, one scenario each: one-way platforms, ladders, hazards and
// water with Platformer Body, Chase Player (with a body and on its own), Sound on Event (touch, jump, spawn and
// destroy, played as SFX Designer effects on the DMG channels) and the Empty Actor preset.
#include "../test.hpp"
#include "agb/api.hpp"
#include "agb/audio.hpp"
#include "agb/behaviours.hpp"
#include "agb/game.hpp"

using namespace agb;

namespace {
void frames(int n, u16 keys = 0) {
    for (int i = 0; i < n; ++i) {
        input::injectState(keys);
        game::frame();
    }
}

// ---- art and map: one metatile per collision type -------------------------------------------------------
AGB_EWRAM_BSS u32 g_tiles[2 * 8];
const u16 kColors[16] = {0x7E10, 0x0200};
const data::Palette kPal{kColors, 1};
const data::Metatile kMeta[6] = {{{0, 0, 0, 0}}, {{1, 1, 1, 1}}, {{1, 1, 0, 0}}, {{1, 0, 1, 0}}, {{0, 0, 1, 1}}, {{1, 0, 0, 1}}};
const data::Collision kCol[6] = {data::Collision::None,   data::Collision::Solid,  data::Collision::OneWay,
                                 data::Collision::Ladder, data::Collision::Hazard, data::Collision::Water};
enum : u16 { None, Solid, OneWay, Ladder, Hazard, Water };
data::Tileset g_tileset;
constexpr int kW = 40, kH = 12;
u16 g_cells[kW * kH];
data::MapLayer g_layers[1];

AGB_EWRAM_BSS u32 g_sprite[4 * 8];
const u16 kSprColors[16] = {0, 0x7FFF};
const data::Palette kSprPal{kSprColors, 1};
const u8 kIdle[] = {0}, kIdleT[] = {10};
const data::AnimDef kAnims[] = {{"idle", kIdle, kIdleT, 1, true, false}};
data::SpriteSheet g_sheet;

// ---- SFX Designer effects (DMG register images), each on its own channel and held for 30 frames ---------
const u32 kWave[4] = {0x01234567, 0x89ABCDEF, 0xFEDCBA98, 0x76543210};
const audio::DmgSfx kSfx[4] = {
    {audio::DmgChannel::Square1, 0x0000, 0xF080, 0x0700, nullptr, 30},
    {audio::DmgChannel::Square2, 0x0000, 0xF080, 0x0700, nullptr, 30},
    {audio::DmgChannel::Noise, 0x0000, 0xF080, 0x0040, nullptr, 30},
    {audio::DmgChannel::Wave, 0x0000, 0x2000, 0x8400, kWave, 30},
};
constexpr u16 kTouchSfx = audio::kDmgSfxFlag | 0, kJumpSfx = audio::kDmgSfxFlag | 1, kSpawnSfx = audio::kDmgSfxFlag | 2,
              kDestroySfx = audio::kDmgSfxFlag | 3;

// ---- behaviours ----------------------------------------------------------------------------------------
class Counter final : public Behaviour {
public:
    static constexpr u16 kTypeId = 70;
    u16 typeId() const override { return kTypeId; }
    const char* typeName() const override { return "Counter"; }
    void update() override { ++updates; }
    int updates = 0;
};

Behaviour* makeBody(Arena& a) { return a.make<PlatformerBody>(); }
Behaviour* makeHealth(Arena& a) { return a.make<Health>(); }
Behaviour* makePatrol(Arena& a) { return a.make<Patrol>(); }
Behaviour* makeChase(Arena& a) {
    auto* c = a.make<ChasePlayer>();
    if (c) c->range = 64;
    return c;
}
Behaviour* makeCounter(Arena& a) { return a.make<Counter>(); }
template <SoundEvent E, u16 Id>
Behaviour* makeSound(Arena& a) {
    auto* s = a.make<SoundOnEvent>();
    if (s) {
        s->event = E;
        s->sample = Id;
    }
    return s;
}
const BehaviourCreate kBodyB[] = {makeBody};
const BehaviourCreate kSpikyB[] = {makeBody, makeHealth};
const BehaviourCreate kBatB[] = {makeBody, makePatrol, makeChase};
const BehaviourCreate kGhostB[] = {makeChase};
const BehaviourCreate kToucherB[] = {makeSound<SoundEvent::Touch, kTouchSfx>};
const BehaviourCreate kJumperB[] = {makeBody, makeSound<SoundEvent::Jump, kJumpSfx>};
const BehaviourCreate kTemplateB[] = {makeSound<SoundEvent::Spawn, kSpawnSfx>};
const BehaviourCreate kMortalB[] = {makeSound<SoundEvent::Destroy, kDestroySfx>};
const BehaviourCreate kEmptyB[] = {makeCounter};

ActorDef g_actors[14];
SceneDef g_lab;
const SceneDef* g_scenes[1];
GameDef g_game;

void setupData() {
    for (int i = 0; i < 8; ++i) g_tiles[8 + i] = 0x11111111u;
    g_tileset = {};
    g_tileset.gfx = {g_tiles, sizeof g_tiles, 2, 4, false};
    g_tileset.palette = &kPal;
    g_tileset.metatiles = kMeta;
    g_tileset.collision = kCol;
    g_tileset.metatileCount = 6;
    // Ground on row 11 (y 176); a one-way platform on row 7 (x 32..95); a ladder at column 10, rows 6..10
    // (x 160..175, y 96..175); spikes on row 10 at x 224..255; a pool of water at x 320..383, rows 4..10.
    for (int y = 0; y < kH; ++y)
        for (int x = 0; x < kW; ++x) {
            u16 v = None;
            if (y == 11) v = Solid;
            else if (y == 7 && x >= 2 && x <= 5) v = OneWay;
            else if (x == 10 && y >= 6) v = Ladder;
            else if (y == 10 && (x == 14 || x == 15)) v = Hazard;
            else if (x >= 20 && x <= 23 && y >= 4) v = Water;
            g_cells[y * kW + x] = v;
        }
    g_layers[0] = {0, data::LayerType::Tile, 1, false, 256, 256, kW, kH, g_cells, &g_tileset};
    for (int i = 0; i < 32; ++i) g_sprite[i] = 0x11111111u;
    g_sheet = {};
    g_sheet.gfx = {g_sprite, sizeof g_sprite, 4, 4, false};
    g_sheet.palette = &kSprPal;
    g_sheet.width = g_sheet.height = 16;
    g_sheet.sizeCode = 1;
    g_sheet.tilesPerFrame = 4;
    g_sheet.frameCount = 1;
    g_sheet.anims = kAnims;
    g_sheet.animCount = 1;
    g_sheet.originX = 8;
    g_sheet.originY = 16;
    g_sheet.colliderX = 2;
    g_sheet.colliderY = 2;
    g_sheet.colliderW = 12;
    g_sheet.colliderH = 14;
    const ActorKind E = ActorKind::Enemy;
    g_actors[0] = {"Player", ActorKind::Player, &g_sheet, 600, 176, 1, 1, nullptr, {}, nullptr, 0, nullptr, false, -1};
    g_actors[1] = {"OneWay", E, &g_sheet, 64, 80, 0, 1, nullptr, {}, kBodyB, 1, nullptr, false, -1};
    g_actors[2] = {"Climber", E, &g_sheet, 168, 176, 0, 1, nullptr, {}, kBodyB, 1, nullptr, false, -1};
    g_actors[3] = {"Spiky", E, &g_sheet, 240, 176, 0, 1, nullptr, {}, kSpikyB, 2, nullptr, false, -1};
    g_actors[4] = {"Swimmer", E, &g_sheet, 352, 70, 0, 1, nullptr, {}, kBodyB, 1, nullptr, false, -1};
    g_actors[5] = {"Faller", E, &g_sheet, 420, 16, 0, 1, nullptr, {}, kBodyB, 1, nullptr, false, -1};
    g_actors[6] = {"Bat", E, &g_sheet, 500, 176, 0, 1, nullptr, {}, kBatB, 3, nullptr, false, -1};
    g_actors[7] = {"Ghost", E, &g_sheet, 560, 120, 0, 1, nullptr, {}, kGhostB, 1, nullptr, false, -1};
    g_actors[8] = {"Toucher", ActorKind::Pickup, &g_sheet, 40, 176, 0, 1, nullptr, {}, kToucherB, 1, nullptr, false, -1};
    g_actors[9] = {"Jumper", E, &g_sheet, 100, 176, 0, 1, nullptr, {}, kJumperB, 2, nullptr, false, -1};
    g_actors[10] = {"Template", E, &g_sheet, 8, 16, 0, 1, nullptr, {}, kTemplateB, 1, nullptr, true, -1};
    g_actors[11] = {"Mortal", E, nullptr, 130, 176, 0, 1, nullptr, {}, kMortalB, 1, nullptr, false, -1};
    g_actors[12] = {"Empty", ActorKind::Empty, nullptr, 200, 60, 0, 1, nullptr, {}, kEmptyB, 1, nullptr, false, -1};
    g_actors[13] = {"Bare", ActorKind::Empty, nullptr, 210, 60, 0, 1, nullptr, {}, nullptr, 0, nullptr, false, -1};
    g_lab = {"Lab", kW, kH, 0, g_layers, 1, 0, g_actors, 14, {0, 32, 24, true, 0, 0}, -1, false, nullptr, 0,
             nullptr, nullptr, nullptr, 0x7E10, false, false, nullptr};
    g_scenes[0] = &g_lab;
    g_game = {};
    g_game.title = "BEHAVIOURS";
    g_game.scenes = g_scenes;
    g_game.sceneCount = 1;
    g_game.dmgSfx = kSfx;
    g_game.dmgSfxCount = 4;
}

void bootLab() {
    setupData();
    game::boot(g_game);
    for (int i = 0; i < 30 && game::loading(); ++i) frames(1);
}

Actor* find(const char* name) { return actors::find(name); }
PlatformerBody* body(const char* name) { return find(name)->get<PlatformerBody>(); }
}  // namespace

AGB_TEST(one_way_platform_lands_from_above_and_passes_from_below) {
    bootLab();
    Actor* a = find("OneWay");
    frames(40);
    CHECK(a->onGround);
    CHECK_EQ(a->pos.y.floor(), 112);  // the top of the platform row
    // From the ground below, a jump goes up through the platform and lands on top of it.
    a->pos = {fixed(64), fixed(176)};
    frames(2);
    CHECK(a->onGround);
    PlatformerBody* b = body("OneWay");
    b->vy = fixed::lit(-6.5);
    int top = 999;
    bool ceiling = false;
    for (int i = 0; i < 60; ++i) {
        frames(1);
        if (a->pos.y.floor() < top) top = a->pos.y.floor();
        ceiling = ceiling || b->hitCeiling;
    }
    CHECK(!ceiling);
    CHECK(top < 112);
    CHECK(a->onGround);
    CHECK_EQ(a->pos.y.floor(), 112);
}

AGB_TEST(ladder_climbs_without_gravity_and_holds_on_top) {
    bootLab();
    Actor* a = find("Climber");
    PlatformerBody* b = body("Climber");
    frames(2);
    CHECK(b->onLadder);
    // Climbing (set by a script on a ladder): no gravity, the body moves with its own vy.
    b->climbing = true;
    const int y0 = a->pos.y.floor();
    for (int i = 0; i < 20; ++i) {
        b->vy = fixed(-1);
        frames(1);
    }
    CHECK(b->onLadder);
    CHECK(b->climbing);
    CHECK_EQ(a->pos.y.floor(), y0 - 20);
    // The top of a ladder holds a falling body like a one-way platform.
    b->climbing = false;
    b->vy = fixed();
    a->pos = {fixed(168), fixed(60)};
    frames(30);
    CHECK(a->onGround);
    CHECK_EQ(a->pos.y.floor(), 96);
    CHECK(!b->onLadder);
}

AGB_TEST(hazard_tiles_hurt_a_body_with_health) {
    bootLab();
    Health* h = find("Spiky")->get<Health>();
    CHECK_EQ(h->hearts, 3);
    frames(1);
    CHECK(body("Spiky")->touchingHazard);
    CHECK_EQ(h->hearts, 2);
    CHECK(h->invincibleLeft > 0);
    frames(5);
    CHECK_EQ(h->hearts, 2);  // invincible for a while
    frames(60);
    CHECK_EQ(h->hearts, 1);
}

AGB_TEST(water_halves_gravity_and_fall_speed) {
    bootLab();
    frames(20);
    PlatformerBody* swim = body("Swimmer");
    PlatformerBody* fall = body("Faller");
    CHECK(swim->inWater);
    CHECK(!fall->inWater);
    CHECK_EQ(swim->vy.raw, (fixed(4) / 2).raw);  // maxFall / 2 in water
    CHECK_EQ(fall->vy.raw, fixed(4).raw);
}

AGB_TEST(chase_player_follows_in_range) {
    bootLab();
    Actor* bat = find("Bat");
    ChasePlayer* chase = bat->get<ChasePlayer>();
    Patrol* patrol = bat->get<Patrol>();
    // Out of range (100 px): the bat patrols.
    frames(2);
    CHECK(!chase->chasing);
    CHECK(patrol->enabled);
    // The ghost has no body: it moves on both axes toward the player (40, 56 px away, in range).
    Actor* ghost = find("Ghost");
    CHECK(ghost->get<ChasePlayer>()->chasing);
    CHECK(ghost->pos.x.floor() > 560);
    CHECK(ghost->pos.y.floor() > 120);
    // In range: the patrol stops and the bat runs at the player.
    Actor* p = find("Player");
    p->pos = {bat->pos.x + fixed(40), fixed(176)};
    frames(1);
    CHECK(chase->chasing);
    CHECK(!patrol->enabled);
    const int x0 = bat->pos.x.floor();
    frames(20);
    CHECK(bat->pos.x.floor() >= x0 + 12);
    CHECK(!bat->facingLeft);
    p->pos = {bat->pos.x - fixed(40), fixed(176)};
    frames(3);
    CHECK(bat->facingLeft);
    CHECK(bat->get<PlatformerBody>()->vx.raw < 0);
    // Out of range again: the patrol takes over.
    p->pos = {bat->pos.x + fixed(200), fixed(176)};
    frames(1);
    CHECK(!chase->chasing);
    CHECK(patrol->enabled);
}

AGB_TEST(sound_on_event_plays_on_touch_jump_spawn_and_destroy) {
    bootLab();
    frames(40);  // the effect the template played when it spawned has ended
    for (int c = 0; c < 4; ++c) CHECK(!audio::usage().dmg[c]);
    // Touch: the player walks into the coin-like pickup.
    find("Player")->pos = {fixed(40), fixed(176)};
    frames(1);
    CHECK(audio::usage().dmg[0]);  // Square 1
    // Jump: the body leaves the ground going up.
    body("Jumper")->vy = fixed(-3);
    frames(2);
    CHECK(audio::usage().dmg[1]);  // Square 2
    // Spawn: a copy of the hidden template starts.
    CHECK(scene::spawn("Template", 300, 100) != nullptr);
    frames(1);
    CHECK(audio::usage().dmg[3]);  // Noise
    // Destroy.
    CHECK(!audio::usage().dmg[2]);
    find("Mortal")->destroy();
    frames(1);
    CHECK(audio::usage().dmg[2]);  // Wave
    CHECK(find("Mortal") == nullptr);
}

AGB_TEST(empty_actor_spawns_and_runs_its_behaviours) {
    bootLab();
    Actor* e = find("Empty");
    Actor* bare = find("Bare");
    CHECK(e != nullptr && bare != nullptr);
    CHECK(e->kind == ActorKind::Empty);
    CHECK(e->sprite == render::sprites::kNone);  // no sprite
    CHECK_EQ(bare->behaviourCount, 0);
    Counter* c = e->get<Counter>();
    CHECK(c != nullptr);
    const int n = c->updates;
    frames(10);
    CHECK_EQ(c->updates, n + 10);
    // No body: it stays where it was placed.
    CHECK_EQ(e->pos.x.floor(), 200);
    CHECK_EQ(e->pos.y.floor(), 60);
    CHECK_EQ(bare->pos.y.floor(), 60);
}

int main() {
    system::init();
    test::runAll();
}
