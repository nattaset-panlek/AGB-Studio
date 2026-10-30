// Core services: formatter, fixed-point, RNG, tasks, pools, arenas, input, frame loop, profiler.
#include <tonc.h>

#include "../test.hpp"
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
    // Known first value for seed 1234 (shared with the C# reference test).
    rng::seed(1234);
    debug::logf("rng first %lu", rng::next());
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
    for (int i = 0; i < 10; ++i) system::waitVBlank();
    CHECK_EQ(system::frame() - start, 10u);
    const auto& s = profile::stats();
    u32 total = 0;
    for (u32 c : s.thisFrame) total += c;
    // A whole frame is 280896 cycles; allow timer read slack.
    CHECK(total > 270000 && total < 290000);
    CHECK(s.thisFrame[static_cast<int>(profile::Category::Idle)] > 200000);
    CHECK_EQ(s.overruns, overrunsBefore);
    debug::logf("idle cycles %lu of %lu", s.thisFrame[static_cast<int>(profile::Category::Idle)], total);
}

AGB_TEST(commit_queue_budget_defers) {
    static u16 src[2048];
    commit::setDmaBudget(4096);
    const u32 before = commit::deferredCount();
    CHECK(commit::queueCopy(commit::objTiles(), src, 4096));
    CHECK(!commit::queueCopy(commit::objTiles(), src, 32));  // over budget -> deferred
    CHECK_EQ(commit::deferredCount(), before + 1);
    system::waitVBlank();
    CHECK_EQ(commit::queuedBytes(), 0u);
    CHECK(commit::queueCopy(commit::objTiles(), src, 32));
    commit::setDmaBudget(commit::kDefaultDmaBudget);
    system::waitVBlank();
}

int main() {
    system::init();
    test::runAll();
}
