// P3.11 music with MusicMeta (Audio page T-P15): the build reads each module's <file>.meta.json and the runtime applies
// the track's volume, its loop start (the order the song continues at when it ends) and its "Fade on scene change".
// audio/loop.mod has a meta file (volume 0.5, loop start 1, no fade); audio/plain.mod is the same module without one
// (the defaults: volume 0.8, loop start 0, fade). Each module has three short orders (8 rows at speed 3, about 29 frames).
#include "../test.hpp"
#include "agb/api.hpp"
#include "agb/audio.hpp"
#include "agb/game.hpp"
#include "agb/input.hpp"
#include "agb/render/effects.hpp"
#include "soundbank.hpp"

using namespace agb;

namespace {
void init() { audio::init({agb_soundbank, 8, 21, agb_soundbank_music, agb_soundbank_music_count}); }

// Plays the module from its start and reports the order it plays after its first wrap (the song ended and looped).
int orderAfterTheEnd(u16 module) {
    audio::playMusic(module);
    int last = 0;
    for (int f = 0; f < 400; ++f) {
        system::waitVBlank();
        const int order = audio::musicPosition();
        if (order < last) return order;
        last = order;
    }
    return -1;
}

// ---- a game of three scenes without layers: their music and the fade on scene change ---------------------------------
SceneDef g_scenes[3];
const SceneDef* g_sceneList[3] = {&g_scenes[0], &g_scenes[1], &g_scenes[2]};
GameDef g_game;

void setupGame(u16 start, u16 loopVolume = 256, u16 plainVolume = 256) {
    // 0 plays loop.mod (its meta: no fade), 1 keeps the music playing, 2 plays plain.mod (fades). Every scene asks for
    // the fade, so the track's MusicMeta decides. The scenes' music Volume (World Map inspector) is 0..256.
    const s16 music[3] = {static_cast<s16>(agb_sound::MOD_LOOP), -1, static_cast<s16>(agb_sound::MOD_PLAIN)};
    const u16 volume[3] = {loopVolume, 256, plainVolume};
    const char* names[3] = {"Loop", "Keep", "Plain"};
    for (int i = 0; i < 3; ++i)
        g_scenes[i] = {names[i], 15, 10, 0, nullptr, 0, -1, nullptr, 0, {-1, 0, 0, true, 0, 0}, music[i], true, nullptr, 0,
                       nullptr, nullptr, nullptr, 0x0000, false, false, nullptr, volume[i]};
    g_game = {};
    g_game.title = "MUSIC";
    g_game.scenes = g_sceneList;
    g_game.sceneCount = 3;
    g_game.startScene = start;
    g_game.soundbank = agb_soundbank;
    g_game.audioChannels = 8;
    g_game.audioMixKHz = 21;
    g_game.music = agb_soundbank_music;
    g_game.musicCount = agb_soundbank_music_count;
}

void frames(int n) {
    for (int i = 0; i < n; ++i) {
        input::injectState(0);
        game::frame();
    }
}
void waitLoaded() {
    for (int i = 0; i < 120 && (game::loading() || !render::effects::transitionDone()); ++i) frames(1);
}

// What maxmod gets (0..1024): player x game x fade (0..256 each) x the track's MusicMeta volume, as audio.cpp rounds.
int mixed(int player, int game, int fade, int track) { return (((((player * game) >> 8) * fade) >> 8) * track * 1024) >> 16; }
}  // namespace

AGB_TEST(music_meta_comes_from_the_build) {
    CHECK_EQ(agb_soundbank_music_count, 2);
    const audio::MusicInfo& loop = agb_soundbank_music[agb_sound::MOD_LOOP];
    CHECK_EQ(loop.volume, 128);  // 0.5
    CHECK_EQ(loop.loopStart, 1);
    CHECK(!loop.fadeOnSceneChange);
    const audio::MusicInfo& plain = agb_soundbank_music[agb_sound::MOD_PLAIN];
    CHECK_EQ(plain.volume, 205);  // the default 0.8
    CHECK_EQ(plain.loopStart, 0);
    CHECK(plain.fadeOnSceneChange);
}

AGB_TEST(track_volume_scales_the_music_volume) {
    init();
    audio::setMusicVolume(fixed(1));
    audio::playMusic(agb_sound::MOD_LOOP);
    CHECK_EQ(audio::moduleVolume(), 512);  // 1 x 0.5 of maxmod's 1024
    audio::setMusicVolume(fixed::lit(0.5));
    CHECK_EQ(audio::moduleVolume(), 256);  // the Options music volume on top of the track's
    audio::setMusicVolume(fixed(1));
    audio::playMusic(agb_sound::MOD_PLAIN);
    CHECK_EQ(audio::moduleVolume(), 820);  // 0.8
    audio::stopMusic();
}

AGB_TEST(song_end_continues_at_the_loop_start) {
    init();
    // loop.mod: orders 0, 1, 2, then 1 again (its loop start), never 0.
    CHECK_EQ(orderAfterTheEnd(agb_sound::MOD_LOOP), 1);
    CHECK(audio::musicPlaying());
    int orders[3] = {};
    for (int f = 0; f < 150; ++f) {
        system::waitVBlank();
        ++orders[audio::musicPosition() % 3];
    }
    CHECK_EQ(orders[0], 0);
    CHECK(orders[1] > 0 && orders[2] > 0);
    // plain.mod (no meta file): back to order 0.
    CHECK_EQ(orderAfterTheEnd(agb_sound::MOD_PLAIN), 0);
    audio::stopMusic();
}

AGB_TEST(fade_on_scene_change_follows_the_track) {
    // loop.mod says no fade: the music keeps its volume through the change and plays on in the next scene.
    setupGame(0);
    game::boot(g_game);
    waitLoaded();
    CHECK(audio::musicPlaying());
    CHECK_EQ(audio::moduleVolume(), 512);
    game::loadScene(1);
    frames(5);
    CHECK_EQ(audio::musicFade().raw, 256);
    waitLoaded();
    CHECK_EQ(game::currentScene(), 1);
    CHECK(audio::musicPlaying());
    CHECK_EQ(audio::moduleVolume(), 512);
    // plain.mod fades (its default): the volume falls during the transition and the music stops at 0.
    setupGame(2);
    game::boot(g_game);
    waitLoaded();
    CHECK(audio::musicPlaying());
    game::loadScene(1);
    frames(5);
    CHECK(audio::musicFade().raw < 256);
    CHECK_EQ(audio::musicVolume().raw, 256);  // the fade is not the player's music volume
    waitLoaded();
    CHECK_EQ(game::currentScene(), 1);
    CHECK_EQ(audio::musicFade().raw, 0);
    CHECK_EQ(audio::moduleVolume(), 0);
    CHECK(!audio::musicPlaying());
}

// Each scene's own track plays at the scene's music Volume (World Map inspector), and the player's Options music volume
// (the Options "Music" slider calls audio::setMusicVolume) stays what the player chose through scene changes and
// game::boot: a scene start no longer resets it.
AGB_TEST(options_music_volume_survives_scene_changes_and_the_scene_volume_applies) {
    setupGame(0, 128, 192);  // Loop at 0.5, Plain at 0.75
    game::boot(g_game);
    waitLoaded();
    CHECK_EQ(audio::gameMusicVolume().raw, 128);
    CHECK_EQ(audio::moduleVolume(), mixed(256, 128, 256, 128));
    audio::setMusicVolume(fixed::fromRaw(179));  // Options "Music" 7 of 10
    CHECK_EQ(audio::moduleVolume(), mixed(179, 128, 256, 128));
    // Plain starts its own track at its scene's volume; the player's 0.7 stays.
    game::loadScene(2);
    waitLoaded();
    CHECK_EQ(game::currentScene(), 2);
    CHECK(audio::musicPlaying());
    CHECK_EQ(audio::musicVolume().raw, 179);
    CHECK_EQ(audio::gameMusicVolume().raw, 192);
    CHECK_EQ(audio::moduleVolume(), mixed(179, 192, 256, 205));
    // Music.SetVolume changes the game's volume, not the player's.
    api::Music::SetVolume(fixed::lit(0.25));
    CHECK_EQ(audio::musicVolume().raw, 179);
    CHECK_EQ(audio::moduleVolume(), mixed(179, 64, 256, 205));
    // The game booting again (Quit to title, a reset without power-off) keeps the player's volume too.
    game::boot(g_game);
    waitLoaded();
    CHECK_EQ(audio::musicVolume().raw, 179);
    CHECK_EQ(audio::moduleVolume(), mixed(179, 128, 256, 128));
    audio::setMusicVolume(fixed(1));
}

int main() {
    system::init();
    test::runAll();
}
