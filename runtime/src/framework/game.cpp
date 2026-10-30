#include "agb/game.hpp"

#include <tonc.h>

#include "agb/commit.hpp"
#include "agb/debug.hpp"
#include "agb/input.hpp"
#include "agb/natives.hpp"
#include "agb/render/bg.hpp"
#include "agb/render/palette.hpp"
#include "agb/rng.hpp"
#include "agb/script.hpp"
#include "agb/system.hpp"
#include "agb/task.hpp"
#include "agb/text.hpp"
#include "agb/ui.hpp"

namespace agb {

namespace {
const GameDef* g_def = nullptr;
Screen* g_stack[game::kMaxScreens];
int g_depth = 0;
bool g_paused = false;

// ---- the built-in scene screen ------------------------------------------------------------------------
constexpr int kMaxLayers = 4;

struct SceneState {
    const SceneDef* def = nullptr;
    u16 index = 0;
    render::MapLayerView views[kMaxLayers];
    int camX = 0, camY = 0;
    Actor* follow = nullptr;
    u8 shakeAmp = 0;
    u16 shakeFrames = 0;
    int spawnX = -1, spawnY = -1;
    // pending scene change
    bool changePending = false;
    u16 nextScene = 0;
    int nextX = -1, nextY = -1;
    render::effects::Transition transition = render::effects::Transition::Fade;
    u16 transitionFrames = 15;
    u32 loadStartFrame = 0, lastLoadFrames = 0;
    bool loaded = false;
    s8 drawnLayers = -1;  // layers drawn by the spawn step so far (-1 = actors not spawned yet)
};
SceneState g_scene;
void publishGameInfo();
// The current load step runs again next frame (a step split over several frames).
bool g_holdStep = false;
void holdLoadStep() { g_holdStep = true; }
[[maybe_unused]] bool g_handoverChecked = false;

// Hot reload (D-15): the editor writes the hand-over block shortly after the reloaded ROM starts. During its
// first second a Debug build looks for it between scene loads and continues where the previous build was:
// game state restored, then the saved scene loaded with the player at the saved position.
[[maybe_unused]] void applyHandover();

const char* const kLoadSteps[] = {"fade out", "free and reset", "tiles to VRAM", "map and palette", "spawn actors", "OnLoad script", "fade in"};

void clampCamera() {
    const SceneDef* d = g_scene.def;
    if (!d || !d->camera.clampToMap) return;
    const int w = d->widthTiles * 16, h = d->heightTiles * 16;
    g_scene.camX = w <= 240 ? 0 : (g_scene.camX < 0 ? 0 : g_scene.camX > w - 240 ? w - 240 : g_scene.camX);
    g_scene.camY = h <= 160 ? 0 : (g_scene.camY < 0 ? 0 : g_scene.camY > h - 160 ? h - 160 : g_scene.camY);
}

void followCamera() {
    Actor* a = g_scene.follow;
    if (!a || !a->isAlive() || !g_scene.def) return;
    const int px = a->pos.x.floor() - g_scene.camX, py = a->pos.y.floor() - g_scene.camY;
    const int dzw = g_scene.def->camera.deadZoneW, dzh = g_scene.def->camera.deadZoneH;
    const int left = 120 - dzw / 2, right = 120 + dzw / 2, top = 80 - dzh / 2, bottom = 80 + dzh / 2;
    if (px < left) g_scene.camX -= left - px;
    if (px > right) g_scene.camX += px - right;
    if (py < top) g_scene.camY -= top - py;
    if (py > bottom) g_scene.camY += py - bottom;
    clampCamera();
}

class SceneScreen final : public Screen {
public:
    const char* name() const override { return g_scene.def ? g_scene.def->name : "scene"; }

    bool load(int step) override {
        const SceneDef* d = g_scene.def;
        switch (step) {
            case 0:  // 1 fade out (handled by the caller before replace); start the clock
                g_scene.loadStartFrame = system::frame();
                return false;
            case 1:  // 2 free + reset the previous scene's arena and hardware state
                g_scene.drawnLayers = -1;
                actors::reset();
                render::sprites::reset();
                render::bg::reset();
                render::palettes::reset();
                render::effects::reset();
                ui::closeSceneMenu();
                text::clearLabels();
                task::destroyAll();
                script::resetContexts();
                screenArena().reset();
                render::bg::setVideoMode(d->videoMode);
                render::palettes::setBackdrop(d->backdrop);
                return false;
            case 2:  // 3 tiles -> VRAM (tileset loads happen in init)
            case 3: {  // 4 map + palette
                const int first = step == 2 ? 0 : 2;
                for (int i = first; i < d->layerCount && i < kMaxLayers && i < first + 2; ++i)
                    if (!g_scene.views[i].init(d->layers[i])) debug::logf("warning: layer %d of %s could not be loaded", i, d->name);
                return false;
            }
            case 4: {  // 5 spawn actors near the camera, then draw one layer per frame
                if (g_scene.drawnLayers >= 0) {
                    const int i = g_scene.drawnLayers++;
                    g_scene.views[i].setCamera(g_scene.camX, g_scene.camY);
                    g_scene.views[i].drawAll();
                    if (g_scene.drawnLayers < d->layerCount && g_scene.drawnLayers < kMaxLayers) holdLoadStep();
                    return false;
                }
                for (int i = 0; i < d->actorCount; ++i) {
                    Actor* a = actors::spawn(d->actors[i], screenArena());
                    const ActorDef& ad = d->actors[i];
                    const data::TextRun* label = ad.label ? ad.label : ad.labelKey >= 0 ? text::run(static_cast<u16>(ad.labelKey)) : nullptr;
                    if (a && ad.kind == ActorKind::TextLabel && label) {
                        a->label = label;
                        a->textLabel = static_cast<s8>(text::addLabel(ad.x, ad.y, 240 - ad.x, label));
                    }
                }
                g_scene.follow = d->camera.followActor >= 0 ? actors::at(d->camera.followActor) : nullptr;
                if (g_scene.follow && g_scene.spawnX >= 0) g_scene.follow->pos = {fixed(g_scene.spawnX), fixed(g_scene.spawnY)};
                g_scene.camX = d->camera.startX;
                g_scene.camY = d->camera.startY;
                if (g_scene.follow) {
                    g_scene.camX = g_scene.follow->pos.x.floor() - 120;
                    g_scene.camY = g_scene.follow->pos.y.floor() - 80;
                }
                clampCamera();
                // Each layer draws its whole window (about 70k cycles): one layer per frame keeps the frame rate.
                if (d->layerCount > 0) {
                    g_scene.drawnLayers = 0;
                    holdLoadStep();
                }
                return false;
            }
            case 5:  // 6 OnLoad script (runs to completion)
                if (d->onLoad) d->onLoad();
                actors::startNew();
                return false;
            default:  // 7 fade in + OnEnter
                render::effects::beginIn(g_scene.transition, g_scene.transitionFrames);
                if (d->music >= 0 && audio::ready()) {
                    audio::setMusicVolume(fixed(1));
                    audio::playMusic(static_cast<u16>(d->music));
                }
                g_scene.lastLoadFrames = system::frame() - g_scene.loadStartFrame;
                g_scene.loaded = true;
                if (d->onEnter) d->onEnter();
                if (d->menu) ui::openSceneMenu(*d->menu);
                return true;
        }
    }

    void update() override {
        const SceneDef* d = g_scene.def;
        if (g_scene.changePending) {
            if (render::effects::transitionDone() && render::effects::transitionLevel() >= 16) game::replace(this);
            render::effects::update();
            return;
        }
        render::effects::update();
        if (!g_paused && ui::pauseRequested()) {
            ui::openPause();
            return;
        }
        if (!g_paused) {
            profile::begin(profile::Category::Logic);
            if (ui::sceneMenuOpen()) ui::updateSceneMenu();
            actors::startNew();
            if (!script::natives::locked()) actors::updateBehaviours();  // LockAll freezes actor scripts
            script::run();
            task::runAll();
            actors::lateUpdate();  // bodies move here (physics and tile collision)
            text::update();
            actors::touches();
            checkLinks();
            actors::removeDead();
            render::sprites::update();
        }
        profile::begin(profile::Category::Render);
        followCamera();
        int cx = g_scene.camX, cy = g_scene.camY;
        if (g_scene.shakeFrames) {
            --g_scene.shakeFrames;
            cx += (g_scene.shakeFrames & 1) ? g_scene.shakeAmp : -g_scene.shakeAmp;
            cy += (g_scene.shakeFrames & 2) ? g_scene.shakeAmp / 2 : -(g_scene.shakeAmp / 2);
        }
        for (int i = 0; i < d->layerCount && i < kMaxLayers; ++i) {
            g_scene.views[i].setCamera(cx, cy);
            g_scene.views[i].commit();
        }
        render::bg::animateTiles();
        actors::syncSprites(cx, cy);
        render::sprites::build();
        render::palettes::update();
        actors::publishDebug();
        publishGameInfo();
    }

    void exit() override {
        if (g_scene.def && g_scene.def->onExit) g_scene.def->onExit();
    }

private:
    void checkLinks() {
        const SceneDef* d = g_scene.def;
        for (int i = 0; i < d->linkCount; ++i) {
            const LinkDef& l = d->links[i];
            Actor* trigger = actors::at(l.fromActor);
            if (!trigger || !trigger->isAlive()) continue;
            bool fire = false;
            actors::forEach([&](Actor& a) {
                if (&a != trigger && a.kind == ActorKind::Player && a.overlaps(*trigger)) fire = true;
            });
            if (!fire || (l.condition && !l.condition())) continue;
            game::loadScene(l.targetScene, l.spawnX, l.spawnY, l.transition, l.frames);
            return;
        }
    }
};

AGB_EWRAM_BSS u8 g_sceneScreenStorage[sizeof(SceneScreen)] AGB_ALIGN4;
SceneScreen* g_sceneScreen = nullptr;

// Boot splash (Project Settings · General · Boot splash): the image fades in, stays, fades out, then the
// start scene loads.
class SplashScreen final : public Screen {
public:
    static constexpr u16 kFade = 20;
    const char* name() const override { return "boot splash"; }

    bool load(int step) override {
        const ui::Splash& s = *g_def->splash;
        if (step == 0) {
            render::bg::reset();
            render::palettes::reset();
            render::effects::reset();
            screenArena().reset();
            render::bg::setVideoMode(0);
            render::palettes::setBackdrop(s.backdrop);
            render::effects::setBrightness(render::effects::kBg0 | render::effects::kBg1 | render::effects::kBg2 | render::effects::kBg3 |
                                               render::effects::kObj | render::effects::kBackdrop, -16);
            return false;
        }
        if (!s.image || !view_.init(*s.image, render::bg::Size::S32x32)) debug::log("warning: the boot splash image could not be loaded");
        view_.setCamera(0, 0);
        view_.drawAll();
        render::effects::beginIn(render::effects::Transition::Fade, kFade);
        timer_ = 0;
        out_ = false;
        return true;
    }

    void update() override {
        render::effects::update();
        render::palettes::update();
        view_.commit();
        ++timer_;
        if (!out_ && timer_ + kFade >= g_def->splash->frames) {
            render::effects::beginOut(render::effects::Transition::Fade, kFade);
            out_ = true;
        }
        if (out_ && render::effects::transitionDone() && render::effects::transitionLevel() >= 16) {
            view_.release();
            g_scene.nextScene = g_def->startScene;
            g_scene.nextX = g_scene.nextY = -1;
            g_scene.transition = render::effects::Transition::Fade;
            g_scene.transitionFrames = 15;
            game::replace(g_sceneScreen);
        }
    }

private:
    render::MapLayerView view_;
    u16 timer_ = 0;
    bool out_ = false;
};
AGB_EWRAM_BSS u8 g_splashStorage[sizeof(SplashScreen)] AGB_ALIGN4;

debug::GameInfo g_info;

void publishGameInfo() {
    g_info.scene = g_scene.index;
    g_info.stateSize = g_def ? g_def->stateSize : 0;
    g_info.state = g_def ? g_def->state : nullptr;
    Actor* player = nullptr;
    actors::forEach([&](Actor& a) {
        if (!player && a.kind == ActorKind::Player) player = &a;
    });
    if (player) {
        g_info.playerX = static_cast<s16>(player->pos.x.floor());
        g_info.playerY = static_cast<s16>(player->pos.y.floor());
    }
    debug::channel().game = &g_info;
}

// Staged load driver for the top screen.
int g_loadStep = -1;
u32 g_stepCycles[8];

void beginLoad(Screen* s) {
    AGB_UNUSED(s);
    g_loadStep = 0;
}
}  // namespace

namespace {
[[maybe_unused]] void applyHandover() {
#if defined(AGB_DEBUG)
    if (system::frame() > 60) {
        g_handoverChecked = true;
        return;
    }
    auto* h = reinterpret_cast<debug::Handover*>(debug::kHandoverAddress);
    if (h->magic != debug::kHandoverMagic || !g_def) return;
    h->magic = 0;
    g_handoverChecked = true;
    if (g_def->state && h->stateSize == g_def->stateSize) __builtin_memcpy(g_def->state, h->state, g_def->stateSize);
    if (h->scene < g_def->sceneCount) game::loadScene(h->scene, h->playerX, h->playerY, render::effects::Transition::Cut, 1);
    debug::logf("Hot reload: continued in %s", h->scene < g_def->sceneCount ? g_def->scenes[h->scene]->name : "?");
#endif
}
}  // namespace

namespace game {

void boot(const GameDef& def) {
    g_def = &def;
    system::init();
    if (def.save.type != save::Type::None) save::configure(def.save);
    if (def.actionKeys) input::setActionMap(def.actionKeys, def.actionCount);
    audio::init({def.soundbank, def.audioChannels ? def.audioChannels : static_cast<u8>(8)});
    rng::seed(0xA6B5);
    if (def.textTables) {
        text::setTables(reinterpret_cast<const text::Table* const*>(def.textTables), def.textLanguages, def.textFallback);
        text::setLanguage(def.textLanguage);
    }
    if (def.init) def.init();
    g_depth = 0;
    g_paused = false;
    actors::reset();
    ui::setKit(def.ui);
    g_sceneScreen = new (g_sceneScreenStorage) SceneScreen();
#if defined(AGB_DEBUG)
    const bool splash = def.splash && !def.skipSplash;
#else
    const bool splash = def.splash != nullptr;
#endif
    if (splash && def.sceneCount) {
        push(new (g_splashStorage) SplashScreen());
    } else if (def.sceneCount) {
        g_scene.def = def.scenes[def.startScene];
        g_scene.index = def.startScene;
        g_scene.spawnX = g_scene.spawnY = -1;

        g_scene.transition = render::effects::Transition::Fade;
        g_scene.transitionFrames = 15;
        push(g_sceneScreen);
    }
}

void frame() {
#if defined(AGB_DEBUG)
    if (!g_handoverChecked && g_loadStep < 0 && !g_scene.changePending) applyHandover();
#endif
    const u32 overruns = profile::stats().overruns;
    const int stepBefore = g_loadStep;
    input::update();
    if (g_depth == 0) {
        system::waitVBlank();
        return;
    }
    Screen* s = g_stack[g_depth - 1];
    if (g_loadStep >= 0) {
        const u32 t0 = profile::cyclesNow();
        const bool done = s->load(g_loadStep);
        const u32 spent = profile::cyclesNow() - t0;
        if (g_loadStep < 8) g_stepCycles[g_loadStep] = spent;
        if (spent > profile::kCyclesPerFrame * 3 / 4)
            debug::logf("warning: load step '%s' of %s took %lu cycles; split it into smaller steps", scene::loadStepName(g_loadStep), s->name(), spent);
        if (done) {
            g_loadStep = -1;
            s->enter();
        } else if (g_holdStep) {
            g_holdStep = false;
        } else {
            ++g_loadStep;
        }
        // Let the screen draw what it has (fade progress) while loading.
        render::effects::update();
        render::palettes::update();
    } else {
        s->update();
    }
    system::waitVBlank();
    // The first frame also holds system start-up (not a game frame).
    if (profile::stats().overruns != overruns && system::frame() > 1)
        debug::logf("warning: frame %lu took longer than one frame (%s: %s)", system::frame(), s->name(),
                    stepBefore >= 0 ? scene::loadStepName(stepBefore) : "update");
}

void run(const GameDef& def) {
    boot(def);
    for (;;) frame();
}

void push(Screen* screen) {
    if (g_depth >= kMaxScreens) {
        debug::log("warning: screen stack is full");
        return;
    }
    g_stack[g_depth++] = screen;
    beginLoad(screen);
}

void pop() {
    if (g_depth == 0) return;
    g_stack[g_depth - 1]->exit();
    --g_depth;
    if (g_depth) g_stack[g_depth - 1]->resume();
}

void replace(Screen* screen) {
    if (g_depth) {
        g_stack[g_depth - 1]->exit();
        --g_depth;
    }
    if (screen == g_sceneScreen) {
        g_scene.def = g_def->scenes[g_scene.nextScene];
        g_scene.index = g_scene.nextScene;
        g_scene.spawnX = g_scene.nextX;
        g_scene.spawnY = g_scene.nextY;
        g_scene.changePending = false;
        g_scene.loaded = false;
    }
    push(screen);
}

Screen* top() { return g_depth ? g_stack[g_depth - 1] : nullptr; }
int depth() { return g_depth; }
const GameDef& def() { return *g_def; }

void loadScene(u16 index, int spawnX, int spawnY, render::effects::Transition t, u16 frames) {
    if (!g_def || index >= g_def->sceneCount || g_scene.changePending) return;
    g_scene.changePending = true;
    g_scene.nextScene = index;
    g_scene.nextX = spawnX;
    g_scene.nextY = spawnY;
    g_scene.transition = t;
    g_scene.transitionFrames = frames;
    if (g_scene.def && g_scene.def->fadeMusicOnExit && audio::ready()) audio::fadeMusic(fixed(0), frames);
    render::effects::beginOut(t, frames);
}

bool loadSceneByName(const char* name) {
    for (u16 i = 0; g_def && i < g_def->sceneCount; ++i) {
        const char* a = g_def->scenes[i]->name;
        const char* b = name;
        while (*a && *a == *b) ++a, ++b;
        if (*a == *b) {
            loadScene(i);
            return true;
        }
    }
    debug::logf("warning: Scene.Load(\"%s\"): no scene with that name", name);
    return false;
}

u16 currentScene() { return g_scene.index; }
bool loading() { return g_loadStep >= 0 || g_scene.changePending; }
void setPaused(bool p) { g_paused = p; }
bool paused() { return g_paused; }
void* state() { return g_def ? g_def->state : nullptr; }

}  // namespace game

namespace scene {

const SceneDef* current() { return g_scene.def; }
int cameraX() { return g_scene.camX; }
int cameraY() { return g_scene.camY; }
void setCamera(int x, int y) {
    g_scene.camX = x;
    g_scene.camY = y;
    g_scene.follow = nullptr;
    clampCamera();
}
void follow(Actor* a) { g_scene.follow = a; }
void shake(u8 amplitude, u16 frames) {
    g_scene.shakeAmp = amplitude;
    g_scene.shakeFrames = frames;
}

data::Collision collisionAt(int x, int y) {
    const SceneDef* d = g_scene.def;
    if (!d || d->collisionLayer < 0) return data::Collision::None;
    const data::MapLayer& l = d->layers[d->collisionLayer];
    if (x < 0 || y < 0) return data::Collision::Solid;  // map edges are walls
    const int tx = x >> 4, ty = y >> 4;
    if (tx >= l.width) return data::Collision::Solid;
    if (ty >= l.height) return data::Collision::None;  // falling out of the bottom is allowed
    const u16 mt = l.cells[ty * l.width + tx];
    if (!l.tileset || !l.tileset->collision || mt >= l.tileset->metatileCount) return data::Collision::None;
    return l.tileset->collision[mt];
}

u8 tagAt(int x, int y) {
    const SceneDef* d = g_scene.def;
    if (!d || d->collisionLayer < 0 || x < 0 || y < 0) return 0;
    const data::MapLayer& l = d->layers[d->collisionLayer];
    const int tx = x >> 4, ty = y >> 4;
    if (tx >= l.width || ty >= l.height || !l.tileset || !l.tileset->tags) return 0;
    const u16 mt = l.cells[ty * l.width + tx];
    return mt < l.tileset->metatileCount ? l.tileset->tags[mt] : 0;
}

bool solidAt(int x, int y) { return collisionAt(x, y) == data::Collision::Solid; }

render::MapLayerView* layerView(int index) { return index >= 0 && index < kMaxLayers ? &g_scene.views[index] : nullptr; }
int widthPx() { return g_scene.def ? g_scene.def->widthTiles * 16 : 0; }
int heightPx() { return g_scene.def ? g_scene.def->heightTiles * 16 : 0; }
const char* loadStepName(int step) { return step >= 0 && step < 7 ? kLoadSteps[step] : ""; }
u32 lastLoadFrames() { return g_scene.lastLoadFrames; }
u32 loadStepCycles(int step) { return step >= 0 && step < 8 ? g_stepCycles[step] : 0; }

}  // namespace scene
}  // namespace agb
