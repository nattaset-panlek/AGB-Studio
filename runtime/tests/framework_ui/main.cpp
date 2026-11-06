// Menu / UI kit end to end (Figma "In-game" screens): boot splash timing, a title scene menu with a disabled
// Continue, the pause overlay (dimmed scene, restored after), the options screen (hidden layers, sliders,
// button remap with swap) and that overlays give their VRAM back.
#include "agb/hw.hpp"

#include "../test.hpp"
#include "agb/api.hpp"
#include "agb/audio.hpp"
#include "agb/commit.hpp"
#include "agb/game.hpp"
#include "agb/render/bg.hpp"
#include "agb/render/effects.hpp"
#include "agb/render/palette.hpp"
#include "agb/save.hpp"
#include "agb/text.hpp"
#include "agb/ui.hpp"

using namespace agb;

namespace {
struct GameState {
    SaveHeader header;
    s32 score;
};
GameState g_state;

// ---- a 1bpp font: every printable ASCII character is a 5x7 block (advance 6) ---------------------
const u8 kBlock[] = {0xF8, 0xF8, 0xF8, 0xF8, 0xF8, 0xF8, 0xF8};
const data::Glyph kGlyphs[] = {{0, 5, 7, 0, 0, 6}};
u16 g_ascii[128];
data::Font g_font;

// ---- art: tile 0 empty, tile 1 colour 1 ----------------------------------------------------------------
AGB_EWRAM_BSS u32 g_tiles[2 * 8];
const u16 kColors[16] = {0, 0x7FFF};
const data::Palette kPal{kColors, 1};
const data::Metatile kMeta[2] = {{{0, 0, 0, 0}}, {{1, 1, 1, 1}}};
const data::Collision kCol[2] = {data::Collision::None, data::Collision::Solid};
data::Tileset g_tileset;
u16 g_cells[16 * 10];
u16 g_splashCells[30 * 20];
data::MapLayer g_layers[1], g_splashLayer;
ui::Splash g_splash;

// ---- menus -------------------------------------------------------------------------------------------
const ui::ItemDef kTitleItems[] = {
    {ui::ItemKind::Action, {-1, "Start"}, ui::Command::LoadScene, 1, nullptr, 0},
    {ui::ItemKind::Action, {-1, "Continue"}, ui::Command::Continue, 0, nullptr, 0},
    {ui::ItemKind::Action, {-1, "Options"}, ui::Command::Options, 0, nullptr, 0},
};
const ui::MenuDef kTitleMenu{kTitleItems, 3, 11, 9, 12, 6, 14, {-1, nullptr}, false, false};
const ui::Label kActionNames[] = {{-1, "Jump"}, {-1, "Pause"}};
ui::Kit g_kit;
const u16 kActionKeys[] = {static_cast<u16>(Key::A), static_cast<u16>(Key::Start)};

SceneDef g_title, g_level;
const SceneDef* g_scenes[2];
GameDef g_game;

// A HUD Text Label "HUD" (three blocks) across the top-left of the level, under the pause box and the options panel.
const data::PlacedGlyph kHudGlyphs[] = {{0, 0, 0, 0, 0}, {0, 6, 0, 0, 1}, {0, 12, 0, 0, 2}};
data::TextRun g_hudRun;
ActorDef g_hudActor;

void setupData(bool splash) {
    for (auto& a : g_ascii) a = 0xFFFF;
    for (int c = 33; c < 127; ++c) g_ascii[c] = 0;
    g_font = {"block", kGlyphs, kBlock, 1, 1, 8, 7, false, g_ascii};
    for (int i = 0; i < 8; ++i) g_tiles[8 + i] = 0x11111111u;
    g_tileset = {};
    g_tileset.gfx = {g_tiles, sizeof g_tiles, 2, 4, false};
    g_tileset.palette = &kPal;
    g_tileset.metatiles = kMeta;
    g_tileset.collision = kCol;
    g_tileset.metatileCount = 2;
    for (int i = 0; i < 16 * 10; ++i) g_cells[i] = i >= 16 * 9 ? 1 : 0;
    g_layers[0] = {1, data::LayerType::Tile, 1, false, 256, 256, 16, 10, g_cells, &g_tileset};
    for (int i = 0; i < 30 * 20; ++i) g_splashCells[i] = (i / 30 >= 8 && i / 30 < 12 && i % 30 >= 13 && i % 30 < 17) ? 1 : 0;
    g_splashLayer = {0, data::LayerType::Image, 0, false, 256, 256, 30, 20, g_splashCells, &g_tileset};
    g_splash = {&g_splashLayer, 0x1463, 60};

    g_kit = {};
    g_kit.font = &g_font;
    g_kit.ink = 0x0C63;
    g_kit.muted = 0x4E73;
    g_kit.paper = 0x77DF;
    g_kit.rule = 0x0C63;
    g_kit.screen = 0x1463;
    g_kit.screenInk = 0x77BC;
    g_kit.barTrack = 0x2108;
    g_kit.barFill = 0x6D47;
    g_kit.dim = 9;
    g_kit.pauseAction = 1;
    g_kit.titleScene = 0;
    g_kit.paused = {-1, "Paused"};
    g_kit.resume = {-1, "Resume"};
    g_kit.options = {-1, "Options"};
    g_kit.quit = {-1, "Quit to title"};
    g_kit.optionsTitle = {-1, "Options"};
    g_kit.music = {-1, "Music"};
    g_kit.effects = {-1, "Sound effects"};
    g_kit.language = {-1, "Language"};
    g_kit.controls = {-1, "Controls"};
    g_kit.hint = {-1, "A: change   B: back"};
    g_kit.pressKey = {-1, "press a button"};
    g_kit.actionNames = kActionNames;
    g_kit.actionCount = 2;
    g_kit.remap = true;

    g_title = {"Title", 16, 10, 0, g_layers, 1, 0, nullptr, 0, {-1, 0, 0, true, 0, 0}, -1, false, nullptr, 0,
               nullptr, nullptr, nullptr, 0x7DE7, false, false, &kTitleMenu};
    g_level = {"Level", 16, 10, 0, g_layers, 1, 0, nullptr, 0, {-1, 0, 0, true, 0, 0}, -1, false, nullptr, 0,
               nullptr, nullptr, nullptr, 0x7E10, false, false, nullptr};
    g_hudRun = {&g_font, kHudGlyphs, 3, 1, 3};
    g_hudActor = {"HUD", ActorKind::TextLabel, nullptr, 16, 40, 0, 0, nullptr, {0, 0, 0, 0}, nullptr, 0, &g_hudRun, false, -1};
    g_scenes[0] = &g_title;
    g_scenes[1] = &g_level;
    g_game = {};
    g_game.title = "UI KIT";
    g_game.scenes = g_scenes;
    g_game.sceneCount = 2;
    g_game.state = &g_state;
    g_game.stateSize = sizeof g_state;
    g_game.actionKeys = kActionKeys;
    g_game.actionCount = 2;
    g_game.splash = splash ? &g_splash : nullptr;
    g_game.skipSplash = false;
    g_game.ui = &g_kit;
}

void frames(int n, u16 keys = 0) {
    for (int i = 0; i < n; ++i) {
        input::injectState(keys);
        game::frame();
    }
}
// One press: the key goes down for a frame, then up.
void press(Key k) {
    frames(1, static_cast<u16>(k));
    frames(1);
}
bool topIs(const char* name) {
    const Screen* s = game::top();
    if (!s) return false;
    const char* a = s->name();
    while (*a && *a == *name) ++a, ++name;
    return *a == *name;
}
void waitLoaded() {
    for (int i = 0; i < 60 && (game::loading() || !render::effects::transitionDone()); ++i) frames(1);
}
// The text layer's map entry at a tile: the palette bank tells whose tiles are shown there.
u16 entryAt(int tileX, int tileY) {
    const int bg = text::layer();
    const u16* map = commit::bgMap(render::bg::config(bg).screenBlock);
    return map[tileY * 32 + tileX];
}
int bankAt(int tileX, int tileY) { return entryAt(tileX, tileY) >> 12; }
}  // namespace

AGB_TEST(boot_splash_then_title_menu) {
    setupData(true);
    game::boot(g_game);
    CHECK(topIs("boot splash"));
    int n = 0;
    while (topIs("boot splash") && n < 200) {
        frames(1);
        ++n;
    }
    debug::logf("splash took %d frames", n);
    CHECK(n >= 60 && n <= 64);  // 60 frames plus its one-frame load
    waitLoaded();
    CHECK_EQ(game::currentScene(), 0);
    CHECK(ui::sceneMenuOpen());
    // Continue has no save to load: the cursor skips it.
    CHECK_EQ(ui::sceneMenu().cursor(), 0);
    press(Key::Down);
    CHECK_EQ(ui::sceneMenu().cursor(), 2);
    press(Key::Up);
    CHECK_EQ(ui::sceneMenu().cursor(), 0);
    CHECK_EQ(bankAt(11, 9), 14);  // the menu panel is on the text layer with the menu palette
    CHECK_EQ(render::palettes::baseColor(14 * 16 + 1), g_kit.ink);
}

AGB_TEST(debug_builds_can_skip_the_splash) {
    setupData(true);
    g_game.skipSplash = true;
    game::boot(g_game);
    CHECK(!topIs("boot splash"));
    waitLoaded();
    CHECK_EQ(game::currentScene(), 0);
}

AGB_TEST(title_options_and_start) {
    setupData(false);
    game::boot(g_game);
    waitLoaded();
    const u32 tilesBefore = render::bg::tileBytesUsed();
    press(Key::Down);
    press(Key::A);  // Options
    frames(1);
    CHECK(topIs("options"));
    CHECK(!render::bg::visible(1));  // the scene layer is hidden behind the options screen
    CHECK_EQ(render::palettes::baseColor(0), g_kit.screen);
    press(Key::B);
    CHECK(topIs("Title"));
    CHECK(render::bg::visible(1));
    CHECK_EQ(render::palettes::baseColor(0), 0x7DE7);
    CHECK_EQ(bankAt(11, 9), 14);  // the title menu the options panel covered is shown again
    CHECK_EQ(render::bg::tileBytesUsed(), tilesBefore);  // the options screen gave its tiles back
    press(Key::Up);
    press(Key::A);  // Start
    for (int i = 0; i < 60 && game::currentScene() != 1; ++i) frames(1);
    waitLoaded();
    CHECK_EQ(game::currentScene(), 1);
    CHECK(!ui::sceneMenuOpen());
}

AGB_TEST(pause_overlay_dims_and_restores) {
    setupData(false);
    g_title.menu = nullptr;  // start straight in a level scene
    g_game.startScene = 1;
    game::boot(g_game);
    waitLoaded();
    text::layer();  // the text layer (and its blank tile) stays once created
    const u32 tilesBefore = render::bg::tileBytesUsed();
    const u16 bldBefore = commit::reg(commit::BldCnt);
    press(Key::Start);
    frames(1);
    CHECK(topIs("pause menu"));
    CHECK(game::top()->overlay());
    CHECK_EQ(commit::reg(commit::BldCnt) & 0xC0, BLD_BLACK);
    CHECK_EQ(commit::reg(commit::BldY), 9);
    CHECK_EQ(bankAt(8, 5), 14);
    CHECK(api::Scene::Paused());
    press(Key::Start);  // the pause action closes it again
    CHECK(topIs("Level"));
    CHECK(!api::Scene::Paused());
    CHECK_EQ(commit::reg(commit::BldCnt), bldBefore);
    CHECK_EQ(render::bg::tileBytesUsed(), tilesBefore);
    // Scene.Pause from a script opens the same pause menu in a game that has one.
    api::Scene::Pause();
    frames(1);
    CHECK(topIs("pause menu"));
    CHECK(api::Scene::Paused());
    press(Key::Start);
    CHECK(topIs("Level"));
    // Quit to title.
    press(Key::Start);
    frames(1);
    press(Key::Down);
    press(Key::Down);
    press(Key::A);
    for (int i = 0; i < 60 && game::currentScene() != 0; ++i) frames(1);
    waitLoaded();
    CHECK_EQ(game::currentScene(), 0);
    g_title.menu = &kTitleMenu;
}

AGB_TEST(options_remap_swaps_buttons_and_sets_volume) {
    setupData(false);
    g_game.startScene = 1;
    game::boot(g_game);
    waitLoaded();
    press(Key::Start);
    frames(1);
    press(Key::Down);
    press(Key::A);  // pause › Options
    frames(1);
    CHECK(topIs("options"));
    // Rows: Music, Sound effects, Controls (heading), Jump, Pause.
    press(Key::Down);
    press(Key::Down);  // skips the heading
    press(Key::A);
    press(Key::R);     // Jump -> R
    CHECK_EQ(input::actionKeys(0), static_cast<u16>(Key::R));
    press(Key::Down);
    press(Key::A);
    press(Key::R);     // Pause -> R; Jump gets Pause's old Start
    CHECK_EQ(input::actionKeys(1), static_cast<u16>(Key::R));
    CHECK_EQ(input::actionKeys(0), static_cast<u16>(Key::Start));
    // Music volume: up to the first row (the heading is skipped), three steps left.
    for (int i = 0; i < 3; ++i) press(Key::Up);
    press(Key::Left);
    press(Key::Left);
    press(Key::Left);
    CHECK_EQ((audio::musicVolume() * 10).round(), 7);
    press(Key::B);
    CHECK(topIs("pause menu"));
    CHECK_EQ(bankAt(8, 5), 14);  // the pause box is shown again
    CHECK(render::bg::visible(1));
    press(Key::B);
    CHECK(topIs("Level"));
    CHECK_EQ(commit::reg(commit::BldCnt), 0);
}

AGB_TEST(options_persist_on_the_save_chip) {
    // scope log T-D08, P4.12, P2.10: the 8 B options block outside the slots keeps volume and remap over a boot
    setupData(false);
    g_game.startScene = 1;
    g_game.save.type = save::Type::Sram32K;
    g_game.save.slots = 2;
    g_game.save.payloadSize = sizeof g_state;
    save::configure(g_game.save);
    const u8 wipe[save::kOptionsBytes] = {};
    save::deviceWrite(32 * 1024 - save::kOptionsBytes, wipe, sizeof wipe);
    save::Options none;
    CHECK(!save::readOptions(none));  // nothing saved yet
    audio::setMusicVolume(fixed(10) / 10);
    game::boot(g_game);
    waitLoaded();
    press(Key::Start);
    frames(1);
    press(Key::Down);
    press(Key::A);  // pause > Options
    frames(1);
    CHECK(topIs("options"));
    press(Key::Down);
    press(Key::Down);
    press(Key::A);
    press(Key::R);  // Jump -> R
    for (int i = 0; i < 2; ++i) press(Key::Up);
    press(Key::Left);
    press(Key::Left);
    CHECK_EQ((audio::musicVolume() * 10).round(), 8);
    press(Key::B);  // closing the screen writes the block
    save::Options o;
    CHECK(save::readOptions(o));
    CHECK_EQ(o.music, 8);
    CHECK_EQ(o.effects, 10);
    CHECK_EQ(o.remap[0] & 0xF, 4);  // action 0 -> R
    // the block lies outside the slots: writing a slot leaves it alone
    save::erase(0);
    CHECK(save::write(1, &g_state));
    save::Options again;
    CHECK(save::readOptions(again));
    CHECK_EQ(again.music, 8);
    // a new boot starts from the defaults, then applies the saved block
    audio::setMusicVolume(fixed(1));
    input::remapAction(0, static_cast<u16>(Key::A));
    game::boot(g_game);
    CHECK_EQ((audio::musicVolume() * 10).round(), 8);
    CHECK_EQ(input::actionKeys(0), static_cast<u16>(Key::R));
    CHECK(save::bytesUsed() >= save::kOptionsBytes);
}

AGB_TEST(hud_label_comes_back_after_pause_and_options) {
    setupData(false);
    g_level.actors = &g_hudActor;
    g_level.actorCount = 1;
    g_game.startScene = 1;
    game::boot(g_game);
    waitLoaded();
    frames(2);
    // The label's row on the text layer: tiles 2..29 of row 5, in the text palette, none of them the blank tile.
    u16 label[28];
    for (int x = 0; x < 28; ++x) label[x] = entryAt(2 + x, 5);
    CHECK_EQ(bankAt(2, 5), 15);
    CHECK((label[0] & 0x3FF) != text::blankEntry());
    press(Key::Start);
    frames(1);
    CHECK(topIs("pause menu"));
    CHECK_EQ(bankAt(8, 5), 14);  // the pause box covers the middle of the label
    press(Key::Down);
    press(Key::A);  // Options: its panel covers the whole label
    frames(1);
    CHECK(topIs("options"));
    CHECK(entryAt(2, 5) != label[0]);
    press(Key::B);
    CHECK(topIs("pause menu"));
    CHECK_EQ(entryAt(2, 5), label[0]);  // left of the pause box: the label again
    CHECK_EQ(bankAt(8, 5), 14);         // the pause box on top
    press(Key::B);
    CHECK(topIs("Level"));
    int wrong = 0;
    for (int x = 0; x < 28; ++x) wrong += entryAt(2 + x, 5) != label[x];
    CHECK_EQ(wrong, 0);
}

// P2.04: opening, covering, resuming and closing the pause menu changes the text layer's map only through the commit
// queue. Each change is in VRAM after the VBlank that follows it and not before, and no copyNow / fillNow ran while the
// picture showed.
AGB_TEST(pause_and_resume_map_text_in_vblank) {
    setupData(false);
    g_level.actors = &g_hudActor;
    g_level.actorCount = 1;
    g_game.startScene = 1;
    game::boot(g_game);
    waitLoaded();
    frames(2);
    const u32 direct = commit::directWrites();
    const u16 label = entryAt(2, 5), under = entryAt(8, 5);
    // Open: the pause box appears in the VBlank that ends the frame its screen entered.
    ui::openPause();
    CHECK(topIs("pause menu"));
    CHECK_EQ(entryAt(8, 5), under);
    frames(1);
    const u16 pause = entryAt(8, 5);
    CHECK_EQ(pause >> 12, 14);
    // Options cover the pause box.
    press(Key::Down);
    press(Key::A);
    frames(1);
    CHECK(topIs("options"));
    const u16 options = entryAt(8, 5);
    CHECK(options != pause);
    // Back to the pause menu: the options panel is unmapped and the pause box and label mapped again, at the VBlank.
    game::pop();
    CHECK(topIs("pause menu"));
    CHECK_EQ(entryAt(8, 5), options);
    CHECK(commit::queuedBytes() > 0);
    system::waitVBlank();
    CHECK_EQ(commit::queuedBytes(), 0u);
    CHECK_EQ(entryAt(8, 5), pause);
    CHECK_EQ(entryAt(2, 5), label);
    // Close: the pause box is unmapped and the label it covered comes back, at the VBlank.
    game::pop();
    CHECK(topIs("Level"));
    CHECK_EQ(entryAt(8, 5), pause);
    system::waitVBlank();
    CHECK_EQ(entryAt(8, 5), under);
    CHECK_EQ(commit::directWrites(), direct);
}

AGB_TEST(key_names) {
    const char* s = ui::keyName(static_cast<u16>(Key::A) | static_cast<u16>(Key::Start));
    CHECK(s[0] == 'A' && s[1] == '+' && s[2] == 'S' && s[6] == 'T' && s[7] == 0);
    CHECK(ui::keyName(0)[0] == '-');
}

AGB_TEST(text_layer_avoids_the_scene_bgs) {
    setupData(false);
    g_title.menu = nullptr;
    g_layers[0].bg = 0;  // the scene uses BoxStyle::bg itself (Sky Hopper Forest "Sky")
    g_game.startScene = 1;
    game::boot(g_game);
    waitLoaded();
    const int bg = text::layer();
    CHECK_EQ(bg, 3);  // the highest BG the scene leaves free
    CHECK(render::bg::visible(0));
    CHECK(render::bg::visible(3));
}

int main() {
    system::init();
    test::runAll();
}
