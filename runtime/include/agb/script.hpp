// Event Script VM (blueprint 07): runs the bytecode compiled from `[Cutscene] async` C# methods and the
// Dialogue Editor (map scripts are C++ functions, SceneDef::onLoad / onEnter / onExit). Several contexts run
// side by side by priority; instructions can yield across frames; natives (Say, Move, Choice, ...) can make a
// context wait until they finish.
#pragma once
#include "agb/types.hpp"

namespace agb::script {

enum class Op : u8 {
#define AGB_OP(name, operands, note) name,
#include "agb/script_ops.def"
#undef AGB_OP
    Count
};

enum class Priority : u8 { Cutscene = 0, Trigger = 1, Immediate = 2 };

struct Script {
    const char* name;
    const u8* code;
    u16 size;
    u8 locals;  // local variable count
    u8 args;    // arguments taken from the caller's stack
};

struct Context;
// A native returns its result. To wait, it sets ctx.wait to a predicate checked once per frame.
using NativeFn = s32 (*)(Context& ctx, const s32* args, int argc);

struct Native {
    const char* name;
    NativeFn fn;
    bool waits;
};

constexpr int kMaxContexts = 8;
constexpr int kStackSize = 24;
constexpr int kMaxLocals = 16;
constexpr int kCallDepth = 8;

struct Frame {
    u16 script;
    u16 pc;
    u8 localBase;
    u8 stackBase;
};

struct Context {
    bool active = false;
    Priority priority = Priority::Trigger;
    u16 id = 0;
    Frame frames[kCallDepth];
    u8 depth = 0;
    s32 stack[kStackSize];
    u8 sp = 0;
    s32 locals[kMaxLocals * 2];
    u8 localTop = 0;
    u16 waitFrames = 0;
    bool (*wait)(Context&) = nullptr;  // native wait predicate; true = done
    s32 waitResult = 0;                // pushed when the wait ends (Choice index)
    bool paused = false;               // debugger
    s32 userData[4] = {};              // natives keep per-call state here
    u16 lastLine = 0;
};

// Registration (generated per game): scripts and natives tables.
void setTables(const Script* scripts, u16 scriptCount, const Native* natives, u16 nativeCount);
// Typed state store (blueprint 07). Ids: 0x0000-0x3FFF persistent (save), 0x4000 per-map,
// 0x8000 session (RAM only). Flags and vars use separate id spaces.
void setPersistentStore(u8* flagBits, u16 flagCount, s32* vars, u16 varCount);
// The saved part of the store lives in the generated GameState (member scriptStore), so Save.Write keeps it.
constexpr u16 kPersistentFlags = 256, kPersistentVars = 32;
struct PersistentStore {
    u8 flags[kPersistentFlags / 8];
    s32 vars[kPersistentVars];
};
void clearMapStore();   // called on scene change
bool flag(u16 id);
void setFlag(u16 id, bool value);
s32 var(u16 id);
void setVar(u16 id, s32 value);

// Starts a script; returns the context id or -1 when all contexts are busy.
int start(u16 script, Priority priority = Priority::Trigger, const s32* args = nullptr, int argc = 0);
// Runs a script to completion right now (map OnLoad); waits are not allowed and end the run.
s32 runImmediate(u16 script, const s32* args = nullptr, int argc = 0);
void stop(int context);
bool running(int context);
bool anyRunning(Priority atLeast = Priority::Immediate);
bool cutsceneRunning();  // LockAll: player input is ignored while true
void run();              // one frame for every context, highest priority first
void resetContexts();
Context* context(int id);
u16 scriptIdByName(const char* name);

// ---- debugger (Debugger Breakpoints; read and written by the editor through the debug channel) -----
struct Breakpoint {
    u16 script;
    u16 line;  // source line (Line markers)
};
struct DebugState {
    Breakpoint breakpoints[16];
    u8 breakpointCount;
    volatile u8 pausedContext;  // 0xFF = none
    volatile u8 resume;         // host sets 1 to continue, 2 to step one line
    u16 pausedScript, pausedLine;
};
DebugState& debugState();

}  // namespace agb::script
