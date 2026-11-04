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
#include "agb/ui.hpp"

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
    // Sample ids come from the soundbank; ids with kDmgSfxFlag are SFX Designer effects on the DMG channels.
    static void Play(u16 sample, fixed volume = fixed(1)) {
        if (sample & audio::kDmgSfxFlag) audio::playDmgId(static_cast<u16>(sample & ~audio::kDmgSfxFlag));
        else audio::playSample(sample, volume);
    }
    static void PlayDmg(const audio::DmgSfx& sfx) { audio::playDmg(sfx); }
    static void SetVolume(fixed v) { audio::setEffectsVolume(v); }
};

struct Music {
    static void Play(u16 module) { audio::playMusic(module); }
    static void Stop() { audio::stopMusic(); }
    static void SetVolume(fixed v) { audio::setGameMusicVolume(v); }  // not the player's Options volume
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
    static void LoadAt(u16 index, int x, int y) { game::loadScene(index, x, y); }
    static void Pause() { game::pause(); }
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
    // Reads a slot and goes to the scene and player position it holds; false when the slot is empty or damaged.
    static bool Continue(int slot) {
        if (!Read(slot)) return false;
        const SaveHeader& h = header();
        game::loadScene(h.scene, h.x, h.y);
        return true;
    }
    // Look inside a slot without losing the game in progress (a save list): the Game fields show the slot until EndPreview.
    // Call both in the same frame. False when the slot is empty or damaged.
    static bool BeginPreview(int slot);
    static void EndPreview();
    // Every Game field and the saved flags back to zero (New Game); nothing is written to the chip.
    static void NewGame() {
        if (!game::state()) return;
        u8* p = static_cast<u8*>(game::state());
        for (u32 i = 0; i < save::config().payloadSize; ++i) p[i] = 0;
    }
};

struct Debug {
    static void Log(const char* text) { debug::log(text); }
    template <typename... Args>
    static void Log(const char* fmt, Args... args) { debug::logf(fmt, args...); }
};

struct UI {
    static int CreatePanel(int x, int y, int width, int /*height*/, int /*style*/) {
        return text::addLabel(x, y, width, nullptr, -1);
    }
    static void ShowDialog(u16 textKey, int /*style*/ = 0) {
        text::say(textKey);
    }
    static bool Open(const char* page, int data = 0) { return ui::openPage(page, data); }
    static void Close() { ui::closePage(); }
    static bool IsOpen(const char* page) { return ui::pageOpen(page); }
    static bool AnyOpen() { return ui::pageOpen(); }
    static int Data() { return ui::pageData(); }
    static bool WasChosen(const char* element) {
        static const char* last = nullptr;
        if (const char* now = ui::pageChosen()) last = now;
        if (!last) return false;
        for (const char* a = last; *a || *element; ++a, ++element)
            if (*a != *element) return false;
        last = nullptr;
        return true;
    }
    static void SetText(const char* element, u16 textKey) { ui::setPageText(element, ui::Label{static_cast<s16>(textKey), nullptr}); }
    static void SetHidden(const char* element, bool hidden) { ui::setPageHidden(element, hidden); }
    static void SetAmount(const char* element, int amount) { ui::setPageAmount(element, amount); }
    static int GetAmount(const char* element) { return ui::pageAmount(element); }
    static void SetOn(const char* element, bool on) { ui::setPageOn(element, on); }
    static bool IsOn(const char* element) { return ui::pageOn(element); }
    static void SetNumber(const char* element, int value, int digits = 0) { ui::setPageValues(element, value, 0, 1, digits); }
    static void SetFraction(const char* element, int a, int b) { ui::setPageValues(element, a, b, 2, 0); }
    static void ClearNumber(const char* element) { ui::setPageValues(element, 0, 0, 0, 0); }
    static void SetFocus(const char* element) { ui::setPageFocus(element); }
    static bool IsFocused(const char* element) { return ui::pageFocused(element); }
    static int FocusData() { return ui::pageFocusData(); }
    static void SetInteractable(const char* element, bool on) { ui::setPageInteractable(element, on); }
    static void SetImage(const char* element, const char* image) { ui::setPageImage(element, image); }
    static int InputLength() { return ui::pageInputLength(); }
    static int InputChar(int index) { return ui::pageInputChar(index); }
    static void SetInputChar(int index, int ch) { ui::setPageInputChar(index, ch); }
    static void OpenOptions() { ui::openOptions(); }
    // "Row" + 3 + "Name" = "Row3Name": elements of a list row by number. The text lasts until the next call.
    static const char* Name(const char* prefix, int index, const char* suffix = "") {
        static char buf[32];
        int n = 0;
        for (const char* p = prefix; *p && n < 20; ++p) buf[n++] = *p;
        if (index >= 10) buf[n++] = static_cast<char>('0' + index / 10 % 10);
        buf[n++] = static_cast<char>('0' + index % 10);
        for (const char* p = suffix; *p && n < 31; ++p) buf[n++] = *p;
        buf[n] = 0;
        return buf;
    }
};

struct Text {
    static void SetVar(int index, int value, int digits = 0) { text::setVarNumber(index, value, digits); }
    static void SetVarText(int index, int key) { text::setVarKey(index, static_cast<u16>(key)); }
    static void SetVarChar(int index, int position, int ch) { text::setVarChar(index, position, ch); }
    static void SetSpeed(int charsPerFrame) { text::setSpeed(static_cast<u8>(charsPerFrame)); }
};

struct Random {
    static int Range(int lo, int hi) { return rng::range(lo, hi); }
};

}  // namespace api
}  // namespace agb
