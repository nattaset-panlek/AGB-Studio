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

enum class LinkKind : u8 { OnTouch, Door, Respawn };

struct LinkDef {
    s16 fromActor;      // trigger / goal actor index in the scene's ActorDef list
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
    bool fadeMusicOnExit;
    const LinkDef* links;
    u8 linkCount;
    void (*onLoad)();             // map scripts (nullptr = none)
    void (*onEnter)();
    void (*onExit)();
    u16 backdrop;                 // BGR555
    bool alphaBlend, mosaic;
    const ui::MenuDef* menu;      // title screens: menu opened when the scene has entered (nullptr = none)
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
    void (*init)();         // generated per-game setup (script natives, fonts, strings)
    bool skipSplash;        // Debug builds start without the boot splash
    const void* const* textTables;  // agb::text::Table per language
    u8 textLanguages;
    u8 textFallback;
    const ui::Splash* splash;  // Project Settings · General · Boot splash (nullptr = none)
    const ui::Kit* ui;         // menus, pause overlay and options screen (nullptr = none)
    u8 textLanguage;           // Project Settings · General · Default language (index into textTables)
    bool autosave;             // Project Settings · Save data · Autosave: save::lastSlot() after each scene loads
};

namespace game {

constexpr int kMaxScreens = 4;

// Boots a generated game: system, save, audio, input actions, then the start scene. Never returns.
[[noreturn]] void run(const GameDef& def);
// Runs one frame of the screen stack (used by run() and by tests that drive frames by hand).
void frame();
void boot(const GameDef& def);

void push(Screen* screen);  // overlay or sub-screen; the screen must live in persistent storage
void pop();
void replace(Screen* screen);
Screen* top();
int depth();
const GameDef& def();

// ---- C# API: Scene.Load / Scene.Pause / Game state -------------------------------------------------
void loadScene(u16 index, int spawnX = -1, int spawnY = -1,
               render::effects::Transition t = render::effects::Transition::Fade, u16 frames = 15);
bool loadSceneByName(const char* name);
u16 currentScene();
bool loading();
void setPaused(bool paused);
bool paused();
void* state();

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
// are hidden template actors in the scene. The copy is visible.
Actor* spawn(const char* name, int x, int y);
// Mode 7 floor (C# Scene.Mode7): the scene's affine layer on BG2 seen from a camera at (x, y), heading `angle`
// (0..255), `height` pixels above it, horizon on screen line `horizon`. Called every frame while it shows.
void mode7(fixed x, fixed y, int angle, int height, int horizon);
void stopMode7();
render::MapLayerView* layerView(int index);
int widthPx();
int heightPx();
// Staged load step names (Profiler "On scene load" and over-budget warnings).
const char* loadStepName(int step);
u32 lastLoadFrames();
u32 loadStepCycles(int step);  // CPU cycles the step took on the last load

}  // namespace scene
}  // namespace agb
