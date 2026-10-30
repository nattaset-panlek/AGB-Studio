#include "agb/natives.hpp"

#include "agb/api.hpp"
#include "agb/audio.hpp"
#include "agb/behaviours.hpp"
#include "agb/debug.hpp"
#include "agb/game.hpp"
#include "agb/render/effects.hpp"
#include "agb/rng.hpp"
#include "agb/text.hpp"

namespace agb::script::natives {

namespace {
SayHook g_say = nullptr;
BusyHook g_busy = nullptr;
ChoiceHook g_choice = nullptr;
ChoicePoll g_poll = nullptr;
ShowTextHook g_showText = nullptr;
GiveItemHook g_giveItem = nullptr;
LockHook g_lockHook = nullptr;
const MovePath* g_paths = nullptr;
u16 g_pathCount = 0;
const char* const* g_names = nullptr;
u16 g_nameCount = 0;
s8 g_autoChoice = 0;
u8 g_locks = 0;
Stats g_stats{};

s32 arg(const s32* args, int argc, int i, s32 fallback) { return i < argc ? args[i] : fallback; }

const char* name(s32 index) { return index >= 0 && index < g_nameCount ? g_names[index] : nullptr; }

Actor* actorArg(s32 ref) { return ref == kPlayer ? player() : actors::at(ref); }

s16 g_blipSound = -1;
void blip() {
    if (g_blipSound >= 0) audio::playSample(static_cast<u16>(g_blipSound));
}

bool sayDone(Context& c) {
    AGB_UNUSED(c);
    return !g_busy || !g_busy();
}

bool textSayDone(Context& c) {
    AGB_UNUSED(c);
    return !text::waiting();
}

bool choiceDone(Context& c) {
    const s8 picked = g_poll ? g_poll() : g_autoChoice;
    if (picked < 0) return false;
    c.waitResult = picked;
    return true;
}

// The text box lists only visible options; userData[0..1] map its index back to the script's option index.
bool textChoiceDone(Context& c) {
    const int picked = text::choice();
    if (picked < 0) return false;
    const u32 map = static_cast<u32>(c.userData[picked >> 2]);
    c.waitResult = static_cast<s32>((map >> ((picked & 3) * 8)) & 0xFF);
    return true;
}

bool framesDone(Context& c) { return --c.userData[0] <= 0; }

bool resumed(Context& c) {
    if (!c.userData[0]) return false;
    c.waitResult = c.userData[1];
    return true;
}

// Moves one step toward `target` on one axis; returns true when it is there.
bool approach(fixed& v, fixed target, fixed speed) {
    const fixed d = target - v;
    if (abs(d) <= speed) {
        v = target;
        return true;
    }
    v += d.raw > 0 ? speed : -speed;
    return false;
}

bool moveStep(Context& c) {
    Actor* a = actors::at(c.userData[0]);
    if (!a || c.userData[1] < 0 || c.userData[1] >= g_pathCount) return true;
    const MovePath& p = g_paths[c.userData[1]];
    const int i = c.userData[2];
    if (i >= p.count) return true;
    const s16 ox = static_cast<s16>(static_cast<u32>(c.userData[3]) >> 16);
    const s16 oy = static_cast<s16>(c.userData[3] & 0xFFFF);
    const fixed tx = fixed(p.points[i * 2] + (p.relative ? ox : 0));
    const fixed ty = fixed(p.points[i * 2 + 1] + (p.relative ? oy : 0));
    const fixed speed = p.speed.raw > 0 ? p.speed : fixed(1);
    const bool atX = approach(a->pos.x, tx, speed);
    const bool atY = approach(a->pos.y, ty, speed);
    // A physics body would fight the scripted movement: hold it still.
    if (PlatformerBody* b = a->get<PlatformerBody>()) b->vx = b->vy = fixed();
    if (!atX || !atY) return false;
    return ++c.userData[2] >= p.count;
}

s32 fade(Context& ctx, const s32* args, int argc, bool in) {
    s32 frames = (arg(args, argc, 0, fixed::kOne / 2) * 60) >> fixed::kShift;
    if (frames < 1) frames = 1;
    if (in) render::effects::beginIn(render::effects::Transition::Fade, static_cast<u16>(frames));
    else render::effects::beginOut(render::effects::Transition::Fade, static_cast<u16>(frames));
    ctx.userData[0] = frames;
    ctx.wait = framesDone;
    return 0;
}
}  // namespace

// ---- built-in natives ------------------------------------------------------------------------------

s32 Say(Context& ctx, const s32* args, int argc) {
    SayRequest r{static_cast<u16>(arg(args, argc, 0, 0)), static_cast<s16>(arg(args, argc, 1, -1)),
                 static_cast<s16>(arg(args, argc, 2, -1)), static_cast<s8>(arg(args, argc, 3, -1)),
                 static_cast<s16>(arg(args, argc, 4, -1))};
    ++g_stats.says;
    g_stats.lastText = r.text;
    if (g_say) {
        g_say(r);
        ctx.wait = sayDone;
        return 0;
    }
    g_blipSound = r.blip;
    if (text::say(r.text, r.blip >= 0 ? blip : nullptr, r.speed)) {
        ctx.wait = textSayDone;
        return 0;
    }
    debug::logf("say %u (speaker %d)", r.text, r.speaker);
    return 0;
}

s32 Choice(Context& ctx, const s32* args, int argc) {
    s16 options[8];
    const int n = argc < 8 ? argc : 8;
    for (int i = 0; i < n; ++i) options[i] = static_cast<s16>(args[i]);
    ++g_stats.choices;
    if (g_choice) {
        g_choice(options, static_cast<u8>(n));
        ctx.wait = choiceDone;
        return 0;
    }
    if (text::languageCount() > 0) {
        u16 keys[8];
        u8 count = 0;
        ctx.userData[0] = ctx.userData[1] = 0;
        for (int i = 0; i < n; ++i) {
            if (options[i] < 0) continue;
            ctx.userData[count >> 2] = static_cast<s32>(static_cast<u32>(ctx.userData[count >> 2]) | (static_cast<u32>(i) << ((count & 3) * 8)));
            keys[count++] = static_cast<u16>(options[i]);
        }
        text::choose(keys, count);
        ctx.wait = textChoiceDone;
        return 0;
    }
    {
        s8 pick = g_autoChoice;
        if (pick < 0 || pick >= n || options[pick] < 0) {
            for (pick = 0; pick < n && options[pick] < 0; ++pick) {}
        }
        debug::logf("choice of %d: %d", n, pick);
        return pick;
    }
}

s32 Move(Context& ctx, const s32* args, int argc) {
    Actor* a = actorArg(arg(args, argc, 0, kNoActor));
    const s32 path = arg(args, argc, 1, -1);
    if (!a || path < 0 || path >= g_pathCount) {
        debug::logf("warning: Move: no actor %ld or path %ld", static_cast<long>(arg(args, argc, 0, kNoActor)), static_cast<long>(path));
        return 0;
    }
    ++g_stats.moves;
    ctx.userData[0] = a->index;
    ctx.userData[1] = path;
    ctx.userData[2] = 0;
    ctx.userData[3] = static_cast<s32>((static_cast<u32>(a->pos.x.floor() & 0xFFFF) << 16) | static_cast<u32>(a->pos.y.floor() & 0xFFFF));
    ctx.wait = moveStep;
    return 0;
}

s32 FadeIn(Context& ctx, const s32* args, int argc) { return fade(ctx, args, argc, true); }

s32 FadeOut(Context& ctx, const s32* args, int argc) { return fade(ctx, args, argc, false); }

s32 LockAll(Context& ctx, const s32* args, int argc) {
    AGB_UNUSED(ctx);
    AGB_UNUSED(args);
    AGB_UNUSED(argc);
    if (g_locks++ == 0 && g_lockHook) g_lockHook(true);
    return 0;
}

s32 UnlockAll(Context& ctx, const s32* args, int argc) {
    AGB_UNUSED(ctx);
    AGB_UNUSED(args);
    AGB_UNUSED(argc);
    if (g_locks && --g_locks == 0 && g_lockHook) g_lockHook(false);
    return 0;
}

s32 PlaySound(Context& ctx, const s32* args, int argc) {
    AGB_UNUSED(ctx);
    const s32 id = arg(args, argc, 0, 0);
    ++g_stats.sounds;
    g_stats.lastSound = id;
    audio::playSample(static_cast<u16>(id), fixed::fromRaw(arg(args, argc, 1, fixed::kOne)));
    return 0;
}

s32 PlayMusic(Context& ctx, const s32* args, int argc) {
    AGB_UNUSED(ctx);
    audio::playMusic(static_cast<u16>(arg(args, argc, 0, 0)));
    return 0;
}

s32 StopMusic(Context& ctx, const s32* args, int argc) {
    AGB_UNUSED(ctx);
    AGB_UNUSED(args);
    AGB_UNUSED(argc);
    audio::stopMusic();
    return 0;
}

s32 ShakeCamera(Context& ctx, const s32* args, int argc) {
    AGB_UNUSED(ctx);
    scene::shake(static_cast<u8>(arg(args, argc, 0, 2)), static_cast<u16>(arg(args, argc, 1, 20)));
    return 0;
}

s32 GoToScene(Context& ctx, const s32* args, int argc) {
    AGB_UNUSED(ctx);
    game::loadScene(static_cast<u16>(arg(args, argc, 0, 0)));
    return 0;
}

s32 GiveItem(Context& ctx, const s32* args, int argc) {
    AGB_UNUSED(ctx);
    giveItem(static_cast<u16>(arg(args, argc, 0, 0)), arg(args, argc, 1, 0), arg(args, argc, 2, 1));
    return 0;
}

s32 ShowText(Context& ctx, const s32* args, int argc) {
    AGB_UNUSED(ctx);
    showText(static_cast<u16>(arg(args, argc, 0, 0)));
    return 0;
}

s32 FindActor(Context& ctx, const s32* args, int argc) {
    AGB_UNUSED(ctx);
    const char* n = name(arg(args, argc, 0, -1));
    Actor* a = n ? actors::find(n) : nullptr;
    return a ? a->index : kNoActor;
}

s32 Log(Context& ctx, const s32* args, int argc) {
    AGB_UNUSED(ctx);
    const char* n = name(arg(args, argc, 0, -1));
    debug::log(n ? n : "");
    return 0;
}

s32 Random(Context& ctx, const s32* args, int argc) {
    AGB_UNUSED(ctx);
    return rng::range(arg(args, argc, 0, 0), arg(args, argc, 1, 0));
}

s32 SaveWrite(Context& ctx, const s32* args, int argc) {
    AGB_UNUSED(ctx);
    return api::Save::Write(arg(args, argc, 0, 0)) ? 1 : 0;
}

s32 SaveRead(Context& ctx, const s32* args, int argc) {
    AGB_UNUSED(ctx);
    return api::Save::Read(arg(args, argc, 0, 0)) ? 1 : 0;
}

s32 SaveCheckpoint(Context& ctx, const s32* args, int argc) {
    AGB_UNUSED(ctx);
    AGB_UNUSED(args);
    AGB_UNUSED(argc);
    api::Save::Checkpoint();
    return 0;
}

// ---- registration and helpers ----------------------------------------------------------------------

void setDialogueHooks(SayHook say, BusyHook busy, ChoiceHook choice, ChoicePoll poll) {
    g_say = say;
    g_busy = busy;
    g_choice = choice;
    g_poll = poll;
}

void setShowTextHook(ShowTextHook hook) { g_showText = hook; }
void setGiveItemHook(GiveItemHook hook) { g_giveItem = hook; }
void setLockHook(LockHook hook) { g_lockHook = hook; }
bool locked() { return g_locks != 0; }

void setPaths(const MovePath* paths, u16 count) {
    g_paths = paths;
    g_pathCount = count;
}

void setNames(const char* const* names, u16 count) {
    g_names = names;
    g_nameCount = count;
}

void setAutoChoice(s8 index) { g_autoChoice = index; }

Actor* player() {
    Actor* found = nullptr;
    actors::forEach([&](Actor& a) {
        if (!found && a.kind == ActorKind::Player) found = &a;
    });
    return found;
}

void showText(u16 key) {
    g_stats.lastText = key;
    if (g_showText) g_showText(key);
    else if (!text::say(key)) debug::logf("text %u", key);
}

void fadeIn(fixed seconds) {
    const s32 frames = (seconds.raw * 60) >> fixed::kShift;
    render::effects::beginIn(render::effects::Transition::Fade, static_cast<u16>(frames < 1 ? 1 : frames));
}

void fadeOut(fixed seconds) {
    const s32 frames = (seconds.raw * 60) >> fixed::kShift;
    render::effects::beginOut(render::effects::Transition::Fade, static_cast<u16>(frames < 1 ? 1 : frames));
}

void giveItem(u16 table, s32 item, s32 count) {
    g_stats.lastItem = item;
    if (g_giveItem) g_giveItem(table, item, count);
    else debug::logf("give item %ld x%ld (table %u)", static_cast<long>(item), static_cast<long>(count), table);
}

void waitForResume(Context& ctx) {
    ctx.userData[0] = 0;
    ctx.userData[1] = 0;
    ctx.wait = resumed;
}

void resume(s32 result) {
    for (int i = 0; i < kMaxContexts; ++i) {
        Context* c = context(i);
        if (c && c->wait == resumed) {
            c->userData[0] = 1;
            c->userData[1] = result;
        }
    }
}

void reset() {
    if (g_locks && g_lockHook) g_lockHook(false);
    g_locks = 0;
    g_stats = Stats{};
}

const Stats& stats() { return g_stats; }

}  // namespace agb::script::natives
