#include "agb/debug.hpp"
#include "agb/fixed.hpp"
#include "agb/script.hpp"

namespace agb::script {

namespace {
const Script* g_scripts = nullptr;
u16 g_scriptCount = 0;
const Native* g_natives = nullptr;
u16 g_nativeCount = 0;
AGB_EWRAM_BSS Context g_ctx[kMaxContexts];
u16 g_nextId = 1;

u8* g_pflags = nullptr;
u16 g_pflagCount = 0;
s32* g_pvars = nullptr;
u16 g_pvarCount = 0;
u32 g_mapFlags[4], g_sessionFlags[4];
s32 g_mapVars[32], g_sessionVars[32];

AGB_EWRAM_BSS_NAMED(agb_debugger) DebugState g_debug;

constexpr int kOperandBytes[] = {
#define AGB_OP(name, operands, note) operands,
#include "agb/script_ops.def"
#undef AGB_OP
};

s16 read16(const u8* p) { return static_cast<s16>(p[0] | (p[1] << 8)); }
s32 read32(const u8* p) { return static_cast<s32>(p[0] | (p[1] << 8) | (p[2] << 16) | (static_cast<u32>(p[3]) << 24)); }

void push(Context& c, s32 v) {
    if (c.sp >= kStackSize) {
        debug::log("warning: script stack overflow");
        return;
    }
    c.stack[c.sp++] = v;
}
s32 pop(Context& c) { return c.sp ? c.stack[--c.sp] : 0; }

bool enterScript(Context& c, u16 id, int argc) {
    if (id >= g_scriptCount || c.depth >= kCallDepth) {
        debug::logf("warning: script call failed (id %u, depth %u)", id, c.depth);
        return false;
    }
    const Script& s = g_scripts[id];
    if (c.localTop + s.locals > kMaxLocals * 2) {
        debug::log("warning: script locals exhausted");
        return false;
    }
    Frame& f = c.frames[c.depth++];
    f.script = id;
    f.pc = 0;
    f.localBase = c.localTop;
    for (int i = 0; i < s.locals; ++i) c.locals[c.localTop + i] = 0;
    // Arguments become the first locals.
    for (int i = argc - 1; i >= 0; --i) c.locals[c.localTop + i] = pop(c);
    c.localTop = static_cast<u8>(c.localTop + s.locals);
    f.stackBase = c.sp;
    return true;
}

bool hitBreakpoint(u16 script, u16 line) {
    for (int i = 0; i < g_debug.breakpointCount; ++i)
        if (g_debug.breakpoints[i].script == script && g_debug.breakpoints[i].line == line) return true;
    return false;
}

// Runs one context until it yields, waits or ends. Returns false when the context ended.
bool step(Context& c, int budget, bool immediate) {
    while (budget-- > 0) {
        if (c.depth == 0) return false;
        Frame& f = c.frames[c.depth - 1];
        const Script& s = g_scripts[f.script];
        if (f.pc >= s.size) {
            // Falling off the end acts like Ret with no value.
            push(c, 0);
            const s32 r = pop(c);
            c.localTop = f.localBase;
            c.sp = f.stackBase;
            --c.depth;
            if (c.depth == 0) return false;
            push(c, r);
            continue;
        }
        const u8* p = s.code + f.pc;
        const Op op = static_cast<Op>(p[0]);
        const int operands = static_cast<u8>(op) < static_cast<u8>(Op::Count) ? kOperandBytes[p[0]] : 0;
        f.pc = static_cast<u16>(f.pc + 1 + operands);
        s32* L = c.locals + f.localBase;
        switch (op) {
            case Op::End: c.depth = 0; return false;
            case Op::Nop: break;
            case Op::PushI8: push(c, static_cast<s8>(p[1])); break;
            case Op::PushI16: push(c, read16(p + 1)); break;
            case Op::PushI32: push(c, read32(p + 1)); break;
            case Op::Pop: pop(c); break;
            case Op::Dup: { const s32 v = c.sp ? c.stack[c.sp - 1] : 0; push(c, v); break; }
            case Op::LoadLocal: push(c, L[p[1]]); break;
            case Op::StoreLocal: L[p[1]] = pop(c); break;
            case Op::LoadVar: push(c, var(static_cast<u16>(read16(p + 1)))); break;
            case Op::StoreVar: setVar(static_cast<u16>(read16(p + 1)), pop(c)); break;
            case Op::GetFlag: push(c, flag(static_cast<u16>(read16(p + 1)))); break;
            case Op::SetFlag: setFlag(static_cast<u16>(read16(p + 1)), pop(c) != 0); break;
            case Op::Add: { const s32 b = pop(c); push(c, pop(c) + b); break; }
            case Op::Sub: { const s32 b = pop(c); push(c, pop(c) - b); break; }
            case Op::Mul: { const s32 b = pop(c); push(c, pop(c) * b); break; }
            case Op::Div: { const s32 b = pop(c); const s32 a = pop(c); push(c, b ? a / b : 0); break; }
            case Op::Mod: { const s32 b = pop(c); const s32 a = pop(c); push(c, b ? a % b : 0); break; }
            case Op::FMul: { const s32 b = pop(c); push(c, (fixed::fromRaw(pop(c)) * fixed::fromRaw(b)).raw); break; }
            case Op::FDiv: { const s32 b = pop(c); push(c, (fixed::fromRaw(pop(c)) / fixed::fromRaw(b)).raw); break; }
            case Op::Neg: push(c, -pop(c)); break;
            case Op::Eq: { const s32 b = pop(c); push(c, pop(c) == b); break; }
            case Op::Ne: { const s32 b = pop(c); push(c, pop(c) != b); break; }
            case Op::Lt: { const s32 b = pop(c); push(c, pop(c) < b); break; }
            case Op::Le: { const s32 b = pop(c); push(c, pop(c) <= b); break; }
            case Op::Gt: { const s32 b = pop(c); push(c, pop(c) > b); break; }
            case Op::Ge: { const s32 b = pop(c); push(c, pop(c) >= b); break; }
            case Op::And: { const s32 b = pop(c); push(c, pop(c) && b); break; }
            case Op::Or: { const s32 b = pop(c); push(c, pop(c) || b); break; }
            case Op::Not: push(c, !pop(c)); break;
            case Op::Jump: f.pc = static_cast<u16>(f.pc + read16(p + 1)); break;
            case Op::JumpIfFalse: if (!pop(c)) f.pc = static_cast<u16>(f.pc + read16(p + 1)); break;
            case Op::JumpIfTrue: if (pop(c)) f.pc = static_cast<u16>(f.pc + read16(p + 1)); break;
            case Op::Call: {
                const u16 id = static_cast<u16>(read16(p + 1));
                if (!enterScript(c, id, id < g_scriptCount ? g_scripts[id].args : 0)) return false;
                break;
            }
            case Op::Ret: {
                const s32 r = pop(c);
                c.localTop = f.localBase;
                c.sp = f.stackBase;
                --c.depth;
                if (c.depth == 0) return false;
                push(c, r);
                break;
            }
            case Op::Native: {
                const u16 id = static_cast<u16>(read16(p + 1));
                const int argc = p[3];
                s32 args[8];
                for (int i = argc - 1; i >= 0; --i) {
                    const s32 v = pop(c);
                    if (i < 8) args[i] = v;
                }
                if (id >= g_nativeCount) {
                    debug::logf("warning: unknown script native %u", id);
                    push(c, 0);
                    break;
                }
                c.wait = nullptr;
                const s32 r = g_natives[id].fn(c, args, argc);
                if (c.wait) {
                    if (immediate) {
                        debug::logf("warning: %s waits, which is not allowed in an immediate script", g_natives[id].name);
                        c.wait = nullptr;
                        push(c, r);
                        break;
                    }
                    c.waitResult = r;
                    return true;  // result is pushed when the wait ends
                }
                push(c, r);
                break;
            }
            case Op::WaitFrames: {
                const s32 n = pop(c);
                if (immediate) break;
                c.waitFrames = static_cast<u16>(n > 0 ? n : 0);
                return true;
            }
            case Op::Yield:
                if (immediate) break;
                return true;
            case Op::Line: {
                const u16 line = static_cast<u16>(read16(p + 1));
                c.lastLine = line;
                if (!immediate && (hitBreakpoint(f.script, line) || g_debug.resume == 2)) {
                    if (g_debug.resume == 2) g_debug.resume = 0;
                    c.paused = true;
                    g_debug.pausedContext = static_cast<u8>(&c - g_ctx);
                    g_debug.pausedScript = f.script;
                    g_debug.pausedLine = line;
                    debug::logf("breakpoint: %s line %u", s.name, line);
                    return true;
                }
                break;
            }
            default:
                debug::logf("warning: bad opcode %u in %s", p[0], s.name);
                c.depth = 0;
                return false;
        }
    }
    return true;  // instruction budget used up: continue next frame
}
}  // namespace

void setTables(const Script* scripts, u16 scriptCount, const Native* natives, u16 nativeCount) {
    g_scripts = scripts;
    g_scriptCount = scriptCount;
    g_natives = natives;
    g_nativeCount = nativeCount;
    g_debug.pausedContext = 0xFF;
    debug::channel().debugger = &g_debug;
}

void setPersistentStore(u8* flagBits, u16 flagCount, s32* vars, u16 varCount) {
    g_pflags = flagBits;
    g_pflagCount = flagCount;
    g_pvars = vars;
    g_pvarCount = varCount;
}

void clearMapStore() {
    for (auto& w : g_mapFlags) w = 0;
    for (auto& v : g_mapVars) v = 0;
}

bool flag(u16 id) {
    const u16 i = id & 0x3FFF;
    switch (id >> 14) {
        case 0: return g_pflags && i < g_pflagCount && (g_pflags[i >> 3] & (1 << (i & 7)));
        case 1: return i < 128 && (g_mapFlags[i >> 5] & (1u << (i & 31)));
        default: return i < 128 && (g_sessionFlags[i >> 5] & (1u << (i & 31)));
    }
}

void setFlag(u16 id, bool v) {
    const u16 i = id & 0x3FFF;
    switch (id >> 14) {
        case 0:
            if (g_pflags && i < g_pflagCount) {
                if (v) g_pflags[i >> 3] = static_cast<u8>(g_pflags[i >> 3] | (1 << (i & 7)));
                else g_pflags[i >> 3] = static_cast<u8>(g_pflags[i >> 3] & ~(1 << (i & 7)));
            }
            break;
        case 1:
            if (i < 128) v ? (g_mapFlags[i >> 5] |= 1u << (i & 31)) : (g_mapFlags[i >> 5] &= ~(1u << (i & 31)));
            break;
        default:
            if (i < 128) v ? (g_sessionFlags[i >> 5] |= 1u << (i & 31)) : (g_sessionFlags[i >> 5] &= ~(1u << (i & 31)));
    }
}

s32 var(u16 id) {
    const u16 i = id & 0x3FFF;
    switch (id >> 14) {
        case 0: return g_pvars && i < g_pvarCount ? g_pvars[i] : 0;
        case 1: return i < 32 ? g_mapVars[i] : 0;
        default: return i < 32 ? g_sessionVars[i] : 0;
    }
}

void setVar(u16 id, s32 v) {
    const u16 i = id & 0x3FFF;
    switch (id >> 14) {
        case 0: if (g_pvars && i < g_pvarCount) g_pvars[i] = v; break;
        case 1: if (i < 32) g_mapVars[i] = v; break;
        default: if (i < 32) g_sessionVars[i] = v;
    }
}

int start(u16 id, Priority priority, const s32* args, int argc) {
    if (id >= g_scriptCount) return -1;
    for (int i = 0; i < kMaxContexts; ++i) {
        Context& c = g_ctx[i];
        if (c.active) continue;
        c = Context{};
        c.active = true;
        c.priority = priority;
        c.id = g_nextId++;
        for (int k = 0; k < argc; ++k) push(c, args[k]);
        if (!enterScript(c, id, argc)) {
            c.active = false;
            return -1;
        }
        return i;
    }
    debug::warnOnce(30, "all 8 script contexts are busy; a script did not start");
    return -1;
}

s32 runImmediate(u16 id, const s32* args, int argc) {
    static Context c;
    c = Context{};
    c.active = true;
    c.priority = Priority::Immediate;
    for (int k = 0; k < argc; ++k) push(c, args[k]);
    if (!enterScript(c, id, argc)) return 0;
    // Immediate scripts run to completion inside this frame.
    while (step(c, 100000, true)) {}
    c.active = false;
    return c.sp ? c.stack[c.sp - 1] : 0;
}

void stop(int i) {
    if (i >= 0 && i < kMaxContexts) g_ctx[i].active = false;
}

bool running(int i) { return i >= 0 && i < kMaxContexts && g_ctx[i].active; }

bool anyRunning(Priority atLeast) {
    for (auto& c : g_ctx)
        if (c.active && c.priority <= atLeast) return true;
    return false;
}

bool cutsceneRunning() { return anyRunning(Priority::Cutscene); }

void run() {
    // Debugger resume / step requests from the editor.
    if (g_debug.pausedContext != 0xFF && g_debug.resume) {
        g_ctx[g_debug.pausedContext].paused = false;
        g_debug.pausedContext = 0xFF;
        if (g_debug.resume == 1) g_debug.resume = 0;
    }
    for (int p = 0; p <= static_cast<int>(Priority::Immediate); ++p)
        for (Context& c : g_ctx) {
            if (!c.active || static_cast<int>(c.priority) != p || c.paused) continue;
            if (c.waitFrames) {
                --c.waitFrames;
                continue;
            }
            if (c.wait) {
                if (!c.wait(c)) continue;
                c.wait = nullptr;
                push(c, c.waitResult);
            }
            if (!step(c, 2000, false)) c.active = false;
        }
}

void resetContexts() {
    for (auto& c : g_ctx) c.active = false;
    g_debug.pausedContext = 0xFF;
    clearMapStore();
}

Context* context(int i) { return i >= 0 && i < kMaxContexts && g_ctx[i].active ? &g_ctx[i] : nullptr; }

u16 scriptIdByName(const char* name) {
    for (u16 i = 0; i < g_scriptCount; ++i) {
        const char* a = g_scripts[i].name;
        const char* b = name;
        while (*a && *a == *b) ++a, ++b;
        if (*a == *b) return i;
    }
    return 0xFFFF;
}

DebugState& debugState() { return g_debug; }

}  // namespace agb::script
