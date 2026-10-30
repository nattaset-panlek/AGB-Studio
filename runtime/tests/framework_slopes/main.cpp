// Platformer Body on 45-degree slopes (Component Picker "Platformer Body ... slopes, one-way"): a body walks
// up a SlopeUp tile, steps onto the plateau, walks down a SlopeDown tile without leaving the ground, and
// lands on the flat ground again.
#include "../test.hpp"
#include "agb/api.hpp"
#include "agb/behaviours.hpp"
#include "agb/game.hpp"

using namespace agb;

namespace {
struct GameState {
    SaveHeader header;
};
GameState g_state;

AGB_EWRAM_BSS u32 g_tiles[2 * 8];
const u16 kColors[16] = {0x7E10, 0x0200};
const data::Palette kPal{kColors, 1};
// Metatiles: 0 empty, 1 solid, 2 slope up, 3 slope down.
const data::Metatile kMeta[4] = {{{0, 0, 0, 0}}, {{1, 1, 1, 1}}, {{1, 1, 1, 1}}, {{1, 1, 1, 1}}};
const data::Collision kCol[4] = {data::Collision::None, data::Collision::Solid, data::Collision::SlopeUp, data::Collision::SlopeDown};
data::Tileset g_tileset;
constexpr int kW = 24, kH = 10;
u16 g_cells[kW * kH];
data::MapLayer g_layers[1];

AGB_EWRAM_BSS u32 g_sprite[4 * 8];
const u16 kSprColors[16] = {0, 0x7FFF};
const data::Palette kSprPal{kSprColors, 1};
const u8 kIdle[] = {0}, kIdleT[] = {10};
const data::AnimDef kAnims[] = {{"idle", kIdle, kIdleT, 1, true, false}};
data::SpriteSheet g_sheet;

// Walks right at 1 px per frame, like a player holding Right.
class Walker final : public Behaviour {
public:
    static constexpr u16 kTypeId = 70;
    u16 typeId() const override { return kTypeId; }
    const char* typeName() const override { return "Walker"; }
    PlatformerBody* body = nullptr;
    void start() override { body = actor->get<PlatformerBody>(); }
    void update() override { body->vx = fixed(1); }
};

Behaviour* makeWalker(Arena& a) { return a.make<Walker>(); }
Behaviour* makeBody(Arena& a) { return a.make<PlatformerBody>(); }
const BehaviourCreate kWalkerB[] = {makeWalker, makeBody};

ActorDef g_actors[1];
SceneDef g_scene;
const SceneDef* g_scenes[1];
GameDef g_game;

void setupData() {
    for (int i = 0; i < 8; ++i) g_tiles[8 + i] = 0x11111111u;
    g_tileset = {};
    g_tileset.gfx = {g_tiles, sizeof g_tiles, 2, 4, false};
    g_tileset.palette = &kPal;
    g_tileset.metatiles = kMeta;
    g_tileset.collision = kCol;
    g_tileset.metatileCount = 4;
    // Ground on row 9; a plateau on row 8 from x 8 to 11 with a slope up at x 7 and a slope down at x 12.
    for (int i = 0; i < kW * kH; ++i) g_cells[i] = i >= kW * 9 ? 1 : 0;
    for (int x = 8; x <= 11; ++x) g_cells[8 * kW + x] = 1;
    g_cells[8 * kW + 7] = 2;
    g_cells[8 * kW + 12] = 3;
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
    g_actors[0] = {"Player", ActorKind::Player, &g_sheet, 40, 144, 1, 1, "idle", {}, kWalkerB, 2, nullptr, false, -1};
    g_scene = {"Hills", kW, kH, 0, g_layers, 1, 0, g_actors, 1, {0, 32, 24, true, 0, 0}, -1, false, nullptr, 0,
               nullptr, nullptr, nullptr, 0x7E10, false, false, nullptr};
    g_scenes[0] = &g_scene;
    g_game = {};
    g_game.title = "SLOPES";
    g_game.scenes = g_scenes;
    g_game.sceneCount = 1;
    g_game.state = &g_state;
    g_game.stateSize = sizeof g_state;
}

void frames(int n) {
    for (int i = 0; i < n; ++i) {
        input::injectState(0);
        game::frame();
    }
}

int foot(Actor* p) {
    const Box b = p->worldBox();
    return b.y + b.h - 1;
}
int footX(Actor* p) {
    const Box b = p->worldBox();
    return b.x + b.w / 2;
}
}  // namespace

AGB_TEST(body_walks_up_and_down_slopes) {
    setupData();
    game::boot(g_game);
    for (int i = 0; i < 30 && game::loading(); ++i) frames(1);
    Actor* p = actors::find("Player");
    CHECK(p != nullptr);
    auto* body = p->get<PlatformerBody>();
    // Walk until the foot is in the middle of the slope up (tile x 7: pixels 112-127).
    int guard = 0;
    while (footX(p) < 120 && guard++ < 200) frames(1);
    const int expectUp = 128 + 15 - (footX(p) & 15);
    debug::logf("on slope up: foot %d,%d surface %d ground %d", footX(p), foot(p), expectUp, body->onGround);
    CHECK(body->onGround);
    CHECK(foot(p) >= expectUp - 1 && foot(p) <= expectUp + 1);
    // Onto the plateau (top y 128): no wall stops the body at the top of the slope.
    while (footX(p) < 150 && guard++ < 400) frames(1);
    CHECK(footX(p) >= 150);
    CHECK_EQ(foot(p), 127);
    CHECK(body->onGround);
    // Down the slope (tile x 12: pixels 192-207) without leaving the ground on any frame.
    int airborne = 0;
    while (footX(p) < 206 && guard++ < 600) {
        frames(1);
        if (footX(p) >= 192 && !body->onGround) ++airborne;
    }
    const int expectDown = 128 + (footX(p) & 15);
    debug::logf("on slope down: foot %d,%d surface %d airborne frames %d", footX(p), foot(p), expectDown, airborne);
    CHECK_EQ(airborne, 0);
    CHECK(foot(p) >= expectDown - 1 && foot(p) <= expectDown + 1);
    // Back on the flat ground (top y 144).
    while (footX(p) < 240 && guard++ < 800) frames(1);
    frames(10);
    CHECK_EQ(foot(p), 143);
    CHECK(body->onGround);
}

int main() {
    system::init();
    test::runAll();
}
