namespace GBA;

/// <summary>Moves with gravity and collides with Solid, One-way, Ladder, Water and Hazard tiles (Component Picker).</summary>
[Cpp("agb::PlatformerBody")]
public sealed class PlatformerBody : Behaviour
{
    internal PlatformerBody() { }

    /// <summary>Horizontal speed in pixels per frame.</summary>
    public @fixed vx;

    /// <summary>Vertical speed in pixels per frame (negative = up).</summary>
    public @fixed vy;

    /// <summary>Added to <see cref="vy"/> every frame.</summary>
    public @fixed gravity;

    /// <summary>Upward speed given by <see cref="Jump"/>.</summary>
    public @fixed jumpForce;

    /// <summary>Largest horizontal speed.</summary>
    public @fixed maxSpeed;

    /// <summary>Largest falling speed.</summary>
    public @fixed maxFall;

    /// <summary>True while standing on solid ground or a one-way platform.</summary>
    [Cpp("{this.}onGround")]
    public bool OnGround => false;

    /// <summary>True while overlapping a ladder tile.</summary>
    [Cpp("{this.}onLadder")]
    public bool OnLadder => false;

    /// <summary>True while inside water tiles.</summary>
    [Cpp("{this.}inWater")]
    public bool InWater => false;

    /// <summary>True while touching hazard tiles.</summary>
    [Cpp("{this.}touchingHazard")]
    public bool TouchingHazard => false;

    /// <summary>True on frames the body was stopped by a wall.</summary>
    [Cpp("{this.}hitWall")]
    public bool HitWall => false;

    /// <summary>True on frames the body hit a ceiling.</summary>
    [Cpp("{this.}hitCeiling")]
    public bool HitCeiling => false;

    /// <summary>Frames since the body last stood on the ground (for coyote time).</summary>
    [Cpp("{this.}framesSinceGround")]
    public int FramesSinceGround => 0;

    /// <summary>Set while climbing a ladder: gravity is off.</summary>
    public bool climbing;

    /// <summary>Jumps with <see cref="jumpForce"/>.</summary>
    [Cpp("{this.}jump()")]
    public void Jump() { }
}

/// <summary>8-way movement with the D-pad, or from a script when <see cref="playerControlled"/> is false.</summary>
[Cpp("agb::TopDownMover")]
public sealed class TopDownMover : Behaviour
{
    internal TopDownMover() { }

    /// <summary>Pixels per frame.</summary>
    public @fixed speed;

    /// <summary>When true the D-pad moves the actor.</summary>
    public bool playerControlled;

    /// <summary>Horizontal speed when moved by a script.</summary>
    public @fixed vx;

    /// <summary>Vertical speed when moved by a script.</summary>
    public @fixed vy;

    /// <summary>0 up, 1 right, 2 down, 3 left.</summary>
    [Cpp("{this.}facing")]
    public int Facing => 0;
}

/// <summary>Moves in a straight line and hits things (bullets, thrown items).</summary>
[Cpp("agb::Projectile")]
public sealed class Projectile : Behaviour
{
    internal Projectile() { }

    /// <summary>Horizontal speed.</summary>
    public @fixed vx;

    /// <summary>Vertical speed.</summary>
    public @fixed vy;

    /// <summary>Frames before the projectile disappears.</summary>
    public int lifetime;

    /// <summary>Hearts taken from what it hits.</summary>
    public int damage;

    /// <summary>Enemy bullets hit the player; player bullets hit everything else.</summary>
    public bool hitsPlayer;

    /// <summary>Disappears when it hits a wall.</summary>
    public bool destroyOnWall;
}

/// <summary>Hearts and invincibility frames.</summary>
[Cpp("agb::Health")]
public sealed class Health : Behaviour
{
    internal Health() { }

    /// <summary>Current hearts.</summary>
    public int hearts;

    /// <summary>Most hearts.</summary>
    public int maxHearts;

    /// <summary>Invincible frames after taking damage.</summary>
    public int invincibleFrames;

    /// <summary>Invincible frames left.</summary>
    public int invincibleLeft;

    /// <summary>Destroy the actor when hearts reach 0.</summary>
    public bool destroyOnDeath;

    /// <summary>True once hearts reached 0.</summary>
    public bool dead;

    /// <summary>Takes damage; false while invincible.</summary>
    /// <param name="amount">Hearts to take.</param>
    [Cpp("{this.}damage({0})")]
    public bool Damage(int amount) => false;

    /// <summary>Gives hearts back, up to <see cref="maxHearts"/>.</summary>
    /// <param name="amount">Hearts to give.</param>
    [Cpp("{this.}heal({0})")]
    public void Heal(int amount) { }
}

/// <summary>Hurts what it touches (the player by default).</summary>
[Cpp("agb::DamageOnTouch")]
public sealed class DamageOnTouch : Behaviour
{
    internal DamageOnTouch() { }

    /// <summary>Hearts taken.</summary>
    public int damage;

    /// <summary>Only hurt the player.</summary>
    public bool onlyPlayer;
}

/// <summary>Walks back and forth; turns at walls, at ledges or after a distance.</summary>
[Cpp("agb::Patrol")]
public sealed class Patrol : Behaviour
{
    internal Patrol() { }

    /// <summary>Pixels per frame.</summary>
    public @fixed speed;

    /// <summary>Pixels before turning (0 = until a wall or ledge).</summary>
    public int distance;

    /// <summary>Turn around at the edge of a platform.</summary>
    public bool turnAtLedges;

    /// <summary>Direction: -1 left, 1 right.</summary>
    public int dir;
}

/// <summary>Follows the player within a range.</summary>
[Cpp("agb::ChasePlayer")]
public sealed class ChasePlayer : Behaviour
{
    internal ChasePlayer() { }

    /// <summary>Pixels within which the chase starts.</summary>
    public int range;

    /// <summary>Pixels per frame.</summary>
    public @fixed speed;

    /// <summary>True while chasing.</summary>
    public bool chasing;
}

/// <summary>Plays a sound effect on touch, jump, spawn or destroy.</summary>
[Cpp("agb::SoundOnEvent")]
public sealed class SoundOnEvent : Behaviour
{
    internal SoundOnEvent() { }

    /// <summary>Volume 0..1.</summary>
    public @fixed volume;
}
