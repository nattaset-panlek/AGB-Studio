// Audio (Audio page T-P15, SFX Designer T-P25): music modules and sampled effects through maxmod on
// the two Direct Sound channels, and register-driven effects on the four DMG channels (no sample data).
#pragma once
#include "agb/fixed.hpp"
#include "agb/types.hpp"

namespace agb::audio {

// A music module's MusicMeta (assets/audio/music/<file>.meta.json, Audio page T-P15), by module id. The build writes the
// table next to the soundbank (agb_soundbank_music) and the loop start into the module itself (maxmod's restart
// order), so a looping song continues there when it ends.
struct MusicInfo {
    u16 volume;              // the track's volume, 0..256 (256 = full); the music volume is multiplied by it
    u8 loopStart;            // pattern order the song continues at when it ends
    bool fadeOnSceneChange;  // fade out when the scene changes (with the scene's own setting)
};

struct Config {
    const void* soundbank = nullptr;  // generated soundbank (mmutil)
    u8 channels = 8;                  // maxmod mixing channels (Project Settings · Audio, Figma: 8 ch)
    u8 mixKHz = 21;                   // mixing rate (Project Settings · Audio): 8, 10, 13, 16, 18, 21, 27 or 31 kHz
    const MusicInfo* music = nullptr; // MusicMeta by module id (nullptr = every track at full volume, fading)
    u16 musicCount = 0;
};

// Starts maxmod with static buffers (no heap) and hooks its VBlank and frame work. The buffers are sized at compile time
// for AGB_AUDIO_MIX_KHZ and AGB_AUDIO_CHANNELS (the build passes the project's settings; 21 kHz and 16 channels
// otherwise); a rate or channel count above them falls back to them. May be called again (after a VBlank wait too).
void init(const Config& config);
u8 mixKHz();  // the rate maxmod mixes at (0 = not started)
bool ready();

void playMusic(u16 module, bool loop = true);
void stopMusic();
bool musicPlaying();
void setMusicVolume(fixed volume);  // 0..1
// Ramps the music volume to `target` over `frames` ("Fade on scene change"); stops at 0 if `stopAtZero`.
void fadeMusic(fixed target, u16 frames, bool stopAtZero = true);
fixed musicVolume();
// The volume maxmod mixes the module at, 0..1024: the music volume times the track's MusicMeta volume.
u16 moduleVolume();
// The MusicMeta "Fade on scene change" of the module playing (true when the game has no music table).
bool musicFadesOnSceneChange();
void setMusicPosition(u8 order);
u8 musicPosition();  // the pattern order playing now

using SfxHandle = u16;
SfxHandle playSample(u16 sample, fixed volume = fixed(1), s8 pan = 0, fixed rate = fixed(1));
void stopSample(SfxHandle handle);
bool sampleActive(SfxHandle handle);  // the effect still owns a mixing channel
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

// Each DMG channel plays one effect at a time: a new effect on a busy channel replaces the one playing there (and owns
// the channel for its own `frames`); effects on different channels play together. Maxmod uses only the Direct Sound
// channels, so music never takes a DMG channel.
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
