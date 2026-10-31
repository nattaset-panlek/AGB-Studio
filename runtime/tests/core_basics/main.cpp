// Core services: formatter, fixed-point, RNG, tasks, pools, arenas, input, frame loop, profiler.
#include <tonc.h>

#include "../test.hpp"
#include "agb/bios.hpp"
#include "agb/commit.hpp"
#include "agb/fixed.hpp"
#include "agb/format.hpp"
#include "agb/input.hpp"
#include "agb/memory.hpp"
#include "agb/rng.hpp"
#include "agb/task.hpp"

using namespace agb;

namespace {
bool streq(const char* a, const char* b) {
    while (*a && *a == *b) ++a, ++b;
    return *a == *b;
}
}  // namespace

AGB_TEST(format_numbers_and_strings) {
    char buf[64];
    format(buf, sizeof buf, "%d|%5d|%-4d|%05d|%u|%x|%X|%s|%c|%%|%lu", -42, 7, 3, -12, 4000000000u, 0xbeef, 0xbeef, "ok", 'Z', 5ul);
    CHECK(streq(buf, "-42|    7|3   |-0012|4000000000|beef|BEEF|ok|Z|%|5"));
    CHECK_EQ(format(buf, 4, "%s", "truncate"), 8);
    CHECK(streq(buf, "tru"));
    format(buf, sizeof buf, "%d", -2147483647 - 1);
    CHECK(streq(buf, "-2147483648"));
}

AGB_TEST(fixed_point_math) {
    constexpr fixed a = fixed::lit(1.5), b = fixed::lit(3.5);
    CHECK_EQ(a.raw, 384);
    CHECK_EQ((a + b).raw, 5 * 256);
    CHECK_EQ((a * b).raw, fixed::lit(5.25).raw);
    CHECK_EQ((b / a).raw, 597);  // 2.33203125
    CHECK_EQ(fixed::lit(-1.5).floor(), -2);
    CHECK_EQ(fixed::lit(-1.5).toInt(), -1);
    CHECK_EQ(fixed::lit(2.5).round(), 3);
    CHECK(a < b);
    CHECK_EQ((fixed(1) / fixed(0)).raw, 0x7FFFFFFF);
    CHECK_EQ(math::sqrt(fixed(16)).raw, fixed(4).raw);
    CHECK_EQ(math::sqrt(fixed(2)).raw, 362);  // 1.414
    CHECK_EQ(math::sin(0).raw, 0);
    CHECK_EQ(math::sin(64).raw, 256);
    CHECK_EQ(math::cos(128).raw, -256);
    CHECK_EQ(math::sin(192).raw, -256);
}

AGB_TEST(rng_is_deterministic_per_stream) {
    rng::seed(1234);
    u32 first[4];
    for (auto& v : first) v = rng::next(rng::Stream::Gameplay);
    rng::seed(1234);
    for (u32 v : first) CHECK_EQ(rng::next(rng::Stream::Gameplay), v);
    // Using the effect stream must not change the gameplay sequence.
    rng::seed(1234);
    for (int i = 0; i < 50; ++i) rng::next(rng::Stream::Effect);
    CHECK_EQ(rng::next(rng::Stream::Gameplay), first[0]);
    for (int i = 0; i < 200; ++i) {
        const int r = rng::range(-3, 3);
        CHECK(r >= -3 && r <= 3);
    }
    // 16 values of every stream for seed 1234, then 16 bounded values: RuntimeRomTests.RngMatchesHostReference
    // compares these lines with its host copy of the generator.
    for (int s = 0; s < static_cast<int>(rng::Stream::Count); ++s) {
        rng::seed(1234);
        u32 v[16];
        for (auto& x : v) x = rng::next(static_cast<rng::Stream>(s));
        debug::logf("rng %d a %08lx %08lx %08lx %08lx %08lx %08lx %08lx %08lx", s, v[0], v[1], v[2], v[3], v[4], v[5], v[6], v[7]);
        debug::logf("rng %d b %08lx %08lx %08lx %08lx %08lx %08lx %08lx %08lx", s, v[8], v[9], v[10], v[11], v[12], v[13], v[14], v[15]);
    }
    rng::seed(1234);
    int b[16];
    for (auto& x : b) x = rng::range(-3, 3);
    debug::logf("rng range %d %d %d %d %d %d %d %d %d %d %d %d %d %d %d %d", b[0], b[1], b[2], b[3], b[4], b[5], b[6], b[7], b[8],
                b[9], b[10], b[11], b[12], b[13], b[14], b[15]);
    system::waitVBlank();  // the host reads the log ring once per frame
}

namespace {
// Operand pairs (raw 24.8 values) for the reference table: signs, fractions, small and large magnitudes.
// RuntimeRomTests.FixedPointMatchesHostReference recomputes every result from exact decimal arithmetic.
constexpr s32 kFixedPairs[][2] = {
    {256, 256},       {384, 896},        {-384, 896},      {-384, -896},     {1, 1},           {1, -1},
    {255, 257},       {-255, 257},       {64, 64},         {3, 85},          {-3, 85},         {100000, 300},
    {-100000, 300},   {65536, 65536},    {-65536, 65536},  {1000000, 17},    {7, 100000},      {-7, 100000},
    {12345, 678},     {-12345, -678},    {2560, 3},        {-2560, 3},       {80, 81},         {32767, 32767},
    {-32768, 32767},  {500000, -999},    {8388607, 256},   {-8388608, 256},  {256, 8388607},   {512, -1},
    {77, 1024},       {-77, 1024},
};
constexpr s32 kSqrtInputs[] = {0, 1, 2, 3, 4, 64, 255, 256, 257, 512, 1000, 4096, 65536, 100000, 1000000, 8388607};
}  // namespace

AGB_TEST(fixed_point_reference_table) {
    // Each line: a b a*b a/b (raw values). The host compares them with floor(a * b / 256) and
    // truncate(a * 256 / b) computed exactly.
    int n = 0;
    for (const auto& p : kFixedPairs) {
        const fixed a = fixed::fromRaw(p[0]), b = fixed::fromRaw(p[1]);
        debug::logf("fx %ld %ld %ld %ld", p[0], p[1], (a * b).raw, (a / b).raw);
        if (++n % 16 == 0) system::waitVBlank();
    }
    // Quarter-wave sine table over all 256 angles, 16 per line, and integer square roots.
    for (int line = 0; line < 16; ++line) {
        s32 v[16];
        for (int i = 0; i < 16; ++i) v[i] = math::sin(static_cast<u8>(line * 16 + i)).raw;
        debug::logf("sin %d %ld %ld %ld %ld %ld %ld %ld %ld %ld %ld %ld %ld %ld %ld %ld %ld", line * 16, v[0], v[1], v[2], v[3], v[4], v[5], v[6],
                    v[7], v[8], v[9], v[10], v[11], v[12], v[13], v[14], v[15]);
        if (line % 8 == 7) system::waitVBlank();
    }
    for (s32 in : kSqrtInputs) debug::logf("sqrt %ld %ld", in, math::sqrt(fixed::fromRaw(in)).raw);
    for (int a = 0; a < 256; ++a) CHECK_EQ(math::cos(static_cast<u8>(a)).raw, math::sin(static_cast<u8>(a + 64)).raw);
    // fixed2 is component-wise fixed arithmetic, so the same table covers it.
    for (int i = 0; i + 1 < static_cast<int>(sizeof kFixedPairs / sizeof kFixedPairs[0]); ++i) {
        const fixed2 v{fixed::fromRaw(kFixedPairs[i][0]), fixed::fromRaw(kFixedPairs[i][1])};
        const fixed2 w{fixed::fromRaw(kFixedPairs[i + 1][0]), fixed::fromRaw(kFixedPairs[i + 1][1])};
        const fixed s = fixed::fromRaw(kFixedPairs[i + 1][0]);
        CHECK((v + w) == (fixed2{v.x + w.x, v.y + w.y}));
        CHECK((v - w) == (fixed2{v.x - w.x, v.y - w.y}));
        CHECK((v * s) == (fixed2{v.x * s, v.y * s}));
    }
    system::waitVBlank();
}

namespace {
volatile s32 g_sinkRaw;
volatile s32 g_opA = 0x12345, g_opB = 0x678;

// CPU cycles for `count` runs of `op` minus the same loop with an empty body (timers 2+3 count every cycle).
template <typename F>
u32 cyclesPerOp(F op, int count) {
    system::waitVBlank();  // start of a frame: no interrupt lands inside the measurement
    u32 t0 = profile::cyclesNow();
    for (int i = 0; i < count; ++i) g_sinkRaw = g_opA;
    const u32 empty = profile::cyclesNow() - t0;
    t0 = profile::cyclesNow();
    for (int i = 0; i < count; ++i) g_sinkRaw = op();
    const u32 full = profile::cyclesNow() - t0;
    return full > empty ? (full - empty) / static_cast<u32>(count) : 0;
}
}  // namespace

AGB_TEST(fixed_point_cycle_budget) {
    // BUD: cycles per multiply / divide as the game code runs them (Thumb in ROM, wait states 3/1 + prefetch).
    const u32 mul = cyclesPerOp([] { return (fixed::fromRaw(g_opA) * fixed::fromRaw(g_opB)).raw; }, 64);
    const u32 div = cyclesPerOp([] { return (fixed::fromRaw(g_opA) / fixed::fromRaw(g_opB)).raw; }, 64);
    const u32 mulInt = cyclesPerOp([] { return (fixed::fromRaw(g_opA) * static_cast<int>(g_opB)).raw; }, 64);
    const u32 biosDiv = cyclesPerOp([] { return bios::div(g_opA, g_opB); }, 64);
    const u32 sqrtC = cyclesPerOp([] { return math::sqrt(fixed::fromRaw(g_opA)).raw; }, 16);
    debug::logf("fixed cycles: mul %lu, div %lu, mul by int %lu, bios div %lu, sqrt %lu", mul, div, mulInt, biosDiv, sqrtC);
    // A frame has 280896 cycles; these bounds keep a few thousand fixed operations per frame affordable.
    CHECK(mul > 0 && mul < 200);
    CHECK(div > 0 && div < 400);
    CHECK(mulInt < 60);
    CHECK(biosDiv > 0 && biosDiv < 400);
    CHECK(sqrtC < 2000);
}

namespace {
// BIOS LZ77 (type 0x10) stream for 32 bytes: 8 literals "ABCDEFGH", then copies of 18 and 6 bytes from 8 back.
alignas(4) const u8 kLz77[] = {0x10, 0x20, 0x00, 0x00, 0x00, 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 0xC0, 0xF0, 0x07, 0x30, 0x07, 0x00, 0x00};
alignas(4) u8 g_lzOut[40];
alignas(4) u32 g_copySrc[16];
alignas(4) u32 g_copyDst[20];
}  // namespace

AGB_TEST(bios_wrappers) {
    // LZ77 to WRAM and to VRAM.
    CHECK_EQ(bios::lz77Size(kLz77), 32u);
    for (auto& b : g_lzOut) b = 0xEE;
    bios::lz77ToWram(kLz77, g_lzOut);
    int wrong = 0;
    for (int i = 0; i < 32; ++i) wrong += g_lzOut[i] != 'A' + i % 8;
    CHECK_EQ(wrong, 0);
    CHECK_EQ(g_lzOut[32], 0xEE);  // nothing written past the decompressed size
    u8* vram = reinterpret_cast<u8*>(commit::bgTiles(3)) + 0x3000;
    bios::lz77ToVram(kLz77, vram);
    wrong = 0;
    for (int i = 0; i < 32; ++i) wrong += vram[i] != 'A' + i % 8;
    CHECK_EQ(wrong, 0);
    // Div / DivMod truncate toward zero like C.
    const s32 nums[] = {7, -7, 100, -100, 2147483647, -2147483647, 0, 1};
    const s32 dens[] = {2, 2, 7, -7, 3, 10, 5, -1};
    for (int i = 0; i < 8; ++i) {
        CHECK_EQ(bios::div(nums[i], dens[i]), nums[i] / dens[i]);
        CHECK_EQ(bios::mod(nums[i], dens[i]), nums[i] % dens[i]);
    }
    // CpuFastSet copies whole 32-byte blocks and nothing more.
    for (int i = 0; i < 16; ++i) g_copySrc[i] = 0xC0DE0000u + static_cast<u32>(i);
    for (auto& w : g_copyDst) w = 0xDEADBEEFu;
    bios::fastCopy(g_copyDst, g_copySrc, 64);
    for (int i = 0; i < 16; ++i) CHECK_EQ(g_copyDst[i], 0xC0DE0000u + static_cast<u32>(i));
    CHECK_EQ(g_copyDst[16], 0xDEADBEEFu);
    // Halt sleeps until the next interrupt: with only VBlank enabled that is the start of VBlank (line 160).
    system::waitVBlank();
    while (REG_VCOUNT >= 160 || REG_VCOUNT < 40) {}
    const u32 line = REG_VCOUNT;
    const u32 t0 = profile::cyclesNow();
    bios::halt();
    const u32 slept = profile::cyclesNow() - t0;
    const u32 after = REG_VCOUNT;
    debug::logf("halt from line %lu woke at line %lu after %lu cycles", line, after, slept);
    CHECK(after >= 160 && after <= 161);
    CHECK(slept + 2 * 1232 >= (160 - line) * 1232);
    system::waitVBlank();  // the halt let one VBlank pass; resynchronise the frame loop
}

namespace {
int g_order[4];
int g_orderLen = 0;
void taskA(task::Task& t) { g_order[g_orderLen++] = 1; t.active = false; }
void taskB(task::Task& t) { g_order[g_orderLen++] = 2; t.active = false; }
void taskSteps(task::Task& t) {
    switch (t.state) {
        case 0: t.data[0] = 10; t.state = 1; t.wait = 2; break;
        case 1: t.data[0] += 5; t.state = 2; break;
        default: t.active = false;
    }
}
}  // namespace

AGB_TEST(tasks_run_by_priority_and_step) {
    task::destroyAll();
    g_orderLen = 0;
    task::create(taskA, 200);
    task::create(taskB, 10);
    task::runAll();
    CHECK_EQ(g_orderLen, 2);
    CHECK_EQ(g_order[0], 2);
    CHECK_EQ(g_order[1], 1);
    const auto id = task::create(taskSteps);
    task::runAll();                 // state 0 -> 1, wait 2
    task::runAll();                 // waiting
    task::runAll();                 // waiting
    CHECK_EQ(task::get(id)->data[0], 10);
    task::runAll();                 // state 1 -> 2
    CHECK_EQ(task::get(id)->data[0], 15);
    task::runAll();
    CHECK(!task::isActive(id));
    for (int i = 0; i < task::kMaxTasks; ++i) task::create(taskSteps);
    CHECK_EQ(task::create(taskSteps), task::kNone);  // pool full is reported, not fatal
    task::destroyAll();
}

namespace {
struct Thing {
    int value;
    explicit Thing(int v) : value(v) {}
};
}  // namespace

AGB_TEST(pool_handles_and_arena) {
    Pool<Thing, 4> pool("things");
    auto a = pool.create(1);
    auto b = pool.create(2);
    CHECK(pool.get(a)->value == 1);
    pool.destroy(a);
    CHECK_EQ(pool.count(), 1);
    auto c = pool.create(3);  // reuses slot 0 with a new generation
    CHECK_EQ(c.index, a.index);
    CHECK(c.gen != a.gen);
    pool.create(4);
    pool.create(5);
    CHECK(!pool.create(6).valid());  // full
    CHECK(pool.get(b)->value == 2);
    Arena& arena = screenArena();
    arena.reset();
    void* p1 = arena.alloc(10);
    void* p2 = arena.alloc(4);
    CHECK(p1 != nullptr && p2 != nullptr);
    CHECK_EQ(reinterpret_cast<u32>(p2) - reinterpret_cast<u32>(p1), 12u);
    CHECK(arena.alloc(200 * 1024) == nullptr);
    arena.reset();
    CHECK_EQ(arena.used(), 0u);
}

AGB_TEST(input_edges_axes_repeat_and_actions) {
    input::setRepeat(3, 2);
    input::injectState(0);
    input::update();
    input::injectState(static_cast<u16>(Key::A) | static_cast<u16>(Key::Right));
    input::update();
    CHECK(input::pressed(Key::A));
    CHECK(input::held(Key::A));
    CHECK(input::anyKey());
    CHECK_EQ(input::axisX(), 1);
    CHECK_EQ(input::axisY(), 0);
    int repeats = 0;
    for (int i = 0; i < 8; ++i) {
        input::injectState(static_cast<u16>(Key::A));
        input::update();
        if (input::repeated(Key::A)) ++repeats;
        CHECK(!input::pressed(Key::A));
    }
    CHECK_EQ(repeats, 3);  // frames 5, 7, 9 of holding
    input::injectState(0);
    input::update();
    CHECK(input::released(Key::A));
    const u16 actions[] = {static_cast<u16>(Key::A), static_cast<u16>(Key::B)};
    input::setActionMap(actions, 2);
    input::injectState(static_cast<u16>(Key::B));
    input::update();
    CHECK(input::actionPressed(1));
    CHECK(!input::actionHeld(0));
    input::remapAction(0, static_cast<u16>(Key::B));
    CHECK(input::actionHeld(0));
}

AGB_TEST(frame_loop_counts_and_profiles) {
    system::waitVBlank();  // the tests before this one ran long, so this first wait may count an overrun
    const u32 overrunsBefore = profile::stats().overruns;
    const u32 start = system::frame();
    // 600 frames (10 s): every frame is one VBlank apart and the CPU sleeps in the BIOS (VBlankIntrWait) for the
    // idle part of each frame.
    u32 minIdle = 0xFFFFFFFFu, minTotal = 0xFFFFFFFFu, maxTotal = 0, c0 = 0;
    for (int i = 0; i < 600; ++i) {
        system::waitVBlank();
        const auto& s = profile::stats();
        u32 total = 0;
        for (u32 c : s.thisFrame) total += c;
        if (i == 0) {  // the first frame includes the time before the loop; time the next 599 whole frames
            c0 = profile::cyclesNow();
            continue;
        }
        const u32 idle = s.thisFrame[static_cast<int>(profile::Category::Idle)];
        if (idle < minIdle) minIdle = idle;
        if (total < minTotal) minTotal = total;
        if (total > maxTotal) maxTotal = total;
    }
    const u32 elapsed = profile::cyclesNow() - c0;
    CHECK_EQ(system::frame() - start, 600u);
    CHECK_EQ(debug::channel().frame, system::frame());
    CHECK_EQ(profile::stats().overruns, overrunsBefore);
    // 599 frames of 280896 cycles, measured between two VBlank wake-ups (the commit before each wake-up varies by a few hundred cycles).
    CHECK(elapsed + 1232 >= 599u * profile::kCyclesPerFrame && elapsed <= 599u * profile::kCyclesPerFrame + 1232);
    // Refresh rate from the measured cycles (16.78 MHz clock): 59.73 Hz.
    const u32 hz1000 = static_cast<u32>((16777216ull * 1000 * 599 + elapsed / 2) / elapsed);
    CHECK(hz1000 >= 59720 && hz1000 <= 59735);
    // A whole frame is 280896 cycles; allow timer read slack.
    CHECK(minTotal > 270000 && maxTotal < 290000);
    CHECK(minIdle > 200000);
    debug::logf("600 frames in %lu cycles; %lu.%03lu Hz; idle (CPU halted) at least %lu of %lu cycles per frame", elapsed, hz1000 / 1000,
                hz1000 % 1000, minIdle, maxTotal);
}

AGB_TEST(commit_queue_budget_defers) {
    static u16 src[2048];
    commit::setDmaBudget(4096);
    const u32 before = commit::deferredCount();
    const u32 log = debug::channel().logHead;
    CHECK(commit::queueCopy(commit::objTiles(), src, 4096));
    CHECK(!commit::queueCopy(commit::objTiles(), src, 32));  // over budget -> deferred
    CHECK_EQ(commit::deferredCount(), before + 1);
    // The Console says what happened and what to do (once per run).
    CHECK(test::loggedSince(log, "warning: too much graphics data changed this frame, so the rest is copied next frame."));
    system::waitVBlank();
    CHECK_EQ(commit::queuedBytes(), 0u);
    CHECK(commit::queueCopy(commit::objTiles(), src, 32));
    commit::setDmaBudget(commit::kDefaultDmaBudget);
    system::waitVBlank();
}

// Queued fills and tile sequences (text panels, the Mode 3 fill) keep their data in the queue: rows past the budget are
// written in the next VBlank instead of being refused, and nothing reaches VRAM before VBlank.
AGB_TEST(commit_queue_rows_past_the_budget_wait_for_the_next_vblank) {
    u16* map = commit::bgMap(30);
    commit::beginStagedLoad();
    commit::fillNow(map, 0, 2048);  // VRAM test area (no layer shows it)
    commit::endStagedLoad();
    system::waitVBlank();
    commit::setDmaBudget(256);
    const u32 before = commit::deferredCount();
    CHECK(commit::queueFill(map, 0x1234, 64, 8, 64));             // 8 rows of 64 bytes: 512 bytes
    CHECK(commit::queueSequence(map + 8 * 32, 0x5000, 4, 2, 64));  // 2 rows of 4 entries
    CHECK_EQ(commit::queuedBytes(), 512u + 16u);
    CHECK_EQ(map[0], 0);  // nothing written before VBlank
    CHECK(!commit::queueCopy(map, map, 2));  // the leftover counts against the budget: copies wait their turn
    system::waitVBlank();
    // The first four rows (256 bytes) landed; the rest waits.
    CHECK_EQ(map[3 * 32 + 31], 0x1234);
    CHECK_EQ(map[4 * 32], 0);
    CHECK_EQ(commit::queuedBytes(), 256u + 16u);
    CHECK(commit::deferredCount() > before);
    system::waitVBlank();
    CHECK_EQ(map[7 * 32 + 31], 0x1234);
    CHECK_EQ(map[8 * 32], 0);  // the sequence did not fit the second frame either
    system::waitVBlank();
    CHECK_EQ(commit::queuedBytes(), 0u);
    // Sequence: value, value + 1 ... across the rows (a text box of 4 x 2 tiles mapped in order).
    CHECK_EQ(map[8 * 32 + 0], 0x5000);
    CHECK_EQ(map[8 * 32 + 3], 0x5003);
    CHECK_EQ(map[9 * 32 + 0], 0x5004);
    CHECK_EQ(map[9 * 32 + 3], 0x5007);
    CHECK_EQ(map[9 * 32 + 4], 0);
    commit::setDmaBudget(commit::kDefaultDmaBudget);
}

// copyNow / fillNow are for staged loads: Debug builds count a call made while the picture shows, not one inside a
// staged load or in VBlank.
AGB_TEST(direct_vram_writes_outside_a_staged_load_are_counted) {
    u16* map = commit::bgMap(30);
    system::waitVBlank();
    while (REG_VCOUNT >= 160) {}  // the picture is being drawn
    CHECK_EQ(REG_DISPCNT & DCNT_BLANK, 0u);
    const u32 before = commit::directWrites();
    commit::beginStagedLoad();
    commit::fillNow(map, 0, 64);
    commit::endStagedLoad();
    CHECK_EQ(commit::directWrites(), before);
    const u32 log = debug::channel().logHead;
    commit::fillNow(map, 0, 64);
    CHECK_EQ(commit::directWrites(), before + 1);
    CHECK(test::loggedSince(log, "warning: graphics memory was written at once while the picture was showing"));
    system::waitVBlank();
}

int main() {
    system::init();
    test::runAll();
}
