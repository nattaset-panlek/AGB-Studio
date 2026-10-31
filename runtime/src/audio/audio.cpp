#include "agb/audio.hpp"

#include <maxmod.h>
#include <tonc.h>

#include "agb/debug.hpp"
#include "agb/system.hpp"

namespace agb::audio {

// Project Settings · Audio: the build passes the project's mixing rate and channel count, so the buffers below are the
// size the game needs. Runtime test ROMs mix at 21 kHz (D-14) with up to 16 channels.
#ifndef AGB_AUDIO_MIX_KHZ
#define AGB_AUDIO_MIX_KHZ 21
#endif
#ifndef AGB_AUDIO_CHANNELS
#define AGB_AUDIO_CHANNELS 16
#endif

namespace {
struct Rate {
    u8 khz;
    mm_mixmode mode;
    u16 bytes;  // MM_MIXLEN_*: one frame of mixing at this rate
};
constexpr Rate kRates[] = {
    {8, MM_MIX_8KHZ, MM_MIXLEN_8KHZ},    {10, MM_MIX_10KHZ, MM_MIXLEN_10KHZ}, {13, MM_MIX_13KHZ, MM_MIXLEN_13KHZ},
    {16, MM_MIX_16KHZ, MM_MIXLEN_16KHZ}, {18, MM_MIX_18KHZ, MM_MIXLEN_18KHZ}, {21, MM_MIX_21KHZ, MM_MIXLEN_21KHZ},
    {27, MM_MIX_27KHZ, MM_MIXLEN_27KHZ}, {31, MM_MIX_31KHZ, MM_MIXLEN_31KHZ},
};
// The maxmod rate nearest to `khz` (a tie takes the lower one).
constexpr const Rate& rateFor(int khz) {
    int best = 0;
    for (int i = 1; i < static_cast<int>(sizeof kRates / sizeof kRates[0]); ++i) {
        const int d = kRates[i].khz - khz, bd = kRates[best].khz - khz;
        if ((d < 0 ? -d : d) < (bd < 0 ? -bd : bd)) best = i;
    }
    return kRates[best];
}
constexpr const Rate& kBufferRate = rateFor(AGB_AUDIO_MIX_KHZ);
constexpr int kMaxChannels = AGB_AUDIO_CHANNELS < 1 ? 1 : AGB_AUDIO_CHANNELS > 32 ? 32 : AGB_AUDIO_CHANNELS;

// Buffer sizes from maxmod.h. The mixing memory (maxmod's 32-bit sum of every channel, read and written per sample and
// channel) must be in IWRAM; the wave memory (the 8-bit output the sound DMA plays) and the channel state may live in
// EWRAM.
u8 g_mixingMemory[kBufferRate.bytes] AGB_ALIGN4;  // plain .bss is IWRAM
AGB_EWRAM_BSS u8 g_waveMemory[kBufferRate.bytes] AGB_ALIGN4;
AGB_EWRAM_BSS u8 g_modChannels[kMaxChannels * MM_SIZEOF_MODCH] AGB_ALIGN4;
AGB_EWRAM_BSS u8 g_actChannels[kMaxChannels * MM_SIZEOF_ACTCH] AGB_ALIGN4;
AGB_EWRAM_BSS u8 g_mixChannels[kMaxChannels * MM_SIZEOF_MIXCH] AGB_ALIGN4;

bool g_ready = false;
u8 g_mixKHz = 0;
bool g_musicOn = false;
fixed g_volume = fixed(1), g_fadeFrom, g_fadeTo;
u16 g_fadeFrames = 0, g_fadeElapsed = 0;
bool g_fadeStop = false;
u8 g_dmgTimer[4];

void frameHook() {
    if (g_ready) mmFrame();
    update();
}

void applyVolume() {
    const int v = (g_volume.raw * 1024) >> 8;
    mmSetModuleVolume(static_cast<mm_word>(v < 0 ? 0 : v > 1024 ? 1024 : v));
}
}  // namespace

void init(const Config& config) {
    REG_SNDSTAT = SSTAT_ENABLE;
    REG_SNDDMGCNT = SDMG_BUILD_LR(SDMG_SQR1 | SDMG_SQR2 | SDMG_WAVE | SDMG_NOISE, 7);
    REG_SNDDSCNT = SDS_DMG100;
    if (config.soundbank) {
        int ch = config.channels < 1 ? 1 : config.channels;
        if (ch > kMaxChannels) {
            debug::logf("warning: audio: %d mixing channels asked, this build has buffers for %d", ch, kMaxChannels);
            ch = kMaxChannels;
        }
        const Rate* rate = &rateFor(config.mixKHz ? config.mixKHz : kBufferRate.khz);
        if (rate->bytes > kBufferRate.bytes) {
            debug::logf("warning: audio: mixing at %u kHz needs a larger buffer than this build has; mixing at %u kHz", rate->khz, kBufferRate.khz);
            rate = &kBufferRate;
        }
        mm_gba_system sys;
        // maxmod reads mixing_mode as a 32-bit word, but this compiler stores the enum in one byte (arm-none-eabi
        // short enums): clear the padding first. Stack garbage there picked a wrong buffer length and timer, and the
        // init then hung or restarted the ROM, depending on what the stack held (after a VBlank wait, in main()).
        __builtin_memset(&sys, 0, sizeof sys);
        sys.mixing_mode = rate->mode;
        sys.mod_channel_count = static_cast<mm_word>(ch);
        sys.mix_channel_count = static_cast<mm_word>(ch);
        sys.module_channels = reinterpret_cast<mm_addr>(g_modChannels);
        sys.active_channels = reinterpret_cast<mm_addr>(g_actChannels);
        sys.mixing_channels = reinterpret_cast<mm_addr>(g_mixChannels);
        sys.mixing_memory = reinterpret_cast<mm_addr>(g_mixingMemory);
        sys.wave_memory = reinterpret_cast<mm_addr>(g_waveMemory);
        sys.soundbank = const_cast<mm_addr>(config.soundbank);
        // The VBlank hook goes in first: mmInit waits for two VBlanks, and maxmod's VBlank work during them leaves its
        // double buffer at the first half for the next mix. Hooked afterwards, a second init could mix the first frame
        // at the write position the previous init left (past the end of the wave buffer).
        g_ready = false;
        system::addVBlankHook(mmVBlank);
        mmInit(&sys);
        // maxmod resets the DMG master volume; keep the DMG channels audible for register effects.
        REG_SNDDMGCNT = SDMG_BUILD_LR(SDMG_SQR1 | SDMG_SQR2 | SDMG_WAVE | SDMG_NOISE, 7);
        REG_SNDDSCNT = static_cast<u16>(REG_SNDDSCNT | SDS_DMG100);
        g_ready = true;
        g_mixKHz = rate->khz;
    }
    system::setFrameHook(frameHook);
}

bool ready() { return g_ready; }
u8 mixKHz() { return g_mixKHz; }

void playMusic(u16 module, bool loop) {
    if (!g_ready) return;
    mmStart(module, loop ? MM_PLAY_LOOP : MM_PLAY_ONCE);
    g_musicOn = true;
    g_fadeFrames = 0;
    applyVolume();
}

void stopMusic() {
    if (!g_ready) return;
    mmStop();
    g_musicOn = false;
}

bool musicPlaying() { return g_ready && mmActive(); }

void setMusicVolume(fixed v) {
    g_volume = clamp(v, fixed(), fixed(1));
    g_fadeFrames = 0;
    if (g_ready) applyVolume();
}

void fadeMusic(fixed target, u16 frames, bool stopAtZero) {
    g_fadeFrom = g_volume;
    g_fadeTo = clamp(target, fixed(), fixed(1));
    g_fadeFrames = frames ? frames : 1;
    g_fadeElapsed = 0;
    g_fadeStop = stopAtZero;
}

fixed musicVolume() { return g_volume; }

void setMusicPosition(u8 order) {
    if (g_ready) mmPosition(order);
}

SfxHandle playSample(u16 sample, fixed volume, s8 pan, fixed rate) {
    if (!g_ready) return 0;
    mm_sound_effect fx;
    fx.id = sample;
    fx.rate = static_cast<mm_hword>((rate.raw * 1024) >> 8);
    fx.handle = 0;
    const int v = (volume.raw * 255) >> 8;
    fx.volume = static_cast<mm_byte>(v < 0 ? 0 : v > 255 ? 255 : v);
    fx.panning = static_cast<mm_byte>(128 + pan);
    return mmEffectEx(&fx);
}

void stopSample(SfxHandle h) {
    if (g_ready) mmEffectCancel(h);
}

bool sampleActive(SfxHandle h) { return g_ready && h != 0 && mmEffectActive(h); }

void setEffectsVolume(fixed v) {
    if (!g_ready) return;
    const int x = (v.raw * 1024) >> 8;
    mmSetEffectsVolume(static_cast<mm_word>(x < 0 ? 0 : x > 1024 ? 1024 : x));
}

namespace {
const DmgSfx* g_dmgTable = nullptr;
u16 g_dmgCount = 0;
}  // namespace

void setDmgTable(const DmgSfx* table, u16 count) {
    g_dmgTable = table;
    g_dmgCount = count;
}

void playDmgId(u16 index) {
    if (g_dmgTable && index < g_dmgCount) playDmg(g_dmgTable[index]);
    else debug::logf("warning: no DMG sound effect %u", index);
}

void playDmg(const DmgSfx& s) {
    const int c = static_cast<int>(s.channel);
    switch (s.channel) {
        case DmgChannel::Square1:
            REG_SND1SWEEP = s.sweep;
            REG_SND1CNT = s.envelope;
            REG_SND1FREQ = static_cast<u16>(s.frequency | SFREQ_RESET);
            break;
        case DmgChannel::Square2:
            REG_SND2CNT = s.envelope;
            REG_SND2FREQ = static_cast<u16>(s.frequency | SFREQ_RESET);
            break;
        case DmgChannel::Wave:
            REG_SND3SEL = 0;  // select bank 1 for writing while bank 0 plays
            if (s.wave)
                for (int i = 0; i < 4; ++i) reinterpret_cast<volatile u32*>(REG_BASE + 0x90)[i] = s.wave[i];
            REG_SND3SEL = 0x40 | 0x80;  // play bank 1 (just written), 32 samples, enabled
            REG_SND3CNT = s.envelope;
            REG_SND3FREQ = static_cast<u16>(s.frequency | SFREQ_RESET);
            break;
        case DmgChannel::Noise:
            REG_SND4CNT = s.envelope;
            REG_SND4FREQ = static_cast<u16>(s.frequency | SFREQ_RESET);
            break;
    }
    g_dmgTimer[c] = s.frames;
}

void stopDmg(DmgChannel channel) {
    switch (channel) {
        case DmgChannel::Square1: REG_SND1CNT = 0; REG_SND1FREQ = SFREQ_RESET; break;
        case DmgChannel::Square2: REG_SND2CNT = 0; REG_SND2FREQ = SFREQ_RESET; break;
        case DmgChannel::Wave: REG_SND3SEL = 0; break;
        case DmgChannel::Noise: REG_SND4CNT = 0; REG_SND4FREQ = SFREQ_RESET; break;
    }
    g_dmgTimer[static_cast<int>(channel)] = 0;
}

bool dmgActive(DmgChannel channel) { return REG_SNDSTAT & (1 << static_cast<int>(channel)); }

Usage usage() {
    Usage u{};
    u.directSoundA = u.directSoundB = g_ready && mmActive();
    for (int i = 0; i < 4; ++i) u.dmg[i] = dmgActive(static_cast<DmgChannel>(i)) || g_dmgTimer[i];
    u.mixingChannelsActive = 0;
    return u;
}

void update() {
    if (g_fadeFrames && g_fadeElapsed < g_fadeFrames) {
        ++g_fadeElapsed;
        g_volume = g_fadeFrom + (g_fadeTo - g_fadeFrom) * static_cast<int>(g_fadeElapsed) / static_cast<int>(g_fadeFrames);
        if (g_ready) applyVolume();
        if (g_fadeElapsed == g_fadeFrames && g_fadeStop && g_fadeTo.raw == 0) stopMusic();
    }
    for (int i = 0; i < 4; ++i)
        if (g_dmgTimer[i] && --g_dmgTimer[i] == 0) stopDmg(static_cast<DmgChannel>(i));
}

}  // namespace agb::audio
