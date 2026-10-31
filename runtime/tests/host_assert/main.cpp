// Driven by tests/AgbStudio.Tests (RuntimeRomTests.PoolAssertsCatchStaleHandlesAndDoubleFrees): a pool handle used
// after its object was destroyed, or an object freed twice, must stop a Debug build with the assert message in
// the Console. With no key held the ROM uses a stale handle; with A held it frees an object twice. A Release build
// has no asserts: the pool refuses both quietly and the ROM reports a pass.
#include "../test.hpp"
#include "agb/input.hpp"
#include "agb/memory.hpp"

using namespace agb;

namespace {
struct Thing {
    int value;
    explicit Thing(int v) : value(v) {}
};
}  // namespace

int main() {
    system::init();
    for (int i = 0; i < 3; ++i) system::waitVBlank();
    input::update();
    Pool<Thing, 4> pool("things");
    const auto first = pool.create(1);
    pool.destroy(first);
    if (input::held(Key::A)) {
        debug::log("freeing the object a second time");
        pool.destroy(first);
        if (pool.count() != 0) debug::testFail("the second free changed the pool");
    } else {
        const auto second = pool.create(2);  // reuses the slot with a new generation
        debug::logf("slot %d reused (generation %d -> %d); reading through the old handle", second.index, first.gen, second.gen);
        const Thing* t = pool.get(first);
        if (t) debug::testFail("the stale handle returned an object");
    }
    if (debug::channel().testStatus == static_cast<u32>(debug::TestStatus::Running)) debug::testPass();
    for (;;) system::waitVBlank();
}
