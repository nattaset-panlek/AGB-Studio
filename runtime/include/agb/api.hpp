// C++ side of the C# scripting API (namespace GBA). Translated scripts call these names one to one:
// Input.Pressed(Key.A) -> agb::api::Input::Pressed(agb::Key::A), Sound.Play("jump") -> Sound::Play(id).
// Members follow the Scripting API page (T-P18) and the Figma code samples.
#pragma once
#include "agb/actor.hpp"
#include "agb/assert.hpp"
#include "agb/behaviours.hpp"
#include "agb/game.hpp"
#include "agb/input.hpp"
#include "agb/rng.hpp"
#include "agb/script.hpp"
#include "agb/text.hpp"

namespace agb {

// First bytes of every generated save payload: where Save.Checkpoint() puts the player back.
struct SaveHeader {
    u16 scene;
    s16 x, y;
    u16 flags;
};

namespace api {

struct Input {
    static bool Pressed(Key k) { return !script::cutsceneRunning() && input::pressed(k); }
    static bool Held(Key k) { return !script::cutsceneRunning() && input::held(k); }
    static bool Released(Key k) { return input::released(k); }
    static fixed AxisX() { return script::cutsceneRunning() ? fixed() : fixed(input::axisX()); }
    static fixed AxisY() { return script::cutsceneRunning() ? fixed() : fixed(input::axisY()); }
    static bool AnyKey() { return input::anyKey(); }
    static bool Action(int action) { return input::actionPressed(action); }
};

struct Sound {
    static void Play(u16 sample, fixed volume = fixed(1)) { audio::playSample(sample, volume); }
    static void PlayDmg(const audio::DmgSfx& sfx) { audio::playDmg(sfx); }
    static void SetVolume(fixed v) { audio::setEffectsVolume(v); }
};

struct Music {
    static void Play(u16 module) { audio::playMusic(module); }
    static void Stop() { audio::stopMusic(); }
    static void SetVolume(fixed v) { audio::setMusicVolume(v); }
    static void Fade(fixed to, int frames) { audio::fadeMusic(to, static_cast<u16>(frames)); }
    static bool Playing() { return audio::musicPlaying(); }
};

struct Camera {
    static int X() { return scene::cameraX(); }
    static int Y() { return scene::cameraY(); }
    static void SetPosition(int x, int y) { scene::setCamera(x, y); }
    static void Follow(Actor* a) { scene::follow(a); }
    static void Shake(int amplitude, int frames) { scene::shake(static_cast<u8>(amplitude), static_cast<u16>(frames)); }
};

struct Scene {
    static void Load(u16 index) { game::loadScene(index); }
    static void Load(const char* name) { game::loadSceneByName(name); }
    static void Pause() { game::setPaused(true); }
    static void Resume() { game::setPaused(false); }
    static bool Paused() { return game::paused(); }
    static int Current() { return game::currentScene(); }
    static Actor* Find(const char* name) { return actors::find(name); }
    static Actor* Spawn(const ActorDef& def, int x, int y) { return actors::spawnAt(def, screenArena(), x, y); }
};

struct Save {
    static SaveHeader& header() { return *static_cast<SaveHeader*>(game::state()); }
    static bool Write(int slot) { return game::state() && save::write(slot, game::state()); }
    static bool Read(int slot) { return game::state() && save::read(slot, game::state()); }
    static bool Exists(int slot) { return save::exists(slot); }
    static void Erase(int slot) { save::erase(slot); }
    // Remembers the current scene and the player's position, then writes slot 0 (Scene Settings OnExit).
    static void Checkpoint() { Checkpoint(0); }
    static void Checkpoint(int slot);
    static u16 scene() { return game::state() ? header().scene : 0; }
};

struct Debug {
    static void Log(const char* text) { debug::log(text); }
    template <typename... Args>
    static void Log(const char* fmt, Args... args) { debug::logf(fmt, args...); }
};

struct Random {
    static int Range(int lo, int hi) { return rng::range(lo, hi); }
};

}  // namespace api
}  // namespace agb
