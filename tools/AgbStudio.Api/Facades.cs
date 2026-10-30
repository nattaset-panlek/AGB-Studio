namespace GBA;

/// <summary>Sound effects (Audio page): names are the sound file names without the extension.</summary>
[Cpp("agb::api::Sound")]
public static class Sound
{
    /// <summary>Plays a sound effect at full volume.</summary>
    /// <param name="name">Sound name, for example <c>"jump"</c>.</param>
    [Cpp("agb::api::Sound::Play({0})")]
    [ScriptOp("native:PlaySound")]
    public static void Play([Asset(AssetKind.Sound)] string name) { }

    /// <summary>Plays a sound effect at a volume from 0 to 1.</summary>
    /// <param name="name">Sound name, for example <c>"jump"</c>.</param>
    /// <param name="volume">Volume 0..1.</param>
    [Cpp("agb::api::Sound::Play({0}, {1})")]
    [ScriptOp("native:PlaySound")]
    public static void Play([Asset(AssetKind.Sound)] string name, @fixed volume) { }

    /// <summary>Volume of all sound effects, 0..1.</summary>
    /// <param name="volume">Volume 0..1.</param>
    [Cpp("agb::api::Sound::SetVolume({0})")]
    public static void SetVolume(@fixed volume) { }
}

/// <summary>Music modules (MOD / XM).</summary>
[Cpp("agb::api::Music")]
public static class Music
{
    /// <summary>Starts a music module (looping).</summary>
    /// <param name="name">Music name, for example <c>Songs.Help</c>.</param>
    [Cpp("agb::api::Music::Play({0})")]
    [ScriptOp("native:PlayMusic")]
    public static void Play([Asset(AssetKind.Music)] string name) { }

    /// <summary>Stops the music.</summary>
    [Cpp("agb::api::Music::Stop()")]
    [ScriptOp("native:StopMusic")]
    public static void Stop() { }

    /// <summary>Music volume 0..1.</summary>
    /// <param name="volume">Volume 0..1.</param>
    [Cpp("agb::api::Music::SetVolume({0})")]
    public static void SetVolume(@fixed volume) { }

    /// <summary>Ramps the music volume over a number of frames (stops at 0).</summary>
    /// <param name="to">Target volume 0..1.</param>
    /// <param name="frames">Duration in frames.</param>
    [Cpp("agb::api::Music::Fade({0}, {1})")]
    public static void Fade(@fixed to, int frames) { }

    /// <summary>True while music plays.</summary>
    [Cpp("agb::api::Music::Playing()")]
    public static bool Playing => false;
}

/// <summary>The scene camera.</summary>
[Cpp("agb::api::Camera")]
public static class Camera
{
    /// <summary>Left edge of the view in world pixels.</summary>
    [Cpp("agb::api::Camera::X()")]
    public static int X => 0;

    /// <summary>Top edge of the view in world pixels.</summary>
    [Cpp("agb::api::Camera::Y()")]
    public static int Y => 0;

    /// <summary>Moves the view (stops following).</summary>
    /// <param name="x">Left edge in world pixels.</param>
    /// <param name="y">Top edge in world pixels.</param>
    [Cpp("agb::api::Camera::SetPosition({0}, {1})")]
    public static void SetPosition(int x, int y) { }

    /// <summary>Follows an actor inside the scene's dead zone.</summary>
    /// <param name="actor">Actor to follow, or null to stop.</param>
    [Cpp("agb::api::Camera::Follow({0})")]
    public static void Follow(Actor actor) { }

    /// <summary>Shakes the view.</summary>
    /// <param name="amplitude">Pixels.</param>
    /// <param name="frames">Duration in frames.</param>
    [Cpp("agb::api::Camera::Shake({0}, {1})")]
    [ScriptOp("native:ShakeCamera")]
    public static void Shake(int amplitude, int frames) { }
}

/// <summary>Scenes: loading, pausing and finding actors.</summary>
[Cpp("agb::api::Scene")]
public static class Scene
{
    /// <summary>Loads a scene by name with a fade.</summary>
    /// <param name="name">Scene name, for example <c>"Forest_02"</c>.</param>
    [Cpp("agb::api::Scene::Load(static_cast<agb::u16>({0}))")]
    [ScriptOp("native:GoToScene")]
    public static void Load([Asset(AssetKind.Scene)] string name) { }

    /// <summary>Loads a scene by index (for example <c>Save.scene</c>).</summary>
    /// <param name="index">Scene index.</param>
    [Cpp("agb::api::Scene::Load(static_cast<agb::u16>({0}))")]
    [ScriptOp("native:GoToScene")]
    public static void Load(int index) { }

    /// <summary>Pauses actors and scripts of the scene.</summary>
    [Cpp("agb::api::Scene::Pause()")]
    public static void Pause() { }

    /// <summary>Resumes a paused scene.</summary>
    [Cpp("agb::api::Scene::Resume()")]
    public static void Resume() { }

    /// <summary>True while the scene is paused.</summary>
    [Cpp("agb::api::Scene::Paused()")]
    public static bool Paused => false;

    /// <summary>Index of the current scene.</summary>
    [Cpp("agb::api::Scene::Current()")]
    public static int Current => 0;

    /// <summary>True when the collision tile at this world pixel is solid (walls, water, trees).</summary>
    /// <param name="x">World x in pixels.</param>
    /// <param name="y">World y in pixels.</param>
    [Cpp("agb::scene::solidAt({0}, {1})")]
    public static bool SolidAt(int x, int y) => false;

    /// <summary>Changes one 16 px tile of a map layer while the game runs (puzzle boards, opened gates).</summary>
    /// <param name="layer">Layer index in the Tilemap Editor (0 = first).</param>
    /// <param name="x">Tile column.</param>
    /// <param name="y">Tile row.</param>
    /// <param name="tile">Tile index in the layer's tileset.</param>
    [Cpp("agb::scene::setTile({0}, {1}, {2}, static_cast<agb::u16>({3}))")]
    public static void SetTile(int layer, int x, int y, int tile) { }

    /// <summary>The tile index at a cell of a map layer (after any SetTile).</summary>
    /// <param name="layer">Layer index in the Tilemap Editor (0 = first).</param>
    /// <param name="x">Tile column.</param>
    /// <param name="y">Tile row.</param>
    [Cpp("agb::scene::tile({0}, {1}, {2})")]
    public static int GetTile(int layer, int x, int y) => 0;

    /// <summary>The actor with this Hierarchy name, or null.</summary>
    /// <param name="name">Actor name.</param>
    [Cpp("agb::api::Scene::Find({0})")]
    [ScriptOp("native:FindActor")]
    public static Actor Find([Asset(AssetKind.ActorName)] string name) => null;
}

/// <summary>Save slots (Project Settings · Save data). The fields of <c>Game</c> are what gets saved.</summary>
[Cpp("agb::api::Save")]
public static class Save
{
    /// <summary>Writes the game state to a slot; false when the cartridge write failed.</summary>
    /// <param name="slot">Slot index.</param>
    [Cpp("agb::api::Save::Write({0})")]
    [ScriptOp("native:SaveWrite")]
    public static bool Write(int slot) => false;

    /// <summary>Reads the game state from a slot; false when the slot is empty or damaged.</summary>
    /// <param name="slot">Slot index.</param>
    [Cpp("agb::api::Save::Read({0})")]
    [ScriptOp("native:SaveRead")]
    public static bool Read(int slot) => false;

    /// <summary>True when the slot holds a save.</summary>
    /// <param name="slot">Slot index.</param>
    [Cpp("agb::api::Save::Exists({0})")]
    public static bool Exists(int slot) => false;

    /// <summary>Erases a slot.</summary>
    /// <param name="slot">Slot index.</param>
    [Cpp("agb::api::Save::Erase({0})")]
    public static void Erase(int slot) { }

    /// <summary>Remembers the current scene and the player's position, then writes slot 0.</summary>
    [Cpp("agb::api::Save::Checkpoint()")]
    [ScriptOp("native:SaveCheckpoint")]
    public static void Checkpoint() { }

    /// <summary>Scene index stored by the last checkpoint or read.</summary>
    [Cpp("agb::api::Save::scene()")]
    public static int scene => 0;
}

/// <summary>Text and dialogue: localized strings shown in the dialogue box.</summary>
[Cpp("agb::script::natives")]
public static class Text
{
    /// <summary>Shows a localized string in the dialogue box without waiting.</summary>
    /// <param name="key">String key from Localization.</param>
    [Cpp("agb::script::natives::showText({0})")]
    [ScriptOp("native:ShowText")]
    public static void Show([Asset(AssetKind.Text)] string key) { }
}

/// <summary>Messages to the editor Console (removed from Release builds when "Strip debug log" is on).</summary>
[Cpp("agb::api::Debug")]
public static class Debug
{
    /// <summary>Writes a line to the Console. Interpolated strings with int, bool and fixed values are allowed.</summary>
    /// <param name="text">Text to write.</param>
    [Cpp("agb::api::Debug::Log({0})")]
    [ScriptOp("native:Log")]
    public static void Log(string text) { }
}

/// <summary>Random numbers from the gameplay stream (replays give the same numbers).</summary>
[Cpp("agb::api::Random")]
public static class Random
{
    /// <summary>A whole number from lo to hi, both included.</summary>
    /// <param name="lo">Lowest result.</param>
    /// <param name="hi">Highest result.</param>
    [Cpp("agb::api::Random::Range({0}, {1})")]
    [ScriptOp("native:Random")]
    public static int Range(int lo, int hi) => 0;
}
