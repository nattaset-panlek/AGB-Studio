#include "agb/game.hpp"

#include <tonc.h>

#include "agb/api.hpp"
#include "agb/commit.hpp"
#include "agb/debug.hpp"
#include "agb/input.hpp"
#include "agb/natives.hpp"
#include "agb/render/affine.hpp"
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
u32 g_pausedFrame = 0;  // frame of Scene.Pause (the Start press that paused does not also resume)

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
    s8 loadedLayers = 0;  // layers loaded by the tiles / map steps so far
    bool labelsMade = false;  // the OnLoad step made the Text Labels (Start and OnLoad may run a frame later)
    data::MapLayer ramLayers[kMaxLayers];  // layers edited at run time (cells in the screen arena)
    bool edited[kMaxLayers] = {};
    s16 zone = -1;  // Camera Zone actor the followed actor is in
    bool pagePending = false;
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

// The Camera Zone actor whose box holds the middle of `a` (its collider, else its origin), or -1.
int zoneOf(const Actor& a) {
    const Box b = a.worldBox();
    const int x = b.w ? b.x + b.w / 2 : a.pos.x.floor(), y = b.h ? b.y + b.h / 2 : a.pos.y.floor();
    int found = -1;
    actors::forEach([&](Actor& z) {
        if (found >= 0 || z.kind != ActorKind::CameraZone || !z.isAlive()) return;
        const Box zb = z.worldBox();
        if (x >= zb.x && x < zb.x + zb.w && y >= zb.y && y < zb.y + zb.h) found = z.index;
    });
    return found;
}

// Keeps one axis of the view inside a zone: a zone smaller than the screen centres the view on it.
int confine(int cam, int zoneStart, int zoneSize, int screen) {
    if (zoneSize <= screen) return zoneStart + (zoneSize - screen) / 2;
    return cam < zoneStart ? zoneStart : cam > zoneStart + zoneSize - screen ? zoneStart + zoneSize - screen : cam;
}

void confineToZone() {
    g_scene.zone = g_scene.follow && g_scene.follow->isAlive() ? static_cast<s16>(zoneOf(*g_scene.follow)) : static_cast<s16>(-1);
    if (const Actor* z = g_scene.zone >= 0 ? actors::at(g_scene.zone) : nullptr) {
        const Box zb = z->worldBox();
        g_scene.camX = confine(g_scene.camX, zb.x, zb.w, 240);
        g_scene.camY = confine(g_scene.camY, zb.y, zb.h, 160);
    }
    clampCamera();
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
    confineToZone();
}

// Text Label actors of the scene: their text drawn on the text layer (load step 6).
void makeLabels(const SceneDef* d) {
    for (int i = 0; i < d->actorCount; ++i) {
        const ActorDef& ad = d->actors[i];
        if (ad.kind != ActorKind::TextLabel && ad.kind != ActorKind::Panel) continue;
        if (ad.hidden) continue;
        Actor* a = actors::at(i);
        const data::TextRun* label = ad.label ? ad.label : ad.labelKey >= 0 ? text::run(static_cast<u16>(ad.labelKey)) : nullptr;
        if (a) {
            a->label = label;
            const int w = ad.collider.w > 0 ? ad.collider.w : (240 - ad.x);
            a->textLabel = static_cast<s8>(text::addLabel(ad.x, ad.y, w, label, ad.label ? static_cast<s16>(-1) : ad.labelKey));
        }
    }
}

// Video Mode popover (T-O02), applied when the scene's layers are loaded and before it fades in.
// "Alpha blending (fog, shadows)": the map layers drawn in front of the collision (gameplay) layer are the first blend
// targets, 8/16 over the layers behind, the sprites and the backdrop; semi-transparent sprites (shadows) blend over the
// layers. The gameplay layer and the layers behind it stay solid, and the text layer (HUD, dialogue) is never blended.
// "Mosaic effect": the scene's layers and sprites take part in the mosaic; the scene's transitions grow the blocks.
void applyVideoEffects(const SceneDef* d) {
    namespace fx = render::effects;
    const data::MapLayer* ground = nullptr;
    if (d->collisionLayer >= 0 && d->collisionLayer < d->layerCount && d->layers[d->collisionLayer].type != data::LayerType::CollisionOnly)
        ground = &d->layers[d->collisionLayer];
    u8 front = 0, behind = fx::kObj | fx::kBackdrop;
    for (int i = 0; i < d->layerCount && i < kMaxLayers; ++i) {
        const data::MapLayer& l = d->layers[i];
        if (l.type == data::LayerType::CollisionOnly) continue;
        const u8 bit = static_cast<u8>(1u << (l.bg & 3));
        if (ground && &l != ground && (l.priority < ground->priority || (l.priority == ground->priority && l.bg < ground->bg))) front |= bit;
        else behind |= bit;
        if (d->mosaic) {
            render::bg::Config c = render::bg::config(l.bg);
            c.mosaic = true;
            render::bg::configure(l.bg, c);
        }
    }
    if (d->alphaBlend) fx::setAlphaBlend(front, behind, 8, 8);
    render::sprites::setMosaicAll(d->mosaic);
    fx::setTransitionMosaic(d->mosaic);
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
                g_scene.loadedLayers = 0;
                g_scene.labelsMade = false;
                g_scene.zone = -1;
                g_paused = false;
                for (bool& e : g_scene.edited) e = false;
                actors::reset();
                render::sprites::reset();
                render::bg::reset();
                render::palettes::reset();
                render::effects::reset();
                ui::closeSceneMenu();
                ui::forgetPageMemory();
                text::clearLabels();
                task::destroyAll();
                script::resetContexts();
                screenArena().reset();
                render::bg::setVideoMode(d->videoMode);
                render::palettes::setBackdrop(d->backdrop);
                return false;
            case 2:  // 3 tiles -> VRAM (tileset loads happen in init)
            case 3: {  // 4 map + palette
                // Two layers per step; after a heavy layer (a 128 x 128 affine map, a large image) the next one waits
                // for the next frame.
                const int first = step == 2 ? 0 : 2;
                const int end = first + 2 < d->layerCount ? first + 2 : d->layerCount;
                if (g_scene.loadedLayers < first) g_scene.loadedLayers = static_cast<s8>(first);
                const u32 t0 = profile::cyclesNow();
                while (g_scene.loadedLayers < end && g_scene.loadedLayers < kMaxLayers) {
                    const int i = g_scene.loadedLayers++;
                    if (!g_scene.views[i].init(d->layers[i])) debug::logf("warning: layer %d of %s could not be loaded (see the warning above), so it is not shown.", i, d->name);
                    if (g_scene.loadedLayers < end && profile::cyclesNow() - t0 > profile::kCyclesPerFrame / 2) {
                        holdLoadStep();
                        break;
                    }
                }
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
                for (int i = 0; i < d->actorCount; ++i) actors::spawn(d->actors[i], screenArena());
                actors::linkParents(d->actors, d->actorCount);
                g_scene.follow = d->camera.followActor >= 0 ? actors::at(d->camera.followActor) : nullptr;
                if (g_scene.follow && g_scene.spawnX >= 0) g_scene.follow->pos = {fixed(g_scene.spawnX), fixed(g_scene.spawnY)};
                g_scene.camX = d->camera.startX;
                g_scene.camY = d->camera.startY;
                if (g_scene.follow) {
                    g_scene.camX = g_scene.follow->pos.x.floor() - 120;
                    g_scene.camY = g_scene.follow->pos.y.floor() - 80;
                }
                confineToZone();
                // Each layer draws its whole window (about 25k cycles) and copies its map to VRAM: one layer per frame.
                if (d->layerCount > 0) {
                    g_scene.drawnLayers = 0;
                    holdLoadStep();
                }
                return false;
            }
            case 5:  // 6 Text Label actors (drawn here, a frame after the actors), then the OnLoad script
                if (!g_scene.labelsMade) {
                    g_scene.labelsMade = true;
                    const u32 t0 = profile::cyclesNow();
                    makeLabels(d);
                    // Many labels in a large font: OnLoad and the Start methods get the next frame.
                    if (profile::cyclesNow() - t0 > profile::kCyclesPerFrame / 3) {
                        holdLoadStep();
                        return false;
                    }
                }
                if (d->onLoad) d->onLoad();
                ui::expectHud(d->page && d->pageIsHud);
                actors::startNew();
                return false;
            default:  // 7 fade in + OnEnter
                applyVideoEffects(d);
                render::effects::beginIn(g_scene.transition, g_scene.transitionFrames);
                // The scene's own track at the scene's music volume; the player's Options music volume stays as it is.
                if (d->music >= 0 && audio::ready()) {
                    audio::setGameMusicVolume(fixed::fromRaw(d->musicVolume));
                    audio::playMusic(static_cast<u16>(d->music));
                }
                g_scene.lastLoadFrames = system::frame() - g_scene.loadStartFrame;
                g_scene.loaded = true;
                // Autosave: gameplay scenes only (a scene with a menu is a title or menu screen).
                if (g_def->autosave && !d->menu && (!d->page || d->pageIsHud) && g_def->state) api::Save::Checkpoint(save::lastSlot());
                if (d->onEnter) d->onEnter();
                if (d->menu) ui::openSceneMenu(*d->menu);
                g_scene.pagePending = d->page != nullptr;  // next frame: a screen pushed during a load would not enter
                return true;
        }
    }

    void update() override {
        const SceneDef* d = g_scene.def;
        if (ui::reopenAfterOptions()) return;
        if (g_scene.pagePending) {
            g_scene.pagePending = false;
            if (d->page) {
                if (d->pageIsHud) ui::openHudPage(d->page);
                else ui::openPage(d->page);
            }
        }
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
        // Frame order of blueprint 04 (debug::Phase); framework_platformer frame_phases_follow_blueprint_order checks it.
        debug::tracePhase(debug::Phase::ScreenUpdate);
        // A scene frozen by Scene.Pause (no pause menu) resumes on Start; the scripts run again from the next frame,
        // so the press that resumed does not pause again.
        bool resumed = false;
        if (g_paused && input::pressed(Key::Start) && system::frame() != g_pausedFrame) {
            g_paused = false;
            resumed = true;
        }
        if (!g_paused && !resumed) {
            profile::begin(profile::Category::Logic);
            if (ui::sceneMenuOpen()) ui::updateSceneMenu();
            actors::startNew();
            debug::tracePhase(debug::Phase::Behaviours);
            if (!script::natives::locked()) actors::updateBehaviours();  // LockAll freezes actor scripts
            debug::tracePhase(debug::Phase::ScriptVm);
            script::run();
            debug::tracePhase(debug::Phase::Tasks);
            task::runAll();
            debug::tracePhase(debug::Phase::Physics);
            actors::lateUpdate();  // bodies move here (physics and tile collision)
            actors::followParents();
            text::update();
            // A finished conversation (or Text.Show the player dismissed) takes the box off the screen.
            if (text::open() && !text::waiting() && !script::cutsceneRunning()) text::close();
            actors::touches();
            checkLinks();
            actors::removeDead();
            debug::tracePhase(debug::Phase::Animation);
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
        actors::syncSprites(cx, cy);
        debug::tracePhase(debug::Phase::BuildOam);
        render::sprites::build();
        debug::tracePhase(debug::Phase::PaletteFade);
        render::palettes::update();
        // Map rows and columns that came into view are drawn into the shadow maps and queued for VBlank. A row the
        // DMA budget cannot take this frame stays dirty and goes next frame (commit::queueCopy). The collision layer
        // goes first: after a big camera move (respawn, Camera.SetPosition) the views redraw at most two whole windows
        // a frame, so the gameplay layer moves with the sprites and a background layer may follow a frame later.
        debug::tracePhase(debug::Phase::Tilemaps);
        const int layers = d->layerCount < kMaxLayers ? d->layerCount : kMaxLayers;
        const int ground = d->collisionLayer >= 0 && d->collisionLayer < layers ? d->collisionLayer : 0;
        for (int k = 0; k < layers; ++k) {
            const int i = k == 0 ? ground : k <= ground ? k - 1 : k;
            g_scene.views[i].setCamera(cx, cy);
            g_scene.views[i].commit();
        }
        render::bg::animateTiles();
        actors::publishDebug();
        publishGameInfo();
    }

    void exit() override {
        if (g_scene.def && g_scene.def->onExit) g_scene.def->onExit();
    }

private:
    // World Map links (see LinkKind). The first link that fires starts the scene change.
    void checkLinks() {
        const SceneDef* d = g_scene.def;
        if (!d->linkCount) return;
        // The player is lost when destroyed (Health ran out), out of hearts, or fallen below the map.
        bool lost = false;
        actors::forEach([&](Actor& a) {
            if (a.kind != ActorKind::Player) return;
            const Health* h = a.get<Health>();
            if (a.dying || (h && h->dead) || a.worldBox().y >= d->heightTiles * 16) lost = true;
        });
        for (int i = 0; i < d->linkCount; ++i) {
            const LinkDef& l = d->links[i];
            bool inside = false;
            if (Actor* trigger = l.fromActor >= 0 ? actors::at(l.fromActor) : nullptr; trigger && trigger->isAlive()) {
                actors::forEach([&](Actor& a) {
                    if (&a != trigger && a.kind == ActorKind::Player && a.isAlive() && a.overlaps(*trigger)) inside = true;
                });
            }
            bool fire;
            switch (l.kind) {
                case LinkKind::Door:
                    fire = inside && api::Input::Pressed(Key::Up);
                    break;
                case LinkKind::Respawn:
                    fire = inside || lost;
                    break;
                default:
                    fire = l.fromActor < 0 ? l.condition != nullptr : inside;
                    break;
            }
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
        if (!s.image || !view_.init(*s.image, render::bg::Size::S32x32)) debug::log("warning: the boot splash could not be shown (no image, or no free background memory). Turn it off in Project Settings (General) if this repeats.");
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

AGB_IWRAM_BSS_NAMED(agb_game_info) debug::GameInfo g_info;

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
u32 g_loadWorstCycles = 0;  // the longest single load-step frame of the current load
int g_loadWorstStep = 0;

void beginLoad(Screen* s) {
    AGB_UNUSED(s);
    g_loadStep = 0;
    g_loadWorstCycles = 0;
    g_loadWorstStep = 0;
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
    audio::init({def.soundbank, def.audioChannels ? def.audioChannels : static_cast<u8>(8), def.audioMixKHz ? def.audioMixKHz : static_cast<u8>(21),
                 static_cast<const audio::MusicInfo*>(def.music), def.musicCount});
    if (def.dmgSfx) audio::setDmgTable(static_cast<const audio::DmgSfx*>(def.dmgSfx), def.dmgSfxCount);
    rng::seed(0xA6B5);
    text::clearLabels();  // a boot starts with no labels (a language switch would redraw a previous game's)
    if (def.textTables) {
        text::setTables(reinterpret_cast<const text::Table* const*>(def.textTables), def.textLanguages, def.textFallback);
        text::setLanguage(def.textLanguage);
    }
    ui::loadOptions();  // the player's saved volumes, language and button remap (the save chip's options block)
    if (def.init) def.init();
    g_depth = 0;
    g_paused = false;
    actors::reset();
    ui::setKit(def.ui);
    ui::setPages(def.pages);
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
    debug::tracePhase(debug::Phase::Input);
    if (g_depth == 0) {
        system::waitVBlank();
        return;
    }
    Screen* s = g_stack[g_depth - 1];
    if (g_loadStep >= 0) {
        const u32 t0 = profile::cyclesNow();
        // Load steps run with the picture covered (faded out, or nothing shown yet at boot): they may write VRAM at once.
        commit::beginStagedLoad();
        const bool done = s->load(g_loadStep);
        commit::endStagedLoad();
        const u32 spent = profile::cyclesNow() - t0;
        if (g_loadStep < 8) g_stepCycles[g_loadStep] = spent;
        if (spent > g_loadWorstCycles) {
            g_loadWorstCycles = spent;
            g_loadWorstStep = g_loadStep;
        }
        if (spent > profile::kCyclesPerFrame * 3 / 4)
            debug::logf("warning: load step '%s' of %s took %lu cycles; split it into smaller steps", scene::loadStepName(g_loadStep), s->name(), spent);
        if (done) {
            g_loadStep = -1;
#if defined(AGB_DEBUG)
            // Console record of the load budget (G-HW.07): every staged-load step must fit in one frame.
            debug::logf("%s loaded: longest load step '%s' %lu cycles (%lu%% of a frame); least VBlank left after a commit %lu cycles",
                        s == g_sceneScreen && g_scene.def ? g_scene.def->name : s->name(), scene::loadStepName(g_loadWorstStep), g_loadWorstCycles,
                        g_loadWorstCycles * 100 / profile::kCyclesPerFrame, profile::vblankFreeMin());
#endif
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
        debug::logf("warning: frame %lu took longer than one frame (%s: %s), so the game slowed down. The Profiler shows why.", system::frame(), s->name(),
                    stepBefore >= 0 ? scene::loadStepName(stepBefore) : "update");
}

void run(const GameDef& def) {
    boot(def);
    for (;;) frame();
}

void push(Screen* screen) {
    if (g_depth >= kMaxScreens) {
        debug::logf("warning: too many screens are open on top of each other (%d), so a new menu or screen was not opened. Close one first.", kMaxScreens);
        return;
    }
    g_stack[g_depth++] = screen;
    beginLoad(screen);
}

void finishLoading() {
    if (g_loadStep < 0 || !g_depth) return;
    g_loadStep = -1;
    g_stack[g_depth - 1]->enter();
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
Screen* at(int depth) { return depth >= 0 && depth < g_depth ? g_stack[depth] : nullptr; }
const GameDef& def() { return *g_def; }

void loadScene(u16 index, int spawnX, int spawnY, render::effects::Transition t, u16 frames) {
    if (!g_def || index >= g_def->sceneCount || g_scene.changePending) return;
    g_scene.changePending = true;
    g_scene.nextScene = index;
    g_scene.nextX = spawnX;
    g_scene.nextY = spawnY;
    g_scene.transition = t;
    g_scene.transitionFrames = frames;
    // Fade on scene change: the scene's setting and the playing track's MusicMeta must both ask for it.
    if (g_scene.def && g_scene.def->fadeMusicOnExit && audio::ready() && audio::musicFadesOnSceneChange()) audio::fadeMusic(fixed(0), frames);
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
    debug::logf("warning: Scene.Load(\"%s\"): no scene has that name, so nothing loaded. Use a scene name from the World Map.", name);
    return false;
}

u16 currentScene() { return g_scene.index; }
bool loading() { return g_loadStep >= 0 || g_scene.changePending; }

void pause() {
    const ui::Kit* k = ui::kit();
    // While a scene loads (Scene.Pause in OnLoad or Start) an overlay would cut the staged load short: freeze instead.
    if (k && k->pauseAction >= 0 && !loading()) {
        if (!ui::overlayOpen()) ui::openPause();
        return;
    }
    if (!g_paused) g_pausedFrame = system::frame();
    g_paused = true;
}

void setPaused(bool p) {
    if (p && !g_paused) g_pausedFrame = system::frame();
    g_paused = p;
}

bool paused() { return g_paused || ui::overlayOpen(); }
void* state() { return g_def ? g_def->state : nullptr; }

const ActorDef* prefab(const char* name) {
    for (u16 i = 0; g_def && name && i < g_def->prefabCount; ++i) {
        const char* a = g_def->prefabs[i].name;
        const char* b = name;
        while (*a && *a == *b) ++a, ++b;
        if (*a == *b) return g_def->prefabs[i].actor;
    }
    return nullptr;
}

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

// The collision layer as it is now (edited cells included).
const data::MapLayer& collisionLayer(const SceneDef* d) {
    const int i = d->collisionLayer;
    return i < kMaxLayers && g_scene.edited[i] ? g_scene.ramLayers[i] : d->layers[i];
}

data::Collision collisionAt(int x, int y) {
    const SceneDef* d = g_scene.def;
    if (!d || d->collisionLayer < 0) return data::Collision::None;
    const data::MapLayer& l = collisionLayer(d);
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
    const data::MapLayer& l = collisionLayer(d);
    const int tx = x >> 4, ty = y >> 4;
    if (tx >= l.width || ty >= l.height || !l.tileset || !l.tileset->tags) return 0;
    const u16 mt = l.cells[ty * l.width + tx];
    return mt < l.tileset->metatileCount ? l.tileset->tags[mt] : 0;
}

bool solidAt(int x, int y) { return collisionAt(x, y) == data::Collision::Solid; }

void setTile(int layer, int tx, int ty, u16 metatile) {
    const SceneDef* d = g_scene.def;
    if (!d || layer < 0 || layer >= d->layerCount || layer >= kMaxLayers) return;
    const data::MapLayer& src = d->layers[layer];
    if (tx < 0 || ty < 0 || tx >= src.width || ty >= src.height) return;
    if (!g_scene.edited[layer]) {
        const u32 cells = static_cast<u32>(src.width) * src.height;
        auto* ram = static_cast<u16*>(screenArena().alloc(cells * 2));
        if (!ram) return;
        for (u32 i = 0; i < cells; ++i) ram[i] = src.cells[i];
        g_scene.ramLayers[layer] = src;
        g_scene.ramLayers[layer].cells = ram;
        g_scene.edited[layer] = true;
        g_scene.views[layer].retarget(&g_scene.ramLayers[layer]);
    }
    const_cast<u16*>(g_scene.ramLayers[layer].cells)[ty * src.width + tx] = metatile;
    g_scene.views[layer].redrawMetatile(tx, ty);
}

void mode7(fixed x, fixed y, int angle, int height, int horizon) {
    render::affine::Mode7 m;
    m.camX = x;
    m.camY = y;
    m.angle = static_cast<u8>(angle);
    m.camHeight = height;
    m.horizon = horizon;
    render::affine::mode7(m);
}

void stopMode7() { render::affine::stopMode7(); }

Actor* spawn(const char* name, int x, int y) {
    const SceneDef* d = g_scene.def;
    if (!d || !name) return nullptr;
    const ActorDef* def = nullptr;
    for (int i = 0; i < d->actorCount && !def; ++i) {
        const char* a = d->actors[i].name;
        const char* b = name;
        while (*a && *a == *b) ++a, ++b;
        if (*a == *b) def = &d->actors[i];
    }
    if (!def) def = game::prefab(name);
    if (!def) {
        debug::logf("warning: Spawn: %s has no actor or prefab named %s, so nothing spawned. Add a prefab or hidden actor of that name.", d->name, name);
        return nullptr;
    }
    Actor* actor = actors::spawnAt(*def, screenArena(), x, y);
    if (actor) {
        actor->setVisible(true);
        if (def->parent >= 0) actor->setParent(actors::at(def->parent));
    }
    else debug::logf("warning: Spawn %s: all %d actor slots are in use, so it was not spawned. Destroy actors you no longer need.", name, actors::kMaxActors);
    return actor;
}

int cameraZone() { return g_scene.zone; }

int tile(int layer, int tx, int ty) {
    const SceneDef* d = g_scene.def;
    if (!d || layer < 0 || layer >= d->layerCount || layer >= kMaxLayers) return 0;
    const data::MapLayer& l = g_scene.edited[layer] ? g_scene.ramLayers[layer] : d->layers[layer];
    if (tx < 0 || ty < 0 || tx >= l.width || ty >= l.height) return 0;
    return l.cells[ty * l.width + tx];
}

render::MapLayerView* layerView(int index) { return index >= 0 && index < kMaxLayers ? &g_scene.views[index] : nullptr; }
int widthPx() { return g_scene.def ? g_scene.def->widthTiles * 16 : 0; }
int heightPx() { return g_scene.def ? g_scene.def->heightTiles * 16 : 0; }
const char* loadStepName(int step) { return step >= 0 && step < 7 ? kLoadSteps[step] : ""; }
u32 lastLoadFrames() { return g_scene.lastLoadFrames; }
u32 loadStepCycles(int step) { return step >= 0 && step < 8 ? g_stepCycles[step] : 0; }

}  // namespace scene
}  // namespace agb
