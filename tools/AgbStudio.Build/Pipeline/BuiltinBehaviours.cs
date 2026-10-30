using System.Globalization;

namespace AgbStudio.Build.Pipeline;

/// <summary>The Component Picker built-ins (runtime/include/agb/behaviours.hpp) and their Inspector fields.</summary>
public static class BuiltinBehaviours
{
    public enum Kind { Fixed, Int, Bool, U16, SoundEvent, Sample }

    public sealed record Field(string Name, string CppMember, Kind Kind, string Default);

    public sealed record Behaviour(string Name, string CppClass, string Category, string Description, IReadOnlyList<Field> Fields);

    public static readonly IReadOnlyList<Behaviour> All =
    [
        new("PlatformerBody", "agb::PlatformerBody", "Physics", "gravity, jump, slopes",
        [
            new("gravity", "gravity", Kind.Fixed, "0.25"), new("jumpForce", "jumpForce", Kind.Fixed, "3.5"),
            new("maxSpeed", "maxSpeed", Kind.Fixed, "1.5"), new("maxFall", "maxFall", Kind.Fixed, "4"),
        ]),
        new("TopDownMover", "agb::TopDownMover", "Physics", "8-way movement",
            [new("speed", "speed", Kind.Fixed, "1"), new("playerControlled", "playerControlled", Kind.Bool, "true")]),
        new("Projectile", "agb::Projectile", "Physics", "moves and hits",
        [
            new("vx", "vx", Kind.Fixed, "2"), new("vy", "vy", Kind.Fixed, "0"), new("lifetime", "lifetime", Kind.U16, "120"),
            new("damage", "damage", Kind.Int, "1"), new("hitsPlayer", "hitsPlayer", Kind.Bool, "false"),
        ]),
        new("Health", "agb::Health", "Combat", "hearts, invincibility",
        [
            new("hearts", "hearts", Kind.Int, "3"), new("maxHearts", "maxHearts", Kind.Int, "3"),
            new("invincibleFrames", "invincibleFrames", Kind.U16, "60"), new("destroyOnDeath", "destroyOnDeath", Kind.Bool, "true"),
        ]),
        new("DamageOnTouch", "agb::DamageOnTouch", "Combat", "hurts the player",
            [new("damage", "damage", Kind.Int, "1"), new("onlyPlayer", "onlyPlayer", Kind.Bool, "true")]),
        new("Patrol", "agb::Patrol", "AI", "walk between points",
        [
            new("speed", "speed", Kind.Fixed, "0.5"), new("distance", "distance", Kind.U16, "0"),
            new("turnAtLedges", "turnAtLedges", Kind.Bool, "true"),
        ]),
        new("ChasePlayer", "agb::ChasePlayer", "AI", "follow when close",
            [new("range", "range", Kind.U16, "96"), new("speed", "speed", Kind.Fixed, "0.75")]),
        new("SoundOnEvent", "agb::SoundOnEvent", "Audio", "play SFX on touch/jump",
        [
            new("event", "event", Kind.SoundEvent, "Touch"), new("sound", "sample", Kind.Sample, ""),
            new("volume", "volume", Kind.Fixed, "1"),
        ]),
    ];

    public static Behaviour? Find(string name) => All.FirstOrDefault(b => b.Name == name);

    /// <summary>C++ assignment value for an Inspector field string.</summary>
    public static string Value(Field f, string text, Func<string, int>? soundId = null)
    {
        text = text.Trim();
        switch (f.Kind)
        {
            case Kind.Fixed:
                return Cpp.Fixed(double.Parse(text, CultureInfo.InvariantCulture));
            case Kind.Int:
            case Kind.U16:
                return ((int)double.Parse(text, CultureInfo.InvariantCulture)).ToString(CultureInfo.InvariantCulture);
            case Kind.Bool:
                return text.Equals("true", StringComparison.OrdinalIgnoreCase) ? "true" : "false";
            case Kind.SoundEvent:
                return "agb::SoundEvent::" + text;
            case Kind.Sample:
                return (soundId?.Invoke(text) ?? 0).ToString(CultureInfo.InvariantCulture);
            default:
                return text;
        }
    }
}
