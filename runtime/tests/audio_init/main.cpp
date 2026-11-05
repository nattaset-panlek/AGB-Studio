// audio::init where games and tests call it: in main() right after system::init(), after a VBlank wait
// (system::waitVBlank and the BIOS VBlankIntrWait), with stack garbage where it builds maxmod's set-up, and at the
// mixing rate from Project Settings · Audio. maxmod reads the set-up's mixing mode as a 32-bit word while the compiler
// stores the enum in one byte: garbage in the padding made the init hang or restart the ROM.
#include "agb/hw.hpp"

#include "../test.hpp"
#include "agb/audio.hpp"
#include "soundbank.hpp"

using namespace agb;

extern const unsigned char agb_soundbank[];

namespace {
// Timer 0 paces maxmod's sample output: it counts up from 65536 - 16777216 / rate. The lowest value seen over many
// reads is the reload value, which tells the rate maxmod was started with.
u16 lowestTimer0() {
    u16 low = 0xFFFF;
    for (int i = 0; i < 4000; ++i) {
        const u16 t = REG_TM0CNT_L;
        if (t < low) low = t;
    }
    return low;
}
constexpr u16 kReload21kHz = 0xFCE2, kReload16kHz = 0xFBD8;
// mGBA shows the counter a couple of cycles behind the reload, so allow a few counts either way.
bool timer0Reloads(u16 reload) {
    const int low = lowestTimer0();
    return low >= reload - 8 && low <= reload + 8;
}

[[gnu::noinline]] void dirtyStack() {
    volatile u8 junk[256];
    for (auto& b : junk) b = 0xA5;
}

bool playsFor(int frames) {
    audio::playMusic(agb_sound::MOD_TUNE);
    for (int i = 0; i < frames; ++i) system::waitVBlank();
    return audio::musicPlaying();
}
}  // namespace

AGB_TEST(init_in_main_right_after_system_init) {
    CHECK(audio::ready());
    CHECK_EQ(audio::mixKHz(), 21);
    CHECK(playsFor(30));
    CHECK(timer0Reloads(kReload21kHz));
}

AGB_TEST(init_after_wait_vblank) {
    system::waitVBlank();
    audio::init({agb_soundbank, 8});
    CHECK(audio::ready());
    CHECK(playsFor(30));
}

AGB_TEST(init_after_bios_vblank_wait) {
    VBlankIntrWait();
    audio::init({agb_soundbank, 8});
    CHECK(audio::ready());
    CHECK(playsFor(30));
}

AGB_TEST(init_with_garbage_on_the_stack) {
    dirtyStack();
    audio::init({agb_soundbank, 8});
    CHECK(audio::ready());
    CHECK(playsFor(30));
}

AGB_TEST(project_mixing_rate) {
    audio::init({agb_soundbank, 8, 16});
    CHECK_EQ(audio::mixKHz(), 16);
    CHECK(timer0Reloads(kReload16kHz));
    CHECK(playsFor(30));
    // The nearest maxmod rate; a rate above the build's buffers (21 kHz here) falls back to them.
    audio::init({agb_soundbank, 8, 12});
    CHECK_EQ(audio::mixKHz(), 13);
    audio::init({agb_soundbank, 8, 31});
    CHECK_EQ(audio::mixKHz(), 21);
    CHECK(timer0Reloads(kReload21kHz));
    CHECK(playsFor(10));
    audio::stopMusic();
}

int main() {
    system::init();
    audio::init({agb_soundbank, 8});
    test::runAll();
}
