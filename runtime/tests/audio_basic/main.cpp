// Audio: maxmod music and sampled effects from a generated soundbank, music fades, DMG register effects
// and the audio share of the frame budget.
#include <tonc.h>

#include "../test.hpp"
#include "agb/audio.hpp"
#include "soundbank.hpp"

using namespace agb;

extern const unsigned char agb_soundbank[];

AGB_TEST(music_plays_and_fades) {
    audio::init({agb_soundbank, 8});
    CHECK(audio::ready());
    audio::playMusic(agb_sound::MOD_TUNE);
    for (int i = 0; i < 30; ++i) system::waitVBlank();
    CHECK(audio::musicPlaying());
    audio::setMusicVolume(fixed::lit(0.5));
    CHECK_EQ(audio::musicVolume().raw, 128);
    audio::fadeMusic(fixed(0), 20);
    for (int i = 0; i < 25; ++i) system::waitVBlank();
    CHECK_EQ(audio::musicVolume().raw, 0);
    CHECK(!audio::musicPlaying());  // stopped at zero
    audio::setMusicVolume(fixed(1));
    audio::playMusic(agb_sound::MOD_TUNE);
    for (int i = 0; i < 10; ++i) system::waitVBlank();
    const u32 audioCycles = profile::stats().thisFrame[static_cast<int>(profile::Category::Audio)];
    debug::logf("audio cycles per frame %lu (%lu%%)", audioCycles, audioCycles * 100 / profile::kCyclesPerFrame);
    CHECK(audioCycles < profile::kCyclesPerFrame / 10);
}

AGB_TEST(sampled_effect_plays) {
    const auto h = audio::playSample(agb_sound::SFX_BEEP, fixed(1), 0);
    CHECK(h != 0);
    system::waitVBlank();
    audio::stopSample(h);
    audio::setEffectsVolume(fixed::lit(0.5));
}

AGB_TEST(dmg_square_effect_owns_channel_for_its_frames) {
    // Square 1 with an upward sweep (SFX Designer "Jump" preset shape).
    const audio::DmgSfx jump{audio::DmgChannel::Square1, 0x0023, 0xF280, 0x06D6, nullptr, 12};
    audio::playDmg(jump);
    CHECK(audio::dmgActive(audio::DmgChannel::Square1));  // status bit set on trigger
    system::waitVBlank();
    CHECK(audio::usage().dmg[0]);  // the sweep ends the tone quickly, but the effect owns the channel 12 frames
    for (int i = 0; i < 14; ++i) system::waitVBlank();
    CHECK(!audio::usage().dmg[0]);
    const u32 wave[4] = {0x01234567, 0x89ABCDEF, 0xFEDCBA98, 0x76543210};
    const audio::DmgSfx tone{audio::DmgChannel::Wave, 0, 0x2000, 0x8400, wave, 5};
    audio::playDmg(tone);
    CHECK(audio::dmgActive(audio::DmgChannel::Wave));
    audio::stopDmg(audio::DmgChannel::Wave);
}

int main() {
    system::init();
    test::runAll();
}
