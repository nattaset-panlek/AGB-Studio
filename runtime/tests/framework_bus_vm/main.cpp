// Command bus (headless rules, AI, replay determinism) and the Event Script VM (contexts, priorities,
// waits, natives, calls, flags / vars stores, immediate scripts, breakpoints).
#include "../test.hpp"
#include "agb/bus.hpp"
#include "agb/rng.hpp"
#include "agb/script.hpp"

using namespace agb;

namespace {
// ---- a tiny turn-based battle: rules only talk through the bus ------------------------------------
enum CmdType : u16 { ShowAttack = 1, ShowDamage, ShowFaint };

struct Battle {
    s16 hp[2] = {30, 30};
    int turn = 0;
    int winner = -1;
};

int aiStrongest(int side, const s16* options, int count) {
    AGB_UNUSED(side);
    int best = 0;
    for (int i = 1; i < count; ++i)
        if (options[i] > options[best]) best = i;
    return best;
}

int aiRandom(int side, const s16* options, int count) {
    AGB_UNUSED(side);
    AGB_UNUSED(options);
    return static_cast<int>(rng::below(static_cast<u32>(count), rng::Stream::AI));
}

// One rules step; returns false while waiting for a choice or for the presentation.
bool rulesStep(Battle& b) {
    if (b.winner >= 0 || !bus::idle()) return false;  // sync point: wait for the presentation
    const int side = b.turn & 1;
    const s16 moves[3] = {4, 7, 2};  // attack powers
    const int pick = bus::choose(side, moves, 3);
    if (pick < 0) return false;
    const int damage = moves[pick] + static_cast<int>(rng::below(4));
    const int target = side ^ 1;
    b.hp[target] = static_cast<s16>(b.hp[target] - damage);
    bus::post({ShowAttack, static_cast<s16>(side), static_cast<s16>(pick), 0, 0});
    bus::post({ShowDamage, static_cast<s16>(target), static_cast<s16>(damage), 0, 0});
    if (b.hp[target] <= 0) {
        b.winner = side;
        bus::post({ShowFaint, static_cast<s16>(target), 0, 0, 0});
    }
    ++b.turn;
    return true;
}

Battle runBattle(bool presentation, u32 seed) {
    rng::seed(seed);
    Battle b;
    int frames = 0, playing = 0;
    while (b.winner < 0 && frames < 5000) {
        rulesStep(b);
        if (presentation) {
            // Presentation takes 3 frames per command.
            if (bus::front() && ++playing >= 3) {
                bus::complete();
                playing = 0;
            }
        } else {
            bus::completeAll();
        }
        ++frames;
    }
    return b;
}
}  // namespace

AGB_TEST(bus_rules_are_independent_of_presentation) {
    bus::reset();
    bus::setSource(0, bus::Source::AI, aiStrongest);
    bus::setSource(1, bus::Source::AI, aiRandom);
    const Battle a = runBattle(true, 99);
    int len = 0;
    const u8* rec = bus::record(&len);
    u8 saved[256];
    for (int i = 0; i < len; ++i) saved[i] = rec[i];
    bus::reset();
    bus::setSource(0, bus::Source::AI, aiStrongest);
    bus::setSource(1, bus::Source::AI, aiRandom);
    const Battle b = runBattle(false, 99);  // headless: same result without drawing
    CHECK(a.winner >= 0);
    CHECK_EQ(a.winner, b.winner);
    CHECK_EQ(a.turn, b.turn);
    CHECK_EQ(a.hp[0], b.hp[0]);
    // Replay of the recorded choices gives the same battle.
    bus::reset();
    bus::setSource(0, bus::Source::Replay);
    bus::setSource(1, bus::Source::Replay);
    bus::setReplay(saved, len);
    const Battle c = runBattle(false, 99);
    CHECK_EQ(c.winner, a.winner);
    CHECK_EQ(c.hp[1], a.hp[1]);
    // A player side waits for the UI.
    bus::reset();
    const s16 opts[2] = {1, 2};
    CHECK_EQ(bus::choose(0, opts, 2), -1);
    bus::submitChoice(0, 1);
    CHECK_EQ(bus::choose(0, opts, 2), 1);
}

// ---- VM --------------------------------------------------------------------------------------------
namespace {
using script::Op;
constexpr u8 op(Op o) { return static_cast<u8>(o); }

int g_said = 0;
bool sayDone(script::Context& c) { return --c.userData[0] <= 0; }
s32 nSay(script::Context& c, const s32* args, int argc) {
    AGB_UNUSED(argc);
    g_said += args[0];
    c.userData[0] = 3;  // the dialogue box takes 3 frames
    c.wait = sayDone;
    return 0;
}
s32 nAdd3(script::Context&, const s32* args, int) { return args[0] + args[1] + args[2]; }
bool choiceDone(script::Context& c) { return --c.userData[0] <= 0; }
s32 nChoice(script::Context& c, const s32*, int) {
    c.userData[0] = 2;
    c.wait = choiceDone;
    return 1;  // pushed when the wait ends: "Ask again"
}

// script 0: main cutscene
//   Say(5); x = Add3(1,2,3); if (Choice() == 1) flag[3] = true; var[1] = x * 2; wait 4 frames; call 1; End
const u8 kMain[] = {
    op(Op::Line), 10, 0,
    op(Op::PushI8), 5, op(Op::Native), 0, 0, 1, op(Op::Pop),
    op(Op::PushI8), 1, op(Op::PushI8), 2, op(Op::PushI8), 3, op(Op::Native), 1, 0, 3, op(Op::StoreLocal), 0,
    op(Op::Line), 12, 0,
    op(Op::Native), 2, 0, 0, op(Op::PushI8), 1, op(Op::Eq), op(Op::JumpIfFalse), 4, 0,
    op(Op::PushI8), 1, op(Op::SetFlag), 3, 0,
    op(Op::LoadLocal), 0, op(Op::PushI8), 2, op(Op::Mul), op(Op::StoreVar), 1, 0,
    op(Op::PushI8), 4, op(Op::WaitFrames),
    op(Op::PushI16), 0x34, 0x12, op(Op::Call), 1, 0, op(Op::StoreVar), 2, 0x80,  // session var 2 = result
    op(Op::End),
};
// script 1: returns arg + 1 (1 arg, 1 local)
const u8 kInc[] = {op(Op::LoadLocal), 0, op(Op::PushI8), 1, op(Op::Add), op(Op::Ret)};
// script 2: immediate: var[0] = 7 * 6 (fixed multiply 2.5 * 4 = 10 into var 3)
const u8 kImm[] = {op(Op::PushI8), 7, op(Op::PushI8), 6, op(Op::Mul), op(Op::StoreVar), 0, 0,
                   op(Op::PushI32), 0x80, 0x02, 0, 0, op(Op::PushI32), 0, 4, 0, 0, op(Op::FMul), op(Op::StoreVar), 3, 0, op(Op::End)};
// script 3: low-priority counter loop: var[4] += 1 every frame forever
const u8 kLoop[] = {op(Op::LoadVar), 4, 0, op(Op::PushI8), 1, op(Op::Add), op(Op::StoreVar), 4, 0, op(Op::Yield), op(Op::Jump), 0xF3, 0xFF};  // back to 0 (-13)

const script::Script kScripts[] = {
    {"main", kMain, sizeof kMain, 2, 0},
    {"inc", kInc, sizeof kInc, 1, 1},
    {"imm", kImm, sizeof kImm, 0, 0},
    {"loop", kLoop, sizeof kLoop, 0, 0},
};
const script::Native kNatives[] = {{"Say", nSay, true}, {"Add3", nAdd3, false}, {"Choice", nChoice, true}};
u8 g_flags[16];
s32 g_vars[8];
}  // namespace

AGB_TEST(vm_runs_cutscene_with_waits_calls_and_stores) {
    script::setTables(kScripts, 4, kNatives, 3);
    script::setPersistentStore(g_flags, 128, g_vars, 8);
    script::resetContexts();
    const int ctx = script::start(0, script::Priority::Cutscene);
    CHECK(ctx >= 0);
    CHECK(script::cutsceneRunning());
    const int loop = script::start(3, script::Priority::Trigger);
    int frames = 0;
    while (script::running(ctx) && frames < 50) {
        script::run();
        ++frames;
    }
    CHECK(!script::running(ctx));
    CHECK_EQ(g_said, 5);
    CHECK(script::flag(3));
    CHECK(!script::flag(4));
    CHECK_EQ(g_vars[1], 12);           // (1+2+3) * 2
    CHECK_EQ(script::var(0x8002), 0x1235);  // session var from the called script
    CHECK(frames >= 3 + 2 + 4);        // Say 3 frames + Choice 2 + WaitFrames 4
    CHECK(script::running(loop));
    CHECK(g_vars[4] >= frames - 1);    // ran alongside the cutscene every frame
    script::stop(loop);
    CHECK_EQ(script::runImmediate(2), 0);
    CHECK_EQ(g_vars[0], 42);
    CHECK_EQ(g_vars[3], 10 * 256);     // 2.5 * 4 in fixed
    script::setVar(0x4001, 9);         // per-map var
    CHECK_EQ(script::var(0x4001), 9);
    script::clearMapStore();
    CHECK_EQ(script::var(0x4001), 0);
    CHECK_EQ(script::scriptIdByName("loop"), 3);
}

AGB_TEST(vm_breakpoint_pauses_and_resumes) {
    script::resetContexts();
    g_said = 0;
    auto& d = script::debugState();
    d.breakpoints[0] = {0, 12};
    d.breakpointCount = 1;
    const int ctx = script::start(0, script::Priority::Cutscene);
    for (int i = 0; i < 10; ++i) script::run();
    CHECK_EQ(d.pausedContext, ctx);
    CHECK_EQ(d.pausedLine, 12);
    CHECK(script::running(ctx));
    for (int i = 0; i < 5; ++i) script::run();
    CHECK_EQ(d.pausedLine, 12);  // still paused
    d.breakpointCount = 0;
    d.resume = 1;
    for (int i = 0; i < 30; ++i) script::run();
    CHECK(!script::running(ctx));
    CHECK_EQ(d.pausedContext, 0xFF);
}

// ---- WaitUntil, single step and the locals view -------------------------------------------------------------
namespace {
// script 0: await WaitUntil(() => Flags.open); var[0x8006] = 1
//   L0: GetFlag 0x8005; WaitUntil -> L0; PushI8 1; StoreVar 0x8006; End
const u8 kWaitFlag[] = {op(Op::GetFlag), 0x05, 0x80, op(Op::WaitUntil), 0xFA, 0xFF, op(Op::PushI8), 1, op(Op::StoreVar), 0x06, 0x80, op(Op::End)};
// script 1: await WaitUntil(() => count >= 3) where another script counts; var[0x8009] = count
//   L0: LoadVar 0x8007; PushI8 3; Ge; WaitUntil -> L0; LoadVar 0x8007; StoreVar 0x8009; End
const u8 kWaitCount[] = {op(Op::LoadVar), 0x07, 0x80, op(Op::PushI8), 3, op(Op::Ge), op(Op::WaitUntil), 0xF7, 0xFF,
                         op(Op::LoadVar), 0x07, 0x80, op(Op::StoreVar), 0x09, 0x80, op(Op::End)};
// script 2: counts var[0x8007] up once per frame
const u8 kCount[] = {op(Op::LoadVar), 0x07, 0x80, op(Op::PushI8), 1, op(Op::Add), op(Op::StoreVar), 0x07, 0x80, op(Op::Yield), op(Op::Jump), 0xF3, 0xFF};
// script 3: line 1: a = 7; line 2: b = a + 5; line 3: var[0x8008] = b (2 locals)
const u8 kLines[] = {op(Op::Line), 1, 0, op(Op::PushI8), 7, op(Op::StoreLocal), 0,
                     op(Op::Line), 2, 0, op(Op::LoadLocal), 0, op(Op::PushI8), 5, op(Op::Add), op(Op::StoreLocal), 1,
                     op(Op::Line), 3, 0, op(Op::LoadLocal), 1, op(Op::StoreVar), 0x08, 0x80, op(Op::End)};
// script 4: a cutscene that passes a line marker every frame: L0: Line 9; Yield; Jump -> L0
const u8 kTicker[] = {op(Op::Line), 9, 0, op(Op::Yield), op(Op::Jump), 0xF9, 0xFF};
const script::Script kScripts2[] = {
    {"waitFlag", kWaitFlag, sizeof kWaitFlag, 0, 0}, {"waitCount", kWaitCount, sizeof kWaitCount, 0, 0},
    {"count", kCount, sizeof kCount, 0, 0},          {"lines", kLines, sizeof kLines, 2, 0},
    {"ticker", kTicker, sizeof kTicker, 0, 0},
};

s32 localOf(int ctx, int slot) {
    const script::Context* c = script::context(ctx);
    return c ? c->locals[c->frames[c->depth - 1].localBase + slot] : -999;
}
}  // namespace

AGB_TEST(vm_wait_until_checks_its_condition_every_frame) {
    script::setTables(kScripts2, 5, kNatives, 3);
    script::resetContexts();
    script::setFlag(0x8005, false);
    script::setVar(0x8006, 0);
    const int ctx = script::start(0, script::Priority::Cutscene);
    for (int i = 0; i < 5; ++i) script::run();
    CHECK(script::running(ctx));
    CHECK_EQ(script::var(0x8006), 0);
    script::setFlag(0x8005, true);
    script::run();
    CHECK(!script::running(ctx));
    CHECK_EQ(script::var(0x8006), 1);

    // The condition is evaluated again each frame: here another script counts while this one waits.
    script::setVar(0x8007, 0);
    const int waiter = script::start(1, script::Priority::Cutscene);
    const int counter = script::start(2, script::Priority::Trigger);
    int frames = 0;
    while (script::running(waiter) && frames < 20) {
        script::run();
        ++frames;
    }
    CHECK(!script::running(waiter));
    CHECK_EQ(script::var(0x8009), 3);
    CHECK(frames >= 3);
    script::stop(counter);

    // An immediate script cannot wait: WaitUntil logs a warning and goes on.
    script::setFlag(0x8005, false);
    script::setVar(0x8006, 0);
    script::runImmediate(0);
    CHECK_EQ(script::var(0x8006), 1);
}

AGB_TEST(vm_single_step_and_locals_view) {
    script::setTables(kScripts2, 5, kNatives, 3);
    script::resetContexts();
    script::setVar(0x8008, 0);
    auto& d = script::debugState();
    d.resume = 0;
    d.breakpoints[0] = {3, 2};
    d.breakpointCount = 1;
    // A cutscene that runs first every frame and passes line markers; stepping must not stop it.
    const int ticker = script::start(4, script::Priority::Cutscene);
    const int ctx = script::start(3, script::Priority::Trigger);
    for (int i = 0; i < 3; ++i) script::run();
    CHECK_EQ(d.pausedContext, ctx);
    CHECK_EQ(d.pausedScript, 3);
    CHECK_EQ(d.pausedLine, 2);
    CHECK_EQ(localOf(ctx, 0), 7);  // a = 7 ran; line 2 did not
    CHECK_EQ(localOf(ctx, 1), 0);
    // Step one line: the paused context runs line 2 and stops at line 3.
    d.breakpointCount = 0;
    d.resume = 2;
    script::run();
    CHECK_EQ(d.pausedContext, ctx);
    CHECK_EQ(d.pausedLine, 3);
    CHECK_EQ(d.resume, 0);
    CHECK_EQ(localOf(ctx, 1), 12);
    CHECK_EQ(script::var(0x8008), 0);
    CHECK(script::running(ticker));
    CHECK(!script::context(ticker)->paused);
    // Step past the last line: the script ends and nothing stays paused.
    d.resume = 2;
    script::run();
    CHECK(!script::running(ctx));
    CHECK_EQ(script::var(0x8008), 12);
    CHECK_EQ(d.pausedContext, 0xFF);
    CHECK_EQ(d.resume, 0);
    script::run();
    CHECK(!script::context(ticker)->paused);
    script::stop(ticker);
}

int main() {
    system::init();
    test::runAll();
}
