using System.Text.Json.Serialization;

namespace AgbStudio.Core.Project;

// scenes/<Name>.scene (Scene Editor, Scene Settings, World Map, Link Settings)
public sealed class SceneFile
{
    public int Format { get; set; } = 1;
    public string Name { get; set; } = "";
    public int Width { get; set; } = 30;    // 16 px tiles
    public int Height { get; set; } = 20;
    public int VideoMode { get; set; }
    public string Map { get; set; } = "";   // maps/<Name>.map
    public CameraSettings Camera { get; set; } = new();
    public MusicSettings Music { get; set; } = new();
    public SceneEvents Events { get; set; } = new();
    public List<ActorData> Actors { get; set; } = new();
    public List<LinkData> Links { get; set; } = new();
    public bool AlphaBlending { get; set; }
    public bool Mosaic { get; set; }
    public string Backdrop { get; set; } = "#000000";
    // World Map card position and the template it started from.
    public int MapX { get; set; }
    public int MapY { get; set; }
    public string Template { get; set; } = "";
}

public sealed class CameraSettings
{
    public string Follow { get; set; } = "Player";
    public int DeadZoneW { get; set; } = 32;
    public int DeadZoneH { get; set; } = 24;
    public bool ClampToMap { get; set; } = true;
}

public sealed class MusicSettings
{
    public string Track { get; set; } = "";
    public double Volume { get; set; } = 0.8;
    public bool FadeOnSceneChange { get; set; } = true;
}

public sealed class SceneEvents
{
    public string OnEnter { get; set; } = "";  // C# statements, e.g. FadeIn(0.5s)
    public string OnExit { get; set; } = "";   // e.g. Save.Checkpoint()
    public string OnLoad { get; set; } = "";
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ActorKind { Empty, Player, Enemy, Pickup, TriggerZone, CameraZone, TextLabel }

public sealed class ActorData
{
    public string Name { get; set; } = "Actor";
    public ActorKind Kind { get; set; }
    public string Sprite { get; set; } = "";      // sprite asset name
    public string Animation { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public string Tag { get; set; } = "";
    public int Priority { get; set; } = 1;          // OBJ priority (Inspector "Layer OBJ · Prio 1")
    public bool Hidden { get; set; }
    public string Prefab { get; set; } = "";
    public string Group { get; set; } = "";         // Hierarchy folder (Enemies, Coins ...)
    public ColliderData? Collider { get; set; }     // null = from the sprite
    public int ZoneW { get; set; } = 16;            // Trigger / Camera zones
    public int ZoneH { get; set; } = 16;
    public string Text { get; set; } = "";          // Text Label: string key
    public List<ComponentData> Components { get; set; } = new();
}

public sealed class ColliderData
{
    public int X { get; set; }
    public int Y { get; set; }
    public int W { get; set; }
    public int H { get; set; }
}

// A behaviour on an actor: a built-in (PlatformerBody, Health ...) or a script class name, with the
// Inspector field values as strings ("1.5", "true", "3").
public sealed class ComponentData
{
    public string Type { get; set; } = "";
    public Dictionary<string, string> Fields { get; set; } = new();
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LinkKind { OnTouch, Door, Respawn }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Transition { Cut, Fade, Slide }

// Link Settings popover: From, To scene, Spawn at, Transition, Duration, Only if.
public sealed class LinkData
{
    public string From { get; set; } = "";          // actor name in this scene
    public LinkKind Kind { get; set; }
    public string To { get; set; } = "";            // scene name
    public string SpawnAt { get; set; } = "Start";  // spawn point name or "x,y"
    public int SpawnX { get; set; } = -1;
    public int SpawnY { get; set; } = -1;
    public Transition Transition { get; set; } = Transition.Fade;
    public double Duration { get; set; } = 0.5;     // seconds
    public string OnlyIf { get; set; } = "";        // C# condition, empty = always
}

// maps/<Name>.map (Tilemap Editor)
public sealed class MapFile
{
    public int Format { get; set; } = 1;
    public string Name { get; set; } = "";
    public string Tileset { get; set; } = "";       // tileset asset name
    public int Width { get; set; } = 30;            // 16 px tiles
    public int Height { get; set; } = 20;
    public List<MapLayerData> Layers { get; set; } = new();
    public int CollisionLayer { get; set; } = -1;   // index into Layers
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LayerType { Tile, Image, Affine, CollisionOnly }

public sealed class MapLayerData
{
    public string Name { get; set; } = "";
    public int Bg { get; set; }
    public LayerType Type { get; set; }
    public int Priority { get; set; }
    public double ParallaxX { get; set; } = 1;
    public double ParallaxY { get; set; } = 1;
    public bool Wrap { get; set; }
    public string Image { get; set; } = "";         // Image layers: png asset
    public List<int> Cells { get; set; } = new();   // metatile per cell, row-major (Width x Height)
}
