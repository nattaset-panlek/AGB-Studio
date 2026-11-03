#include "agb/actor.hpp"

#include "agb/behaviours.hpp"
#include "agb/debug.hpp"

namespace agb {

namespace {
// Zeroed storage (alive = false); reset() constructs the actors properly.
alignas(Actor) AGB_EWRAM_BSS u8 g_actorStorage[sizeof(Actor) * actors::kMaxActors];
Actor* const g_actors = reinterpret_cast<Actor*>(g_actorStorage);

struct AllActors {
    Actor* begin() const { return g_actors; }
    Actor* end() const { return g_actors + actors::kMaxActors; }
};
constexpr AllActors kAll{};
AGB_EWRAM_BSS_NAMED(agb_actor_table) ActorDebug g_debug[actors::kMaxActors];
// Behaviour memory of each actor slot (reused with the slot). Behaviours that do not fit go to the scene arena.
constexpr u32 kSlotBytes = 128;
alignas(8) AGB_EWRAM_BSS u8 g_slotMemory[actors::kMaxActors][kSlotBytes];

int g_parentLinks = 0;  // no links: the hierarchy costs nothing per frame
u8 g_followStamp = 0;

void refreshShown(Actor& a) {
    a.hiddenInTree = !a.visible || (a.parent && a.parent->hiddenInTree);
    if (a.sprite != render::sprites::kNone) render::sprites::setVisible(a.sprite, !a.hiddenInTree);
    if (g_parentLinks == 0) return;
    for (Actor& c : kAll)
        if (c.alive && c.parent == &a) refreshShown(c);
}

// Parents first; each actor adds what its parent moved since the last pass to its own position.
void follow(Actor& a) {
    if (a.followStamp == g_followStamp) return;
    a.followStamp = g_followStamp;
    Actor* p = a.parent;
    if (!p) return;
    follow(*p);
    a.pos.x.raw += p->pos.x.raw - p->lastPos.x.raw;
    a.pos.y.raw += p->pos.y.raw - p->lastPos.y.raw;
}

bool streq(const char* a, const char* b) {
    if (!a || !b) return false;
    while (*a && *a == *b) ++a, ++b;
    return *a == *b;
}
}  // namespace

void Actor::destroy() {
    if (!alive || dying) return;
    dying = true;
    if (g_parentLinks == 0) return;
    for (Actor& c : kAll)
        if (c.alive && c.parent == this) c.destroy();
}

void Actor::setParent(Actor* newParent) {
    if (newParent == parent) return;
    for (Actor* p = newParent; p; p = p->parent) {
        if (p == this) {
            debug::warnOnce(21, "SetParent: an actor cannot be the child of itself or of its own children, so the parent did not change.");
            return;
        }
    }
    if (parent) --g_parentLinks;
    parent = newParent;
    if (parent) {
        ++g_parentLinks;
        parent->lastPos = parent->pos;
    }
    lastPos = pos;
    refreshShown(*this);
}

void Actor::playAnimation(const char* animName) {
    if (sprite != render::sprites::kNone) render::sprites::playByName(sprite, animName);
}

void Actor::setVisible(bool v) {
    visible = v;
    refreshShown(*this);
}

void Actor::setFacingLeft(bool left) {
    facingLeft = left;
    if (sprite != render::sprites::kNone) render::sprites::setFlip(sprite, left, false);
}

Box Actor::worldBox() const {
    return Box{static_cast<s16>(pos.x.floor() + collider.x), static_cast<s16>(pos.y.floor() + collider.y), collider.w, collider.h};
}

bool Actor::overlaps(const Actor& o) const {
    const Box a = worldBox(), b = o.worldBox();
    if (!a.w || !a.h || !b.w || !b.h) return false;
    return a.x < b.x + b.w && b.x < a.x + a.w && a.y < b.y + b.h && b.y < a.y + a.h;
}

namespace actors {

void reset() {
    g_parentLinks = 0;
    for (int i = 0; i < kMaxActors; ++i) {
        Actor& a = g_actors[i];
        if (a.alive && a.sprite != render::sprites::kNone) render::sprites::destroy(a.sprite);
        new (&a) Actor();
    }
    debug::channel().actorTable = g_debug;
    debug::channel().actorStride = sizeof(ActorDebug);
    debug::channel().actorCount = 0;
}

Actor* spawnAt(const ActorDef& def, Arena& arena, int x, int y) {
    for (int i = 0; i < kMaxActors; ++i) {
        Actor& a = g_actors[i];
        if (a.alive) continue;
        a = Actor{};
        a.alive = true;
        a.index = static_cast<u16>(i);
        a.name = def.name;
        a.kind = def.kind;
        a.tag = def.tag;
        a.sheet = def.sheet;
        a.label = def.label;
        a.pos = {fixed(x), fixed(y)};
        a.solid = def.kind != ActorKind::TriggerZone && def.kind != ActorKind::CameraZone && def.kind != ActorKind::TextLabel && def.kind != ActorKind::Panel && def.kind != ActorKind::Canvas;
        if (def.collider.w) a.collider = def.collider;
        else if (def.sheet)
            a.collider = Box{static_cast<s16>(def.sheet->colliderX - def.sheet->originX), static_cast<s16>(def.sheet->colliderY - def.sheet->originY),
                             def.sheet->colliderW, def.sheet->colliderH};
        if (def.sheet) {
            a.sprite = render::sprites::create(def.sheet, def.priority);
            if (def.anim) render::sprites::playByName(a.sprite, def.anim);
        }
        if (def.hidden) a.setVisible(false);
        Arena slot(g_slotMemory[i], kSlotBytes, nullptr);  // quiet: a full slot falls back to the arena
        for (int b = 0; b < def.behaviourCount && b < kMaxBehaviours; ++b) {
            Behaviour* beh = def.behaviours[b](slot);
            if (!beh) beh = def.behaviours[b](arena);
            if (!beh) continue;
            beh->actor = &a;
            a.behaviours[a.behaviourCount++] = beh;
        }
        return &a;
    }
    debug::warnOnce(20, "all 64 actors are in use, so an actor was not spawned. Destroy actors you no longer need.");
    return nullptr;
}

Actor* spawn(const ActorDef& def, Arena& arena) { return spawnAt(def, arena, def.x, def.y); }

Actor* find(const char* name) {
    for (Actor& a : kAll)
        if (a.alive && !a.dying && streq(a.name, name)) return &a;
    return nullptr;
}

Actor* findByTag(u16 tag) {
    for (Actor& a : kAll)
        if (a.alive && !a.dying && a.tag == tag) return &a;
    return nullptr;
}

Actor* at(int index) { return index >= 0 && index < kMaxActors && g_actors[index].alive ? &g_actors[index] : nullptr; }

int count() {
    int n = 0;
    for (Actor& a : kAll) n += a.alive && !a.dying;
    return n;
}

void startNew() {
    for (Actor& a : kAll) {
        if (!a.alive) continue;
        for (int i = 0; i < a.behaviourCount; ++i) {
            Behaviour* b = a.behaviours[i];
            if (b->started) continue;
            b->started = true;
            b->start();
        }
    }
}

void updateBehaviours() {
    for (Actor& a : kAll) {
        if (!a.alive || a.dying) continue;
        for (int i = 0; i < a.behaviourCount; ++i)
            if (a.behaviours[i]->enabled && a.behaviours[i]->started) a.behaviours[i]->update();
    }
}

void lateUpdate() {
    for (Actor& a : kAll) {
        if (!a.alive || a.dying) continue;
        for (int i = 0; i < a.behaviourCount; ++i)
            if (a.behaviours[i]->enabled && a.behaviours[i]->started) a.behaviours[i]->lateUpdate();
    }
}

void followParents() {
    if (g_parentLinks == 0) return;
    ++g_followStamp;
    for (Actor& a : kAll)
        if (a.alive) follow(a);
    for (Actor& a : kAll)
        if (a.alive) a.lastPos = a.pos;
}

void linkParents(const ActorDef* defs, int count) {
    for (int i = 0; i < count; ++i) {
        if (defs[i].parent < 0) continue;
        Actor* child = at(i);
        Actor* parent = at(defs[i].parent);
        if (child && parent) child->setParent(parent);
    }
}

// Sort and sweep: the boxes of all colliding actors are taken once, sorted by left edge, and each actor is only
// compared with the ones that start before its right edge (a stage full of bullets stays cheap). Two projectiles
// never touch each other: bullets fired together start on top of one another.
void touches() {
    struct Entry {
        s16 x0, x1, y0, y1;
        u8 index;
        bool projectile;
    };
    Entry list[kMaxActors];
    int n = 0;
    for (int i = 0; i < kMaxActors; ++i) {
        Actor& a = g_actors[i];
        if (!a.alive || a.dying || !a.collider.w || !a.collider.h) continue;
        const Box b = a.worldBox();
        Entry e{b.x, static_cast<s16>(b.x + b.w), b.y, static_cast<s16>(b.y + b.h), static_cast<u8>(i), a.is<Projectile>()};
        int k = n++;
        while (k > 0 && list[k - 1].x0 > e.x0) {
            list[k] = list[k - 1];
            --k;
        }
        list[k] = e;
    }
    for (int i = 0; i < n; ++i) {
        Actor& a = g_actors[list[i].index];
        for (int j = i + 1; j < n && list[j].x0 < list[i].x1 && !a.dying; ++j) {
            if (list[j].y0 >= list[i].y1 || list[i].y0 >= list[j].y1 || (list[i].projectile && list[j].projectile)) continue;
            Actor& b = g_actors[list[j].index];
            if (b.dying) continue;
            for (int k = 0; k < a.behaviourCount && !a.dying; ++k)
                if (a.behaviours[k]->enabled) a.behaviours[k]->onTouch(b);
            for (int k = 0; k < b.behaviourCount && !b.dying; ++k)
                if (b.behaviours[k]->enabled) b.behaviours[k]->onTouch(a);
        }
    }
}

void removeDead() {
    for (Actor& a : kAll) {
        if (!a.alive || !a.dying) continue;
        for (int i = 0; i < a.behaviourCount; ++i) {
            a.behaviours[i]->onDestroy();
            a.behaviours[i]->~Behaviour();
        }
        if (a.sprite != render::sprites::kNone) render::sprites::destroy(a.sprite);
        if (a.parent) --g_parentLinks;
        a = Actor{};
    }
}

void syncSprites(int cameraX, int cameraY) {
    for (Actor& a : kAll)
        if (a.alive && a.sprite != render::sprites::kNone) render::sprites::setPosition(a.sprite, a.pos.x.floor() - cameraX, a.pos.y.floor() - cameraY);
}

void publishDebug() {
    u32 n = 0;
    for (Actor& a : kAll) {
        if (!a.alive) continue;
        ActorDebug& d = g_debug[n++];
        d.name = a.name;
        d.x = a.pos.x.raw;
        d.y = a.pos.y.raw;
        d.vx = a.vel.x.raw;
        d.vy = a.vel.y.raw;
        d.tag = a.tag;
        d.flags = static_cast<u8>((a.alive ? 1 : 0) | (a.onGround ? 2 : 0) | (a.visible ? 4 : 0) | (a.facingLeft ? 8 : 0));
        d.state = a.state;
        d.actor = &a;
    }
    debug::channel().actorCount = n;
}

}  // namespace actors
}  // namespace agb
