// Audio: maxmod music and sampled effects from a generated soundbank, music fades, DMG register effects
// and the audio share of the frame budget.
#include "agb/hw.hpp"

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
    CHECK_EQ(audio::musicFade().raw, 0);
    CHECK_EQ(audio::moduleVolume(), 0);
    CHECK_EQ(audio::musicVolume().raw, 128);  // the fade leaves the player's music volume alone
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

AGB_TEST(channel_use_within_maxmod_settings_and_dmg_channels_do_not_clash) {
    // G-HW.08. Sampled effects share the 8 mixing channels Maxmod was given with the music: however many are started,
    // no more than 8 of them play at once, and the music keeps playing.
    audio::setMusicVolume(fixed(1));
    audio::playMusic(agb_sound::MOD_TUNE);
    system::waitVBlank();
    audio::SfxHandle handles[16];
    for (auto& h : handles) h = audio::playSample(agb_sound::SFX_BEEP, fixed::lit(0.5), 0);
    system::waitVBlank();
    int playing = 0;
    for (auto h : handles) playing += audio::sampleActive(h);
    debug::logf("16 effects started with 8 mixing channels: %d play", playing);
    CHECK(playing >= 1 && playing <= 8);
    CHECK(audio::musicPlaying());
    for (auto h : handles) audio::stopSample(h);
    // DMG effects: Maxmod left the four DMG channels enabled on both speakers.
    CHECK_EQ(REG_SNDDMGCNT & 0xFF00, 0xFF00);
    const audio::DmgSfx longSquare{audio::DmgChannel::Square2, 0, 0xF080, 0x8600, nullptr, 10};
    const audio::DmgSfx shortSquare{audio::DmgChannel::Square2, 0, 0xA080, 0x8700, nullptr, 3};
    const audio::DmgSfx noise{audio::DmgChannel::Noise, 0, 0xF100, 0x8023, nullptr, 6};
    audio::playDmg(longSquare);
    audio::playDmg(noise);
    system::waitVBlank();
    auto u = audio::usage();
    CHECK(u.dmg[1] && u.dmg[3]);    // different channels play together
    CHECK(!u.dmg[0] && !u.dmg[2]);  // and leave the others free
    // A second effect on Square 2 replaces the first: the channel is owned for the new effect's 3 frames only.
    audio::playDmg(shortSquare);
    for (int i = 0; i < 4; ++i) system::waitVBlank();
    u = audio::usage();
    CHECK(!u.dmg[1]);
    CHECK(u.dmg[3]);                // the noise effect on its own channel was not disturbed
    CHECK(audio::musicPlaying());   // and neither was the music
    for (int i = 0; i < 4; ++i) system::waitVBlank();
    CHECK(!audio::usage().dmg[3]);
    audio::stopMusic();
}

int main() {
    system::init();
    test::runAll();
}
