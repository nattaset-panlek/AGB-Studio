// Driven by tests/AgbStudio.Tests (CSharpDebuggerTests): code shaped like translated C# with scopes and probes.
#include "../test.hpp"
#include "agb/probe.hpp"

using namespace agb;

namespace {
const char* const kFiles[] = {"Game.cs", "PlayerController.cs"};
const char* const kNames[] = {"Frame()", "Update()", "Jump()"};
volatile int g_counter = 0;

void jump() {
    AGB_SCOPE(1, 2);
    AGB_PROBE(1, 30);
    g_counter = g_counter + 1;
}

void update() {
    AGB_SCOPE(1, 1);
    AGB_PROBE(1, 19);
    jump();
    AGB_PROBE(1, 21);
}
}  // namespace

int main() {
    system::init();
    dbg::setNames(kFiles, kNames);
    debug::log("probe test ready");
    for (;;) {
        AGB_SCOPE(0, 0);
        update();
        system::waitVBlank();
    }
}
