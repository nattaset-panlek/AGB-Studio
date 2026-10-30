#include "agb/audio.hpp"

#include <maxmod.h>
#include <tonc.h>

#include "agb/debug.hpp"
#include "agb/system.hpp"

namespace agb::audio {

namespace {
constexpr int kMaxChannels = 16;
// Buffer sizes from maxmod.h. Mixing memory must be in IWRAM; the rest may live in EWRAM.
u8 g_mixingMemory[MM_MIXLEN_21KHZ] AGB_ALIGN4;  // plain .bss is IWRAM
AGB_EWRAM_BSS u8 g_waveMemory[MM_MIXLEN_21KHZ] AGB_ALIGN4;
AGB_EWRAM_BSS u8 g_modChannels[kMaxChannels * MM_SIZEOF_MODCH] AGB_ALIGN4;
AGB_EWRAM_BSS u8 g_actChannels[kMaxChannels * MM_SIZEOF_ACTCH] AGB_ALIGN4;
AGB_EWRAM_BSS u8 g_mixChannels[kMaxChannels * MM_SIZEOF_MIXCH] AGB_ALIGN4;

bool g_ready = false;
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
        const int ch = config.channels > kMaxChannels ? kMaxChannels : config.channels;
        mm_gba_system sys;
        sys.mixing_mode = MM_MIX_21KHZ;
        sys.mod_channel_count = static_cast<mm_word>(ch);
        sys.mix_channel_count = static_cast<mm_word>(ch);
        sys.module_channels = reinterpret_cast<mm_addr>(g_modChannels);
        sys.active_channels = reinterpret_cast<mm_addr>(g_actChannels);
        sys.mixing_channels = reinterpret_cast<mm_addr>(g_mixChannels);
        sys.mixing_memory = reinterpret_cast<mm_addr>(g_mixingMemory);
        sys.wave_memory = reinterpret_cast<mm_addr>(g_waveMemory);
        sys.soundbank = const_cast<mm_addr>(config.soundbank);
        mmInit(&sys);
        // maxmod resets the DMG master volume; keep the DMG channels audible for register effects.
        REG_SNDDMGCNT = SDMG_BUILD_LR(SDMG_SQR1 | SDMG_SQR2 | SDMG_WAVE | SDMG_NOISE, 7);
        REG_SNDDSCNT = static_cast<u16>(REG_SNDDSCNT | SDS_DMG100);
        system::addVBlankHook(mmVBlank);
        g_ready = true;
    }
    system::setFrameHook(frameHook);
}

bool ready() { return g_ready; }

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

void setEffectsVolume(fixed v) {
    if (!g_ready) return;
    const int x = (v.raw * 1024) >> 8;
    mmSetEffectsVolume(static_cast<mm_word>(x < 0 ? 0 : x > 1024 ? 1024 : x));
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
