// Built-in natives of the Event Script VM (blueprint 07): the cutscene API of the C# scripts (Say, Choice,
// Move, LockAll, ...) and the Dialogue Editor commands. Say, Choice and Text.Show use the agb::text dialogue
// box unless a hook replaces it; when the game has no string tables they log what they would show and do
// not wait, so scripts also run in tests. Inventory (GiveItem) and actor freezing (LockAll) use hooks.
#pragma once
#include "agb/fixed.hpp"
#include "agb/script.hpp"

namespace agb {
class Actor;
}

namespace agb::script::natives {

// Single source of the built-in natives: tools/AgbStudio.Scripting reads this list (name, waits, note) and
// every generated native table starts with these entries, in this order. Append only.
#define AGB_BUILTIN_NATIVES(X)                                                                   \
    X(Say, true, "text, speaker, portrait, speed, blip: shows a line, waits until it is closed") \
    X(Choice, true, "option texts (-1 hidden): shows a menu, waits, returns the picked index")  \
    X(Move, true, "actor, path: moves the actor along a path, waits until it arrives")         \
    X(FadeIn, true, "seconds (fixed raw): fades the screen in, waits")                          \
    X(FadeOut, true, "seconds (fixed raw): fades the screen out, waits")                        \
    X(LockAll, false, "freezes the player and the other actors")                                \
    X(UnlockAll, false, "ends LockAll")                                                         \
    X(PlaySound, false, "sound, volume (fixed raw, optional)")                                  \
    X(PlayMusic, false, "music module")                                                         \
    X(StopMusic, false, "stops the music")                                                      \
    X(ShakeCamera, false, "amplitude, frames")                                                  \
    X(GoToScene, false, "scene index")                                                          \
    X(GiveItem, false, "table, item, count (Give item hook)")                                   \
    X(ShowText, false, "text: shows a string without waiting")                                  \
    X(FindActor, false, "name index: returns the actor slot or -3")                             \
    X(Log, false, "name index: writes a line to the Console")                                   \
    X(Random, false, "lo, hi: random number, both included")                                    \
    X(SaveWrite, false, "slot: returns 1 when written")                                         \
    X(SaveRead, false, "slot: returns 1 when read")                                             \
    X(SaveCheckpoint, false, "remembers the scene and the player position, writes slot 0")   \
    X(UiChoose, true, "wake on move (0 or 1): waits for the top UI page, returns the Data of the control chosen, -1 for B, -2 when the cursor moved") \
    X(BitmapClear, false, "color: fills the page being drawn")                                 \
    X(BitmapSetPixel, false, "x, y, color")                                                     \
    X(BitmapFillRect, false, "x, y, width, height, color")                                      \
    X(BitmapShow, false, "picture id: draws a picture into the page being drawn")               \
    X(BitmapFlip, false, "shows the page just drawn (Modes 4 and 5)")                           \
    X(BitmapRgb, false, "r, g, b (0..31): returns the BGR555 colour")                           \
    X(BitmapSetPaletteColor, false, "index (1..255), color: Mode 4 palette")

#define AGB_NATIVE_DECLARE(name, waits, note) s32 name(Context& ctx, const s32* args, int argc);
AGB_BUILTIN_NATIVES(AGB_NATIVE_DECLARE)
#undef AGB_NATIVE_DECLARE

// Entry of a generated native table: AGB_BUILTIN_NATIVES(AGB_NATIVE_ENTRY)
#define AGB_NATIVE_ENTRY(name, waits, note) {#name, ::agb::script::natives::name, waits},

// Actor arguments: >= 0 actor slot, kPlayer the first Player actor, kNoActor none.
constexpr s32 kPlayer = -1;
constexpr s32 kNoActor = -3;

// ---- hooks (the dialogue box, inventory and actor freezing live in the game) ------------------------
struct SayRequest {
    u16 text;      // string id
    s16 speaker;   // speaker id, -1 = none
    s16 portrait;  // portrait id, -1 = speaker default
    s8 speed;      // characters per frame, -1 = project default
    s16 blip;      // sound id, -1 = speaker default
};
using SayHook = void (*)(const SayRequest& request);
using BusyHook = bool (*)();                               // true while the dialogue box is open
using ChoiceHook = void (*)(const s16* options, u8 count);  // string ids; -1 = hidden option
using ChoicePoll = s8 (*)();                               // -1 while open, else the picked index
void setDialogueHooks(SayHook say, BusyHook busy, ChoiceHook choice, ChoicePoll poll);

using ShowTextHook = void (*)(u16 text);
void setShowTextHook(ShowTextHook hook);

using GiveItemHook = void (*)(u16 table, s32 item, s32 count);
void setGiveItemHook(GiveItemHook hook);

using LockHook = void (*)(bool locked);
void setLockHook(LockHook hook);
bool locked();

// Movement paths for Move (generated from the project): points are pixel pairs, relative to where the
// actor stands when Move starts unless `relative` is false; speed in pixels per frame.
struct MovePath {
    const s16* points;  // x0, y0, x1, y1, ...
    u8 count;           // number of points
    bool relative;
    fixed speed;
};
void setPaths(const MovePath* paths, u16 count);

// Name strings used by FindActor and Log (generated names table).
void setNames(const char* const* names, u16 count);

// Without dialogue hooks Choice returns this option at once (tests, auto-play).
void setAutoChoice(s8 index);

// ---- C# API helpers called by translated code ---------------------------------------------------------
Actor* player();
void showText(u16 text);
void fadeIn(fixed seconds);
void fadeOut(fixed seconds);
void giveItem(u16 table, s32 item, s32 count);

// [ScriptNative(waits: true)]: the generated wrapper calls waitForResume(ctx); EventScript.Continue(result)
// calls resume(result), which ends the wait of every waiting context and returns `result` to them.
void waitForResume(Context& ctx);
void resume(s32 result);

// Clears locks, waits and statistics (scene change, tests).
void reset();

// Counters for tests and the Debugger: lines said, choices shown, sounds played.
struct Stats {
    u16 says, choices, sounds, moves;
    s32 lastText, lastSound, lastItem;
};
const Stats& stats();

}  // namespace agb::script::natives
