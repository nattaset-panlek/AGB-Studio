// Screens and scenes: the screen stack (push / pop / resume / replace), staged-load budgets and the step named in
// overrun warnings, World Map links (on touch, door, respawn, "Only if", condition only), map scripts OnLoad /
// OnEnter / OnExit, Scene.Pause, the camera (dead zone, shake, Camera Zone actors), prefab templates and the Video
// Mode toggles (alpha blending, mosaic).
#include "agb/hw.hpp"

#include "../test.hpp"
#include "agb/api.hpp"
#include "agb/behaviours.hpp"
#include "agb/commit.hpp"
#include "agb/game.hpp"
#include "agb/render/bg.hpp"
#include "agb/text.hpp"

using namespace agb;

namespace {
// ---- debug log -------------------------------------------------------------------------------------
// True when the log written since `from` (a logHead value) contains `text`.
bool loggedSince(u32 from, const char* text) {
    const debug::Channel& ch = debug::channel();
    const u32 head = ch.logHead;
    for (u32 i = from; i < head; ++i) {
        u32 k = 0;
        while (text[k] && i + k < head && ch.log[(i + k) % debug::kLogSize] == text[k]) ++k;
        if (!text[k]) return true;
    }
    return false;
}

void frames(int n, u16 keys = 0) {
    for (int i = 0; i < n; ++i) {
        input::injectState(keys);
        game::frame();
    }
}

// ---- screen probes -------------------------------------------------------------------------------------
class Probe final : public Screen {
public:
    Probe(const char* n, int steps, bool overlay) : name_(n), steps_(steps), overlay_(overlay) {}
    bool load(int step) override {
        ++loads;
        if (step == slowStep) {
            // Busy for more than a frame: the driver must warn and name the step.
            const u32 t0 = profile::cyclesNow();
            while (profile::cyclesNow() - t0 < profile::kCyclesPerFrame * 11 / 10) {}
        }
        return step >= steps_ - 1;
    }
    void enter() override { ++enters; }
    void update() override { ++updates; }
    void exit() override { ++exits; }
    void resume() override { ++resumes; }
    const char* name() const override { return name_; }
    bool overlay() const override { return overlay_; }

    int loads = 0, enters = 0, updates = 0, exits = 0, resumes = 0;
    int slowStep = -1;

private:
    const char* name_;
    int steps_;
    bool overlay_;
};

Probe g_menu("menu", 3, false), g_dialog("dialog", 1, true), g_other("other", 2, false), g_slow("slow probe", 4, false);

// ---- art and map: 0 empty, 1 solid ground --------------------------------------------------------------
AGB_EWRAM_BSS u32 g_tiles[2 * 8];
const u16 kColors[16] = {0x7E10, 0x0200};
const data::Palette kPal{kColors, 1};
const data::Metatile kMeta[2] = {{{0, 0, 0, 0}}, {{1, 1, 1, 1}}};
const data::Collision kCol[2] = {data::Collision::None, data::Collision::Solid};
data::Tileset g_tileset;
constexpr int kW = 40, kH = 10, kRoomW = 16;
u16 g_cells[kW * kH], g_roomCells[kRoomW * kH];
data::MapLayer g_layers[1], g_roomLayers[1];

AGB_EWRAM_BSS u32 g_sprite[4 * 8];
const u16 kSprColors[16] = {0, 0x7FFF};
const data::Palette kSprPal{kSprColors, 1};
const u8 kIdle[] = {0}, kIdleT[] = {10};
const data::AnimDef kAnims[] = {{"idle", kIdle, kIdleT, 1, true, false}};
data::SpriteSheet g_sheet;

// ---- behaviours ----------------------------------------------------------------------------------------
// Counts Update calls (stops while the scene is paused).
class Counter final : public Behaviour {
public:
    static constexpr u16 kTypeId = 70;
    u16 typeId() const override { return kTypeId; }
    const char* typeName() const override { return "Counter"; }
    void update() override { ++updates; }
    int updates = 0;
};

Behaviour* makeBody(Arena& a) { return a.make<PlatformerBody>(); }
Behaviour* makeHealth(Arena& a) {
    auto* h = a.make<Health>();
    h->hearts = h->maxHearts = 2;
    return h;
}
Behaviour* makeCounter(Arena& a) { return a.make<Counter>(); }
Behaviour* makeBullet(Arena& a) {
    auto* p = a.make<Projectile>();
    if (p) {
        p->vx = fixed(2);
        p->lifetime = 60;
    }
    return p;
}
const BehaviourCreate kPlayerB[] = {makeBody, makeHealth, makeCounter};
const BehaviourCreate kBulletB[] = {makeBullet};

// A prefab: a const template in ROM, spawned by name in any scene.
const ActorDef kBulletPrefab = {"Bullet", ActorKind::Empty, &g_sheet, 0, 0, 0, 1, nullptr, {-4, -8, 8, 8}, kBulletB, 1, nullptr, true, -1};
const PrefabDef kPrefabs[] = {{"Bullet", &kBulletPrefab}};

// ---- map scripts and link conditions -------------------------------------------------------------------
char g_hooks[96];
int g_hookN = 0;
bool g_loadSawActors = false, g_loadBeforeStart = false;
int g_exitPlayerX = -1;
void hook(char scene, char what) {
    if (g_hookN + 3 < static_cast<int>(sizeof g_hooks)) {
        g_hooks[g_hookN++] = what;
        g_hooks[g_hookN++] = scene;
        g_hooks[g_hookN++] = ' ';
        g_hooks[g_hookN] = 0;
    }
}
void hubLoad() {
    hook('0', 'L');
    // OnLoad runs after the actors spawned and before their Start methods.
    Actor* p = actors::find("Player");
    g_loadSawActors = p != nullptr;
    g_loadBeforeStart = p && !p->get<PlatformerBody>()->started;
}
void hubEnter() { hook('0', 'E'); }
void hubExit() {
    hook('0', 'X');
    // OnExit runs while the actors are still there (Save.Checkpoint in OnExit needs the player).
    if (Actor* p = actors::find("Player")) g_exitPlayerX = p->pos.x.floor();
}
void roomLoad() { hook('1', 'L'); }
void roomEnter() { hook('1', 'E'); }
void roomExit() { hook('1', 'X'); }
bool g_allowTouch = false, g_goBack = false;
bool allowTouch() { return g_allowTouch; }
bool goBack() { return g_goBack; }

// ---- scenes --------------------------------------------------------------------------------------------
ActorDef g_hubActors[5], g_roomActors[1];
const LinkDef kHubLinks[] = {
    {1, LinkKind::OnTouch, 1, 40, 144, render::effects::Transition::Fade, 8, allowTouch},  // Only if
    {2, LinkKind::Door, 1, 50, 144, render::effects::Transition::Slide, 8, nullptr},
    {3, LinkKind::Respawn, 0, 40, 144, render::effects::Transition::Cut, 1, nullptr},
};
const LinkDef kRoomLinks[] = {
    {-1, LinkKind::OnTouch, 0, 60, 144, render::effects::Transition::Fade, 8, goBack},  // condition only
    {-1, LinkKind::Respawn, 0, 40, 144, render::effects::Transition::Cut, 1, nullptr},  // when the player is lost
};
SceneDef g_hub, g_room;
const SceneDef* g_scenes[2];
GameDef g_game;

void setupData() {
    for (int i = 0; i < 8; ++i) g_tiles[8 + i] = 0x11111111u;
    g_tileset = {};
    g_tileset.gfx = {g_tiles, sizeof g_tiles, 2, 4, false};
    g_tileset.palette = &kPal;
    g_tileset.metatiles = kMeta;
    g_tileset.collision = kCol;
    g_tileset.metatileCount = 2;
    for (int i = 0; i < kW * kH; ++i) g_cells[i] = i >= kW * 9 ? 1 : 0;
    for (int i = 0; i < kRoomW * kH; ++i) g_roomCells[i] = i >= kRoomW * 9 ? 1 : 0;
    g_layers[0] = {0, data::LayerType::Tile, 1, false, 256, 256, kW, kH, g_cells, &g_tileset};
    g_roomLayers[0] = {0, data::LayerType::Tile, 1, false, 256, 256, kRoomW, kH, g_roomCells, &g_tileset};
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
    // Player, a touch zone ("Only if"), a door, a pit (respawn) and a Camera Zone the size of the screen.
    g_hubActors[0] = {"Player", ActorKind::Player, &g_sheet, 40, 144, 1, 1, "idle", {}, kPlayerB, 3, nullptr, false, -1};
    g_hubActors[1] = {"Touch Zone", ActorKind::TriggerZone, nullptr, 100, 144, 0, 1, nullptr, {0, -32, 16, 32}, nullptr, 0, nullptr, false, -1};
    g_hubActors[2] = {"Door", ActorKind::TriggerZone, nullptr, 160, 144, 0, 1, nullptr, {0, -32, 16, 32}, nullptr, 0, nullptr, false, -1};
    g_hubActors[3] = {"Pit", ActorKind::TriggerZone, nullptr, 260, 144, 0, 1, nullptr, {0, -32, 16, 32}, nullptr, 0, nullptr, false, -1};
    g_hubActors[4] = {"Camera Zone", ActorKind::CameraZone, nullptr, 400, 160, 0, 1, nullptr, {0, -160, 240, 160}, nullptr, 0, nullptr, false, -1};
    g_roomActors[0] = {"Player", ActorKind::Player, &g_sheet, 30, 144, 1, 1, "idle", {}, kPlayerB, 3, nullptr, false, -1};
    g_hub = {"Hub", kW, kH, 0, g_layers, 1, 0, g_hubActors, 5, {0, 32, 24, true, 0, 0}, -1, false, kHubLinks, 3,
             hubLoad, hubEnter, hubExit, 0x7E10, false, false, nullptr};
    g_room = {"Room", kRoomW, kH, 0, g_roomLayers, 1, 0, g_roomActors, 1, {0, 32, 24, true, 0, 0}, -1, false, kRoomLinks, 2,
              roomLoad, roomEnter, roomExit, 0x7E10, false, false, nullptr};
    g_scenes[0] = &g_hub;
    g_scenes[1] = &g_room;
    g_game = {};
    g_game.title = "SCENES";
    g_game.scenes = g_scenes;
    g_game.sceneCount = 2;
    g_game.prefabs = kPrefabs;
    g_game.prefabCount = 1;
}

Actor* player() { return actors::find("Player"); }

// Runs frames until the scene `index` has loaded; returns the frames it took.
int waitForScene(u16 index, int max = 90) {
    int n = 0;
    while ((game::currentScene() != index || game::loading()) && n < max) {
        frames(1);
        ++n;
    }
    return n;
}

void bootHub() {
    setupData();
    g_hookN = 0;
    g_hooks[0] = 0;
    game::boot(g_game);
    waitForScene(0);
}

bool hooksAre(const char* expected) {
    const char* a = g_hooks;
    while (*a && *a == *expected) ++a, ++expected;
    if (*a != *expected) debug::logf("  hooks: %s", g_hooks);
    return *a == *expected;
}
}  // namespace

AGB_TEST(screen_stack_push_pop_resume_replace) {
    CHECK_EQ(game::depth(), 0);
    game::push(&g_menu);
    CHECK(game::loading());
    frames(3);  // 3 staged load steps, one per frame
    CHECK_EQ(g_menu.loads, 3);
    CHECK_EQ(g_menu.enters, 1);
    CHECK(!game::loading());
    frames(2);
    CHECK_EQ(g_menu.updates, 2);

    // An overlay goes on top: only the top screen loads and updates.
    game::push(&g_dialog);
    CHECK_EQ(game::depth(), 2);
    CHECK(game::top() == &g_dialog);
    frames(3);
    CHECK_EQ(g_dialog.enters, 1);
    CHECK_EQ(g_dialog.updates, 2);
    CHECK_EQ(g_menu.updates, 2);
    // Pop: the overlay exits, the screen below resumes and updates again.
    game::pop();
    CHECK_EQ(g_dialog.exits, 1);
    CHECK_EQ(g_menu.resumes, 1);
    CHECK(game::top() == &g_menu);
    frames(1);
    CHECK_EQ(g_menu.updates, 3);

    // Replace: the top screen exits and the new one loads in its place.
    game::replace(&g_other);
    CHECK_EQ(g_menu.exits, 1);
    CHECK_EQ(game::depth(), 1);
    frames(3);
    CHECK_EQ(g_other.enters, 1);
    CHECK_EQ(g_other.updates, 1);

    // The stack holds kMaxScreens; one more is refused with a warning.
    for (int i = game::depth(); i < game::kMaxScreens; ++i) game::push(&g_dialog);
    const u32 head = debug::channel().logHead;
    game::push(&g_menu);
    CHECK_EQ(game::depth(), game::kMaxScreens);
    CHECK(loggedSince(head, "too many screens are open"));
    while (game::depth()) game::pop();
    CHECK(game::top() == nullptr);
}

AGB_TEST(staged_load_steps_fit_in_a_frame) {
    setupData();
    game::boot(g_game);
    frames(1);  // the first frame also holds system start-up
    const u32 overruns = profile::stats().overruns;
    int n = 1;
    while (game::loading() && n < 30) {
        frames(1);
        ++n;
    }
    CHECK(!game::loading());
    CHECK(n <= 9);
    CHECK_EQ(profile::stats().overruns, overruns);
    for (int i = 0; i < 7; ++i) {
        debug::logf("  load step %d (%s): %lu cycles", i, scene::loadStepName(i), scene::loadStepCycles(i));
        CHECK(scene::loadStepCycles(i) < profile::kCyclesPerFrame);
    }
    CHECK(scene::lastLoadFrames() <= 9);

    // A step that takes most of a frame is reported with its name, and so is the frame it overran.
    const u32 head = debug::channel().logHead;
    g_slow.slowStep = 2;
    game::push(&g_slow);
    frames(5);
    CHECK_EQ(g_slow.enters, 1);
    CHECK(loggedSince(head, "warning: load step 'tiles to VRAM' of slow probe took"));
    CHECK(loggedSince(head, "(slow probe: tiles to VRAM)"));
    game::pop();
    CHECK(game::top() != nullptr);
    CHECK(!game::loading());
}

AGB_TEST(map_scripts_run_on_load_enter_and_exit) {
    bootHub();
    CHECK(hooksAre("L0 E0 "));
    CHECK(g_loadSawActors);
    CHECK(g_loadBeforeStart);
    // Leaving the scene runs OnExit while the player is still there, then the next scene's OnLoad and OnEnter.
    g_allowTouch = true;
    player()->pos = {fixed(104), fixed(144)};
    waitForScene(1);
    g_allowTouch = false;
    CHECK(hooksAre("L0 E0 X0 L1 E1 "));
    CHECK_EQ(g_exitPlayerX, 104);
}

AGB_TEST(links_on_touch_door_respawn_and_conditions) {
    bootHub();
    // On touch with "Only if": nothing happens while the condition is false.
    g_allowTouch = false;
    Actor* p = player();
    p->pos = {fixed(104), fixed(144)};
    frames(10);
    CHECK_EQ(game::currentScene(), 0);
    CHECK(!game::loading());
    g_allowTouch = true;
    frames(1);
    CHECK(game::loading());
    const int fadeFrames = waitForScene(1);
    g_allowTouch = false;
    CHECK_EQ(game::currentScene(), 1);
    CHECK(fadeFrames >= 8);  // the fade out took its 8 frames
    CHECK_EQ(player()->pos.x.floor(), 40);  // spawn point

    // Condition only (no From actor): goes back once the condition holds.
    frames(5);
    CHECK_EQ(game::currentScene(), 1);
    g_goBack = true;
    waitForScene(0);
    g_goBack = false;
    CHECK_EQ(game::currentScene(), 0);
    CHECK_EQ(player()->pos.x.floor(), 60);

    // Door: standing in it does nothing until the player presses Up.
    p = player();
    p->pos = {fixed(166), fixed(144)};
    frames(10);
    CHECK_EQ(game::currentScene(), 0);
    CHECK(!game::loading());
    frames(1, static_cast<u16>(Key::Up));
    CHECK(game::loading());
    waitForScene(1);
    CHECK_EQ(player()->pos.x.floor(), 50);

    // Respawn without a From actor: when the player loses the last heart.
    g_goBack = false;
    player()->get<Health>()->damage(5);
    frames(1);
    CHECK(game::loading());
    waitForScene(0);
    CHECK_EQ(player()->pos.x.floor(), 40);
    CHECK_EQ(player()->get<Health>()->hearts, 2);  // the scene spawned again

    // Respawn from a pit: the same scene loads again with the player at the spawn point.
    g_hookN = 0;
    g_hooks[0] = 0;
    player()->pos = {fixed(266), fixed(144)};
    frames(1);
    CHECK(game::loading());
    waitForScene(0);
    CHECK(hooksAre("X0 L0 E0 "));
    CHECK_EQ(player()->pos.x.floor(), 40);

    // Falling below the map also loses the player.
    player()->pos = {fixed(330), fixed(200)};
    frames(1);
    CHECK(game::loading());
    waitForScene(0);
    CHECK_EQ(player()->pos.x.floor(), 40);
}

AGB_TEST(scene_pause_freezes_until_start) {
    bootHub();
    Actor* p = player();
    Counter* c = p->get<Counter>();
    frames(2);
    const int updates = c->updates;
    CHECK(updates >= 2);
    CHECK(!api::Scene::Paused());
    // No pause menu in this game: Scene.Pause freezes the actors, behaviours and physics.
    api::Scene::Pause();
    CHECK(api::Scene::Paused());
    p->get<PlatformerBody>()->vx = fixed(1);
    const int x = p->pos.x.floor();
    frames(10, static_cast<u16>(Key::Right));
    CHECK_EQ(c->updates, updates);
    CHECK_EQ(p->pos.x.floor(), x);
    // Start resumes; the frame of the press does not run the scripts (they would see Start and pause again).
    frames(1, static_cast<u16>(Key::Start));
    CHECK(!api::Scene::Paused());
    CHECK_EQ(c->updates, updates);
    frames(3);
    CHECK_EQ(c->updates, updates + 3);
    CHECK(p->pos.x.floor() > x);
    // Scene.Resume from code.
    api::Scene::Pause();
    frames(2);
    CHECK_EQ(c->updates, updates + 3);
    api::Scene::Resume();
    frames(1);
    CHECK_EQ(c->updates, updates + 4);
}

AGB_TEST(camera_dead_zone_shake_and_camera_zone) {
    bootHub();
    Actor* p = player();
    // Dead zone 32 x 24: the camera stays while the player moves inside it, then follows by the overshoot.
    p->pos = {fixed(320), fixed(144)};
    frames(2);
    const int cam = scene::cameraX();
    CHECK_EQ(cam, 320 - 120 - 16);
    p->pos = {fixed(310), fixed(144)};
    frames(1);
    CHECK_EQ(scene::cameraX(), cam);
    p->pos = {fixed(350), fixed(144)};
    frames(1);
    CHECK_EQ(scene::cameraX(), 350 - 120 - 16);
    CHECK_EQ(scene::cameraZone(), -1);

    // Shake moves the view, not the camera, and stops after its frames.
    const int base = scene::cameraX();
    scene::shake(4, 6);
    frames(1);
    CHECK_EQ(scene::cameraX(), base);
    const int sx = scene::layerView(0)->scrollX();
    CHECK(sx == base + 4 || sx == base - 4);
    frames(6);
    CHECK_EQ(scene::layerView(0)->scrollX(), base);

    // Camera Zone (400..640, the size of the screen): inside it the view holds still on the zone.
    p->pos = {fixed(450), fixed(144)};
    frames(1);
    CHECK_EQ(scene::cameraZone(), 4);
    CHECK_EQ(scene::cameraX(), 400);
    CHECK_EQ(scene::cameraY(), 0);
    p->pos = {fixed(560), fixed(144)};
    frames(1);
    CHECK_EQ(scene::cameraX(), 400);
    // Out of the zone the camera follows again.
    p->pos = {fixed(330), fixed(144)};
    frames(1);
    CHECK_EQ(scene::cameraZone(), -1);
    CHECK_EQ(scene::cameraX(), 330 - 120 + 16);
}

AGB_TEST(prefab_templates_spawn_from_rom) {
    bootHub();
    const ActorDef* def = game::prefab("Bullet");
    CHECK(def == &kBulletPrefab);
    CHECK(reinterpret_cast<u32>(def) >= 0x08000000u);  // a const template in ROM
    CHECK(game::prefab("Nothing") == nullptr);
    const int before = actors::count();
    Actor* b = scene::spawn("Bullet", 60, 120);
    CHECK(b != nullptr);
    CHECK_EQ(actors::count(), before + 1);
    CHECK(b->visible);  // the template is hidden; the copy is not
    CHECK(b->is<Projectile>());
    CHECK_EQ(b->pos.x.floor(), 60);
    frames(5);
    CHECK_EQ(b->pos.x.floor(), 70);
    // Scene actors come first: "Player" copies the scene's player, not a prefab.
    Actor* copy = scene::spawn("Player", 80, 144);
    CHECK(copy != nullptr && copy->is<PlatformerBody>());
    const u32 head = debug::channel().logHead;
    CHECK(scene::spawn("Nothing", 0, 0) == nullptr);
    CHECK(loggedSince(head, "no actor or prefab named Nothing"));
}

namespace {
data::MapLayer g_fxLayers[3];
int oamWithMosaic() {
    const commit::ObjAttr* oam = commit::oam();
    int n = 0;
    for (int i = 0; i < 128; ++i)
        if ((oam[i].attr0 & ATTR0_MODE_MASK) != ATTR0_HIDE && (oam[i].attr0 & ATTR0_MOSAIC)) ++n;
    return n;
}
}  // namespace

AGB_TEST(video_mode_alpha_blending_and_mosaic) {
    // T-O02: a fog layer in front of the ground (collision) layer, a back layer behind it; both toggles on.
    setupData();
    g_fxLayers[0] = {1, data::LayerType::Tile, 1, false, 256, 256, kW, kH, g_cells, &g_tileset};  // ground (BG1)
    g_fxLayers[1] = {0, data::LayerType::Tile, 0, false, 256, 256, kW, kH, g_cells, &g_tileset};  // fog (BG0, in front)
    g_fxLayers[2] = {2, data::LayerType::Tile, 2, false, 128, 128, kW, kH, g_cells, &g_tileset};  // back (BG2)
    g_hub.layers = g_fxLayers;
    g_hub.layerCount = 3;
    g_hub.collisionLayer = 0;
    g_hub.alphaBlend = g_hub.mosaic = true;
    game::boot(g_game);
    for (int n = 0; game::loading() && n < 90; ++n) frames(1);
    CHECK(commit::reg(commit::Mosaic) != 0);  // the fade in starts with big blocks
    CHECK_EQ(commit::reg(commit::BldCnt) & 0xC0, BLD_BLACK);
    frames(20);
    CHECK(render::effects::transitionDone());
    // Fog (BG0) 8/16 over the ground, the back layer, the sprites and the backdrop; the fade gave the blend back.
    CHECK_EQ(commit::reg(commit::BldCnt), BLD_STD | 0x01 | ((0x02 | 0x04 | 0x10 | 0x20) << 8));
    CHECK_EQ(commit::reg(commit::BldAlpha), 0x0808);
    CHECK_EQ(commit::reg(commit::Mosaic), 0);  // 1 px blocks once the scene is in
    for (int bg = 0; bg < 3; ++bg) CHECK_EQ(commit::reg(static_cast<commit::Reg>(commit::Bg0Cnt + bg)) & BG_MOSAIC, BG_MOSAIC);
    CHECK(oamWithMosaic() > 0);
    // The text layer (HUD, dialogue) takes no part.
    const int textBg = text::layer();
    CHECK_EQ(textBg, 3);
    CHECK_EQ(commit::reg(commit::Bg3Cnt) & BG_MOSAIC, 0);
    CHECK_EQ(commit::reg(commit::BldCnt) & (0x08 | 0x0800), 0);
    // Leaving grows the blocks again; the next scene has neither toggle.
    game::loadScene(1);
    frames(4);
    CHECK(commit::reg(commit::Mosaic) != 0);
    waitForScene(1);
    frames(20);
    CHECK_EQ(commit::reg(commit::BldCnt), 0);
    CHECK_EQ(commit::reg(commit::Mosaic), 0);
    CHECK_EQ(commit::reg(commit::Bg0Cnt) & BG_MOSAIC, 0);
    CHECK_EQ(oamWithMosaic(), 0);
    // Only the layers in front of the collision layer are fog: with the ground in front, nothing is (shadows only).
    g_hub.layers = g_layers;
    g_hub.layerCount = 1;
    g_hub.mosaic = false;
    game::loadScene(0);
    waitForScene(0);
    frames(20);
    CHECK_EQ(commit::reg(commit::BldCnt), BLD_STD | ((0x01 | 0x10 | 0x20) << 8));
    g_hub.alphaBlend = false;
}

int main() {
    system::init();
    test::runAll();
}
