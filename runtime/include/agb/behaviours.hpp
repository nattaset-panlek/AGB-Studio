// Built-in behaviours from the Component Picker (T-O05). Field names match the Inspector and the C# API
// (PlatformerBody.vx / vy / OnGround / gravity / jumpForce / maxSpeed ...).
#pragma once
#include "agb/actor.hpp"
#include "agb/fixed.hpp"

namespace agb {

namespace builtin {
enum : u16 {
    PlatformerBodyId = 1, TopDownMoverId, ProjectileId, HealthId, DamageOnTouchId, PatrolId, ChasePlayerId, SoundOnEventId,
};
}

// Moves with gravity and collides with Solid / One-way / Ladder / Water / Hazard tiles.
class PlatformerBody final : public Behaviour {
public:
    static constexpr u16 kTypeId = builtin::PlatformerBodyId;
    u16 typeId() const override { return kTypeId; }
    const char* typeName() const override { return "PlatformerBody"; }
    void lateUpdate() override;
    int fieldCount() const override { return 5; }
    const char* fieldName(int i) const override;
    void* fieldPtr(int i) override;
    u8 fieldKind(int i) const override { return i < 5 ? 1 : 0; }

    fixed gravity = fixed::lit(0.25);
    fixed jumpForce = fixed::lit(3.5);
    fixed maxSpeed = fixed::lit(1.5);
    fixed maxFall = fixed(4);
    fixed vx, vy;
    bool onGround = false, onLadder = false, inWater = false, touchingHazard = false;
    bool hitWall = false, hitCeiling = false;
    u16 framesSinceGround = 0;  // for coyote time in scripts
    bool climbing = false;      // set by scripts while on a ladder

    void jump() { vy = -jumpForce; onGround = false; }
};

// 8-way movement with the D-pad (or set vx / vy from a script when playerControlled is false).
class TopDownMover final : public Behaviour {
public:
    static constexpr u16 kTypeId = builtin::TopDownMoverId;
    u16 typeId() const override { return kTypeId; }
    const char* typeName() const override { return "TopDownMover"; }
    void lateUpdate() override;
    fixed speed = fixed(1);
    bool playerControlled = true;
    fixed vx, vy;
    u8 facing = 2;  // 0 up, 1 right, 2 down, 3 left
};

// Moves in a straight line and hits things (shmup bullets, thrown items).
class Projectile final : public Behaviour {
public:
    static constexpr u16 kTypeId = builtin::ProjectileId;
    u16 typeId() const override { return kTypeId; }
    const char* typeName() const override { return "Projectile"; }
    void lateUpdate() override;
    void onTouch(Actor& other) override;
    fixed vx, vy;
    u16 lifetime = 120;  // frames
    u8 damage = 1;
    bool hitsPlayer = false;  // enemy bullets hit the player, player bullets hit everything else
    bool destroyOnWall = true;
};

// Hearts and invincibility frames.
class Health final : public Behaviour {
public:
    static constexpr u16 kTypeId = builtin::HealthId;
    u16 typeId() const override { return kTypeId; }
    const char* typeName() const override { return "Health"; }
    void update() override;
    int fieldCount() const override { return 2; }
    const char* fieldName(int i) const override { return i == 0 ? "hearts" : "maxHearts"; }
    void* fieldPtr(int i) override { return i == 0 ? static_cast<void*>(&hearts) : &maxHearts; }
    u8 fieldKind(int) const override { return 0; }

    int hearts = 3, maxHearts = 3;
    u16 invincibleFrames = 60;
    u16 invincibleLeft = 0;
    bool destroyOnDeath = true;
    bool dead = false;
    void (*onDamaged)(Actor& self) = nullptr;
    void (*onDeath)(Actor& self) = nullptr;

    // Returns true when the damage was taken (false while invincible).
    bool damage(int amount);
    void heal(int amount);
};

// Hurts what it touches: the player by default (enemies, spikes).
class DamageOnTouch final : public Behaviour {
public:
    static constexpr u16 kTypeId = builtin::DamageOnTouchId;
    u16 typeId() const override { return kTypeId; }
    const char* typeName() const override { return "DamageOnTouch"; }
    void onTouch(Actor& other) override;
    u8 damage = 1;
    bool onlyPlayer = true;
};

// Walks back and forth; turns at walls, at ledges, or after `distance` pixels.
class Patrol final : public Behaviour {
public:
    static constexpr u16 kTypeId = builtin::PatrolId;
    u16 typeId() const override { return kTypeId; }
    const char* typeName() const override { return "Patrol"; }
    void start() override;
    void update() override;
    fixed speed = fixed::lit(0.5);
    u16 distance = 0;  // 0 = until a wall or ledge
    bool turnAtLedges = true;
    s8 dir = -1;

private:
    fixed origin_;
};

// Follows the player when they come within `range` pixels.
class ChasePlayer final : public Behaviour {
public:
    static constexpr u16 kTypeId = builtin::ChasePlayerId;
    u16 typeId() const override { return kTypeId; }
    const char* typeName() const override { return "ChasePlayer"; }
    void update() override;
    u16 range = 96;
    fixed speed = fixed::lit(0.75);
    bool chasing = false;
};

enum class SoundEvent : u8 { Touch, Jump, Spawn, Destroy };

// Plays a sound effect when something happens (touch, jump, spawn, destroy).
class SoundOnEvent final : public Behaviour {
public:
    static constexpr u16 kTypeId = builtin::SoundOnEventId;
    u16 typeId() const override { return kTypeId; }
    const char* typeName() const override { return "SoundOnEvent"; }
    void start() override;
    void update() override;
    void onTouch(Actor& other) override;
    void onDestroy() override;
    SoundEvent event = SoundEvent::Touch;
    u16 sample = 0;
    fixed volume = fixed(1);

private:
    bool wasGround_ = true;
};

namespace physics {
// Moves a box through the tile map on one axis; returns true when it was blocked.
bool moveX(Actor& a, fixed dx);
bool moveY(Actor& a, fixed dy, bool dropThroughOneWay = false);
bool boxTouches(const Box& box, data::Collision type);
}  // namespace physics

}  // namespace agb
