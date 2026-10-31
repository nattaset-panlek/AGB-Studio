namespace GBA;

/// <summary>Collision of a map tile, set per tile in the Tileset Editor.</summary>
[Cpp("agb::data::Collision")]
public enum Collision
{
    /// <summary>Nothing: actors pass.</summary>
    None,
    /// <summary>A wall or floor.</summary>
    Solid,
    /// <summary>A floor that can be jumped through from below.</summary>
    OneWay,
    /// <summary>A ladder.</summary>
    Ladder,
    /// <summary>Hurts the player.</summary>
    Hazard,
    /// <summary>Water (slower movement).</summary>
    Water,
    /// <summary>A slope rising to the right.</summary>
    SlopeUp,
    /// <summary>A slope falling to the right.</summary>
    SlopeDown,
}
