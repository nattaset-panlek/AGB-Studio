// Audio (Audio page T-P15, SFX Designer T-P25): music modules and sampled effects through maxmod on
// the two Direct Sound channels, and register-driven effects on the four DMG channels (no sample data).
#pragma once
#include "agb/fixed.hpp"
#include "agb/types.hpp"

namespace agb::audio {

struct Config {
    const void* soundbank = nullptr;  // generated soundbank (mmutil)
    u8 channels = 8;                  // maxmod mixing channels (Figma: 8 ch)
};

// Starts maxmod at 21 kHz with static buffers (no heap) and hooks its VBlank and frame work.
void init(const Config& config);
bool ready();

void playMusic(u16 module, bool loop = true);
void stopMusic();
bool musicPlaying();
void setMusicVolume(fixed volume);  // 0..1
// Ramps the music volume to `target` over `frames` ("Fade on scene change"); stops at 0 if `stopAtZero`.
void fadeMusic(fixed target, u16 frames, bool stopAtZero = true);
fixed musicVolume();
void setMusicPosition(u8 order);

using SfxHandle = u16;
SfxHandle playSample(u16 sample, fixed volume = fixed(1), s8 pan = 0, fixed rate = fixed(1));
void stopSample(SfxHandle handle);
void setEffectsVolume(fixed volume);

// ---- DMG channels ---------------------------------------------------------------------------------
enum class DmgChannel : u8 { Square1, Square2, Wave, Noise };

// Register image of one DMG effect (SFX Designer "Registers": SOUND1CNT_L / _H / _X etc.).
struct DmgSfx {
    DmgChannel channel;
    u16 sweep;      // SOUND1CNT_L (Square 1 only)
    u16 envelope;   // SOUNDxCNT_H: length, duty, envelope step / direction / initial volume
    u16 frequency;  // SOUNDxCNT_X / SOUND4CNT_H: frequency (or noise divider) + length flag + restart
    const u32* wave;  // Wave channel: 32 samples of 4 bits (4 words), else nullptr
    u8 frames;      // how long the effect owns the channel
};

void playDmg(const DmgSfx& sfx);
// The game's SFX Designer effects (generated table); Sound.Play ids with the high bit set index it.
void setDmgTable(const DmgSfx* table, u16 count);
void playDmgId(u16 index);
constexpr u16 kDmgSfxFlag = 0x8000;
void stopDmg(DmgChannel channel);
bool dmgActive(DmgChannel channel);  // SOUNDCNT_X channel status bit

// Channel usage for the Audio page "GBA channels" panel.
struct Usage {
    bool directSoundA, directSoundB;
    bool dmg[4];
    u8 mixingChannelsActive;
};
Usage usage();

void update();  // called once per frame by the frame hook (fades, DMG timers)

}  // namespace agb::audio
