// Game, screens and scenes (blueprint 05). The game is a stack of screens; each screen loads over several
// frames (staged load) so the game never freezes. Scenes are screens built from generated SceneDefs.
#pragma once
#include "agb/actor.hpp"
#include "agb/audio.hpp"
#include "agb/data.hpp"
#include "agb/render/effects.hpp"
#include "agb/render/map.hpp"
#include "agb/save.hpp"

namespace agb::ui {
struct MenuDef;
struct Kit;
struct Splash;
struct PageSet;
}  // namespace agb::ui

namespace agb {

class Screen {
public:
    virtual ~Screen() = default;
    // Staged load: called once per frame with step 0, 1, 2... until it returns true.
    virtual bool load(int step) { AGB_UNUSED(step); return true; }
    virtual void enter() {}
    virtual void update() {}
    virtual void exit() {}
    virtual void resume() {}  // the screen above was popped (an overlay closed)
    virtual const char* name() const { return "screen"; }
    // Overlays (pause menu, dialogue box) are pushed over a scene and keep it drawn but paused.
    virtual bool overlay() const { return false; }
};

// ---- scene data written by the build ---------------------------------------------------------------

// World Map link kinds (T-P13, T-O09). On touch: the player overlaps the From actor. Door: the player stands in
// the From actor and presses Up. Respawn: the player overlaps the From actor (a pit, spikes) or loses their last
// heart in this scene. A link without a From actor ("Condition only") fires once its "Only if" condition holds; a
// Respawn link without one fires when the player is lost.
enum class LinkKind : u8 { OnTouch, Door, Respawn };

struct LinkDef {
    s16 fromActor;      // trigger / goal actor index in the scene's ActorDef list, -1 = none (condition only)
    LinkKind kind;
    u16 targetScene;
    s16 spawnX, spawnY; // spawn point in the target scene (pixels)
    render::effects::Transition transition;
    u16 frames;
    bool (*condition)(); // "Only if" (nullptr = always)
};

struct CameraDef {
    s16 followActor;    // -1 = fixed camera
    u8 deadZoneW, deadZoneH;
    bool clampToMap;
    s16 startX, startY;
};

struct SceneDef {
    const char* name;
    u16 widthTiles, heightTiles;  // 16 px tiles
    u8 videoMode;
    const data::MapLayer* layers;
    u8 layerCount;
    s8 collisionLayer;            // index into layers, -1 = none
    const ActorDef* actors;
    u16 actorCount;
    CameraDef camera;
    s16 music;                    // soundbank module id, -1 = none (keep current)
    bool fadeMusicOnExit;         // Scene Settings "Fade on scene change"; the track's MusicMeta must allow it too
    const LinkDef* links;
    u8 linkCount;
    void (*onLoad)();             // map scripts (nullptr = none)
    void (*onEnter)();
    void (*onExit)();
    u16 backdrop;                 // BGR555
    // Video Mode popover (T-O02). Alpha blending (fog, shadows): map layers in front of the collision layer are blended
    // half see-through over everything behind them, and semi-transparent sprites over the layers. Mosaic effect: the
    // scene's layers and sprites take part in the mosaic, which the scene's transitions grow (up to 9 px blocks).
    bool alphaBlend, mosaic;
    const ui::MenuDef* menu;      // title screens: menu opened when the scene has entered (nullptr = none)
    u16 musicVolume = 256;        // World Map inspector music Volume (0..256): the game's music volume for `music`
    const char* page = nullptr;   // a UI page opened when the scene has entered: a title screen (nullptr = none)
    bool pageIsHud = false;       // that page stays over the scene while it plays, and the game goes on (Scene › Hud)
    const data::Picture* picture = nullptr;  // Modes 3, 4, 5: the picture the scene shows (nullptr = the backdrop colour)
};

// A prefab (Make Prefab, prefabs/<Name>.prefab): a const actor template in ROM that Scene.Spawn copies in any scene.
struct PrefabDef {
    const char* name;
    const ActorDef* actor;
};

// Input actions from Project Settings · Input (name -> keys) are part of the game definition.
struct GameDef {
    const char* title;
    const SceneDef* const* scenes;
    u16 sceneCount;
    u16 startScene;
    save::Config save;
    void* state;            // generated game state (save fields), saved by Save.Write
    u16 stateSize;
    const u16* actionKeys;  // Project Settings · Input
    u8 actionCount;
    const void* soundbank;
    u8 audioChannels;
    u8 audioMixKHz;         // Project Settings · Audio mixing rate (0 = 21 kHz)
    void (*init)();         // generated per-game setup (script natives, fonts, strings)
    bool skipSplash;        // Debug builds start without the boot splash
    const void* const* textTables;  // agb::text::Table per language
    u8 textLanguages;
    u8 textFallback;
    const ui::Splash* splash;  // Project Settings · General · Boot splash (nullptr = none)
    const ui::Kit* ui;         // menus, pause overlay and options screen (nullptr = none)
    u8 textLanguage;           // Project Settings · General · Default language (index into textTables)
    bool autosave;             // Project Settings · Save data · Autosave: save::lastSlot() after each scene loads
    const void* dmgSfx;        // agb::audio::DmgSfx table of the SFX Designer effects (nullptr = none)
    u16 dmgSfxCount;
    const PrefabDef* prefabs;  // prefab templates by name (Scene.Spawn falls back to them), nullptr = none
    u16 prefabCount;
    const void* music;         // agb::audio::MusicInfo by module id: MusicMeta volume, loop start, fade (nullptr = none)
    u16 musicCount;
    const ui::PageSet* pages = nullptr;  // UI pages (ui/*.uipage), nullptr = none
    const data::Picture* const* pictures = nullptr;  // Bitmap.Show by picture id, nullptr entries for images no script shows
    u16 pictureCount = 0;
};

namespace game {

constexpr int kMaxScreens = 5;

// Boots a generated game: system, save, audio, input actions, then the start scene. Never returns.
[[noreturn]] void run(const GameDef& def);
// Runs one frame of the screen stack (used by run() and by tests that drive frames by hand).
void frame();
void boot(const GameDef& def);

void push(Screen* screen);  // overlay or sub-screen; the screen must live in persistent storage
void pop();
void finishLoading();  // the screen on top enters now, if it has not yet: a page opened and another opened over it in one frame
void replace(Screen* screen);
Screen* top();
Screen* at(int depth);  // 0 = the bottom of the stack
int depth();
const GameDef& def();

// ---- C# API: Scene.Load / Scene.Pause / Game state -------------------------------------------------
void loadScene(u16 index, int spawnX = -1, int spawnY = -1,
               render::effects::Transition t = render::effects::Transition::Fade, u16 frames = 15);
bool loadSceneByName(const char* name);
u16 currentScene();
bool loading();
// Scene.Pause: opens the pause menu when the game has one (Menu / UI kit), else freezes the scene (actors, physics,
// scripts, tasks, sprite animation) until Scene.Resume or the Start button.
void pause();
void setPaused(bool paused);  // the freeze alone (Scene.Resume clears it)
bool paused();                // frozen, or the pause menu is open
void* state();
const ActorDef* prefab(const char* name);  // nullptr = no prefab with that name

}  // namespace game

// ---- the scene currently running -------------------------------------------------------------------
namespace scene {

const SceneDef* current();
// Camera (C# API: Camera)
int cameraX();
int cameraY();
void setCamera(int x, int y);
void follow(Actor* actor);
void shake(u8 amplitude, u16 frames);
// Tile collision of the collision layer at world pixel coordinates.
data::Collision collisionAt(int x, int y);
u8 tagAt(int x, int y);
bool solidAt(int x, int y);
// Runtime map edits (C# Scene.SetTile / GetTile): one 16 px tile of a layer, by tile coordinates. The layer's
// cells are copied to RAM on the first edit; collision follows the edited cells.
void setTile(int layer, int tileX, int tileY, u16 metatile);
int tile(int layer, int tileX, int tileY);
// A copy of an actor of this scene, by Hierarchy name, at a position (C# Scene.Spawn): bullets, enemies and items
// are hidden template actors in the scene, or prefabs (the scene's actors are searched first). The copy is visible.
Actor* spawn(const char* name, int x, int y);
// Camera Zone actors (Add Actor › Camera Zone): while the followed actor is inside one, the view stays inside the
// zone's box (a zone the size of the screen holds the camera still). Returns the zone's actor index, -1 = none.
int cameraZone();
// Mode 7 floor (C# Scene.Mode7): the scene's affine layer on BG2 seen from a camera at (x, y), heading `angle`
// (0..255), `height` pixels above it, horizon on screen line `horizon`. Called every frame while it shows.
void mode7(fixed x, fixed y, int angle, int height, int horizon);
void stopMode7();
render::MapLayerView* layerView(int index);
const data::Picture* picture(int id);  // Bitmap.Show: a picture by id, nullptr when none
int widthPx();
int heightPx();
// Staged load step names (Profiler "On scene load" and over-budget warnings).
const char* loadStepName(int step);
u32 lastLoadFrames();
u32 loadStepCycles(int step);  // CPU cycles the step took on the last load

}  // namespace scene
}  // namespace agb
