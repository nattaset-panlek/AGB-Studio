// Actors and behaviours (Scene Editor Hierarchy / Inspector, Component Picker, C# `Behaviour`).
// An actor is a thing in the scene with a position, an optional sprite and a collider; behaviours are
// the C# classes (translated to C++) and built-in components attached to it.
#pragma once
#include "agb/data.hpp"
#include "agb/fixed.hpp"
#include "agb/memory.hpp"
#include "agb/render/sprite.hpp"

namespace agb {

class Actor;

class Behaviour {
public:
    virtual ~Behaviour() = default;
    // Type id: built-in behaviours use 1..63, generated script classes 64 and up.
    virtual u16 typeId() const = 0;
    virtual const char* typeName() const = 0;
    virtual void start() {}
    virtual void update() {}
    virtual void lateUpdate() {}  // after physics (built-in bodies use it)
    virtual void onTouch(Actor& other) { AGB_UNUSED(other); }
    virtual void onDestroy() {}
    // Exposed fields for the live Inspector: count, and name / kind / pointer of field i.
    virtual int fieldCount() const { return 0; }
    virtual const char* fieldName(int i) const { AGB_UNUSED(i); return ""; }
    virtual void* fieldPtr(int i) { AGB_UNUSED(i); return nullptr; }
    virtual u8 fieldKind(int i) const { AGB_UNUSED(i); return 0; }  // 0 int, 1 fixed, 2 bool

    Actor* actor = nullptr;
    bool enabled = true;
    bool started = false;
};

// Kinds from the Add Actor menu (T-M10).
enum class ActorKind : u8 { Empty, Player, Enemy, Pickup, TriggerZone, CameraZone, TextLabel };

struct Box {
    s16 x, y;  // offset from the actor origin
    u16 w, h;
};

constexpr int kMaxBehaviours = 6;

class Actor {
public:
    // C# API surface (GBA.Actor)
    template <typename T>
    T* get() {
        for (int i = 0; i < behaviourCount; ++i)
            if (behaviours[i]->typeId() == T::kTypeId) return static_cast<T*>(behaviours[i]);
        return nullptr;
    }
    template <typename T>
    bool is() {
        return get<T>() != nullptr;
    }
    bool hasTag(u16 t) const { return tag == t; }
    void destroy();
    bool isAlive() const { return alive && !dying; }
    void playAnimation(const char* name);
    void setVisible(bool v);
    void setFacingLeft(bool left);
    Box worldBox() const;  // collider in world pixels
    bool overlaps(const Actor& other) const;

    const char* name = "";
    u16 tag = 0;
    u16 index = 0;
    ActorKind kind = ActorKind::Empty;
    fixed2 pos;          // world pixels of the origin
    fixed2 vel;          // set by bodies; read by the live Inspector
    Box collider{0, 0, 0, 0};
    bool solid = false;  // trigger zones are not solid
    bool alive = false, dying = false, visible = true, facingLeft = false, onGround = false;
    s8 state = 0;        // short state id shown in the Play Mode Inspector ("state Jump")
    render::sprites::Id sprite = render::sprites::kNone;
    const data::SpriteSheet* sheet = nullptr;
    const data::TextRun* label = nullptr;
    Behaviour* behaviours[kMaxBehaviours] = {};
    u8 behaviourCount = 0;
};

using BehaviourCreate = Behaviour* (*)(Arena& arena);

// Spawn data written by the build for each actor in a scene (Hierarchy entries).
struct ActorDef {
    const char* name;
    ActorKind kind;
    const data::SpriteSheet* sheet;
    s16 x, y;
    u16 tag;
    u8 priority;
    const char* anim;      // starting animation (nullptr = frame 0)
    Box collider;          // w == 0: use the sheet's collider
    const BehaviourCreate* behaviours;
    u8 behaviourCount;
    const data::TextRun* label;  // Text Label actors (fixed run), or
    bool hidden;
    s16 labelKey;                // string key looked up in the current language (-1 = none)
};

// Live actor table read by the editor through the debug channel (Play Mode Inspector "live").
struct ActorDebug {
    const char* name;
    s32 x, y, vx, vy;  // fixed raw values
    u16 tag;
    u8 flags;          // bit0 alive, bit1 onGround, bit2 visible, bit3 facingLeft
    s8 state;
    const Actor* actor;
};

namespace actors {

constexpr int kMaxActors = 64;

void reset();
Actor* spawn(const ActorDef& def, Arena& arena);
Actor* spawnAt(const ActorDef& def, Arena& arena, int x, int y);
Actor* find(const char* name);
Actor* findByTag(u16 tag);
Actor* at(int index);
int count();
// Frame steps run by the scene, in blueprint order.
void startNew();
void updateBehaviours();
void lateUpdate();
void touches();          // overlap pairs -> onTouch on both sides
void removeDead();
void syncSprites(int cameraX, int cameraY);
void publishDebug();     // fills the debug channel actor table

template <typename F>
void forEach(F&& f) {
    for (int i = 0; i < kMaxActors; ++i)
        if (Actor* a = at(i); a && a->alive) f(*a);
}

}  // namespace actors
}  // namespace agb
