#include "agb/behaviours.hpp"

#include "agb/audio.hpp"
#include "agb/game.hpp"
#include "agb/input.hpp"
#include "agb/script.hpp"

namespace agb {

namespace physics {

namespace {
bool blocksSide(data::Collision c) { return c == data::Collision::Solid; }
}  // namespace

bool boxTouches(const Box& b, data::Collision type) {
    for (int y = b.y; y < b.y + b.h; y += 8) {
        for (int x = b.x; x < b.x + b.w; x += 8)
            if (scene::collisionAt(x, y) == type) return true;
        if (scene::collisionAt(b.x + b.w - 1, y) == type) return true;
    }
    for (int x = b.x; x < b.x + b.w; x += 8)
        if (scene::collisionAt(x, b.y + b.h - 1) == type) return true;
    return scene::collisionAt(b.x + b.w - 1, b.y + b.h - 1) == type;
}

bool moveX(Actor& a, fixed dx) {
    if (dx.raw == 0) return false;
    const fixed old = a.pos.x;
    a.pos.x += dx;
    const Box b = a.worldBox();
    const int edge = dx.raw > 0 ? b.x + b.w - 1 : b.x;
    for (int y = b.y; y < b.y + b.h; y += 8) {
        const int yy = y;
        if (blocksSide(scene::collisionAt(edge, yy)) || blocksSide(scene::collisionAt(edge, b.y + b.h - 1))) {
            // Snap to the tile edge.
            const int tile = edge >> 4;
            const int target = dx.raw > 0 ? tile * 16 - b.w - a.collider.x : (tile + 1) * 16 - a.collider.x;
            a.pos.x = fixed(target);
            if ((dx.raw > 0 && a.pos.x > old + dx) || (dx.raw < 0 && a.pos.x < old + dx)) a.pos.x = old;
            return true;
        }
    }
    return false;
}

bool moveY(Actor& a, fixed dy, bool dropThrough) {
    if (dy.raw == 0) return false;
    const Box before = a.worldBox();
    a.pos.y += dy;
    const Box b = a.worldBox();
    if (dy.raw > 0) {
        const int bottom = b.y + b.h - 1;
        const int prevBottom = before.y + before.h - 1;
        for (int x = b.x; x < b.x + b.w + 7; x += 8) {
            const int xx = x < b.x + b.w ? x : b.x + b.w - 1;
            const data::Collision c = scene::collisionAt(xx, bottom);
            const bool topSurface = (prevBottom >> 4) < (bottom >> 4) || ((prevBottom & 15) == 15);
            if (c == data::Collision::Solid ||
                ((c == data::Collision::OneWay || c == data::Collision::Ladder) && !dropThrough && topSurface && (prevBottom >> 4) < (bottom >> 4))) {
                a.pos.y = fixed((bottom >> 4) * 16 - b.h - a.collider.y);
                return true;
            }
        }
    } else {
        const int top = b.y;
        for (int x = b.x; x < b.x + b.w + 7; x += 8) {
            const int xx = x < b.x + b.w ? x : b.x + b.w - 1;
            if (scene::collisionAt(xx, top) == data::Collision::Solid) {
                a.pos.y = fixed(((top >> 4) + 1) * 16 - a.collider.y);
                return true;
            }
        }
    }
    return false;
}

}  // namespace physics

// ---- PlatformerBody ---------------------------------------------------------------------------------

const char* PlatformerBody::fieldName(int i) const {
    static const char* const names[] = {"gravity", "jumpForce", "maxSpeed", "vx", "vy"};
    return i >= 0 && i < 5 ? names[i] : "";
}

void* PlatformerBody::fieldPtr(int i) {
    switch (i) {
        case 0: return &gravity;
        case 1: return &jumpForce;
        case 2: return &maxSpeed;
        case 3: return &vx;
        case 4: return &vy;
        default: return nullptr;
    }
}

void PlatformerBody::lateUpdate() {
    Actor& a = *actor;
    const Box box = a.worldBox();
    onLadder = physics::boxTouches(box, data::Collision::Ladder);
    inWater = physics::boxTouches(box, data::Collision::Water);
    touchingHazard = physics::boxTouches(box, data::Collision::Hazard);
    if (!onLadder) climbing = false;
    vx = clamp(vx, -maxSpeed, maxSpeed);
    if (!climbing) {
        vy += inWater ? gravity / 2 : gravity;
        const fixed fall = inWater ? maxFall / 2 : maxFall;
        if (vy > fall) vy = fall;
    }
    hitWall = physics::moveX(a, vx);
    if (hitWall) vx = fixed();
    const bool wasGround = onGround;
    onGround = false;
    hitCeiling = false;
    if (physics::moveY(a, vy)) {
        if (vy.raw > 0) onGround = true;
        else hitCeiling = true;
        vy = fixed();
    } else if (vy.raw >= 0 && !climbing) {
        // Standing still on the ground: probe one pixel below.
        const Box b = a.worldBox();
        for (int x = b.x; x < b.x + b.w + 7 && !onGround; x += 8) {
            const int xx = x < b.x + b.w ? x : b.x + b.w - 1;
            const data::Collision c = scene::collisionAt(xx, b.y + b.h);
            onGround = c == data::Collision::Solid || ((c == data::Collision::OneWay || c == data::Collision::Ladder) && ((b.y + b.h) & 15) == 0);
        }
    }
    framesSinceGround = onGround ? 0 : static_cast<u16>(framesSinceGround + (framesSinceGround < 0xFFFF ? 1 : 0));
    AGB_UNUSED(wasGround);
    if (touchingHazard)
        if (Health* h = a.get<Health>()) h->damage(1);
    a.vel = {vx, vy};
    a.onGround = onGround;
    if (vx.raw) a.setFacingLeft(vx.raw < 0);
}

// ---- TopDownMover -----------------------------------------------------------------------------------

void TopDownMover::lateUpdate() {
    Actor& a = *actor;
    if (playerControlled && !script::cutsceneRunning()) {
        const int ix = input::axisX(), iy = input::axisY();
        // Diagonals at 0.7 so 8-way movement is not faster on diagonals.
        const fixed s = (ix && iy) ? speed * fixed::lit(0.7071) : speed;
        vx = s * ix;
        vy = s * iy;
        if (iy < 0) facing = 0;
        else if (iy > 0) facing = 2;
        if (ix > 0) facing = 1;
        else if (ix < 0) facing = 3;
    }
    if (physics::moveX(a, vx)) vx = fixed();
    if (physics::moveY(a, vy)) vy = fixed();
    a.vel = {vx, vy};
    if (facing == 1 || facing == 3) a.setFacingLeft(facing == 3);
}

// ---- Projectile -------------------------------------------------------------------------------------

void Projectile::lateUpdate() {
    Actor& a = *actor;
    a.pos += fixed2{vx, vy};
    a.vel = {vx, vy};
    const Box b = a.worldBox();
    if (destroyOnWall && physics::boxTouches(b, data::Collision::Solid)) a.destroy();
    if (lifetime && --lifetime == 0) a.destroy();
    const int cx = scene::cameraX(), cy = scene::cameraY();
    if (b.x > cx + 300 || b.x + b.w < cx - 60 || b.y > cy + 220 || b.y + b.h < cy - 60) a.destroy();
}

void Projectile::onTouch(Actor& other) {
    const bool isPlayer = other.kind == ActorKind::Player;
    if (hitsPlayer != isPlayer) return;
    if (other.is<Projectile>()) return;
    if (Health* h = other.get<Health>()) h->damage(damage);
    actor->destroy();
}

// ---- Health -----------------------------------------------------------------------------------------

bool Health::damage(int amount) {
    if (dead || invincibleLeft) return false;
    hearts -= amount;
    invincibleLeft = invincibleFrames;
    if (onDamaged) onDamaged(*actor);
    if (hearts <= 0) {
        hearts = 0;
        dead = true;
        if (onDeath) onDeath(*actor);
        if (destroyOnDeath) actor->destroy();
    }
    return true;
}

void Health::heal(int amount) {
    hearts = hearts + amount > maxHearts ? maxHearts : hearts + amount;
}

void Health::update() {
    if (!invincibleLeft) return;
    --invincibleLeft;
    // Blink while invincible.
    actor->setVisible(invincibleLeft == 0 || (invincibleLeft & 4) == 0);
}

// ---- DamageOnTouch ----------------------------------------------------------------------------------

void DamageOnTouch::onTouch(Actor& other) {
    if (onlyPlayer && other.kind != ActorKind::Player) return;
    if (Health* h = other.get<Health>()) h->damage(damage);
}

// ---- Patrol -----------------------------------------------------------------------------------------

void Patrol::start() { origin_ = actor->pos.x; }

void Patrol::update() {
    Actor& a = *actor;
    PlatformerBody* body = a.get<PlatformerBody>();
    const Box b = a.worldBox();
    bool turn = false;
    if (body && body->hitWall) turn = true;
    if (turnAtLedges && (!body || body->onGround)) {
        const int probeX = dir > 0 ? b.x + b.w : b.x - 1;
        const data::Collision below = scene::collisionAt(probeX, b.y + b.h + 1);
        if (below == data::Collision::None || below == data::Collision::Hazard || below == data::Collision::Water) turn = true;
    }
    if (distance && abs(a.pos.x - origin_) >= fixed(distance)) {
        // Turn only when moving away from the origin.
        if ((a.pos.x > origin_ && dir > 0) || (a.pos.x < origin_ && dir < 0)) turn = true;
    }
    if (turn) dir = static_cast<s8>(-dir);
    if (body) body->vx = speed * dir;
    else if (physics::moveX(a, speed * dir)) dir = static_cast<s8>(-dir);
    a.setFacingLeft(dir < 0);
}

// ---- ChasePlayer ------------------------------------------------------------------------------------

void ChasePlayer::update() {
    Actor* player = nullptr;
    actors::forEach([&](Actor& o) {
        if (!player && o.kind == ActorKind::Player && o.isAlive()) player = &o;
    });
    Actor& a = *actor;
    if (!player) {
        chasing = false;
        return;
    }
    const fixed dx = player->pos.x - a.pos.x, dy = player->pos.y - a.pos.y;
    chasing = abs(dx) < fixed(range) && abs(dy) < fixed(range);
    PlatformerBody* body = a.get<PlatformerBody>();
    TopDownMover* mover = a.get<TopDownMover>();
    if (Patrol* patrol = a.get<Patrol>()) patrol->enabled = !chasing;
    if (!chasing) {
        if (mover) mover->vx = mover->vy = fixed();
        return;
    }
    const int sx = dx.raw > 0 ? 1 : dx.raw < 0 ? -1 : 0, sy = dy.raw > 0 ? 1 : dy.raw < 0 ? -1 : 0;
    if (body) body->vx = speed * sx;
    else if (mover) {
        mover->playerControlled = false;
        mover->vx = speed * sx;
        mover->vy = speed * sy;
    } else {
        physics::moveX(a, speed * sx);
        physics::moveY(a, speed * sy);
    }
    a.setFacingLeft(sx < 0);
}

// ---- SoundOnEvent -----------------------------------------------------------------------------------

void SoundOnEvent::start() {
    if (event == SoundEvent::Spawn) audio::playSample(sample, volume);
}

void SoundOnEvent::update() {
    if (event != SoundEvent::Jump) return;
    PlatformerBody* body = actor->get<PlatformerBody>();
    if (!body) return;
    if (wasGround_ && !body->onGround && body->vy.raw < 0) audio::playSample(sample, volume);
    wasGround_ = body->onGround;
}

void SoundOnEvent::onTouch(Actor& other) {
    AGB_UNUSED(other);
    if (event == SoundEvent::Touch) audio::playSample(sample, volume);
}

void SoundOnEvent::onDestroy() {
    if (event == SoundEvent::Destroy) audio::playSample(sample, volume);
}

}  // namespace agb
