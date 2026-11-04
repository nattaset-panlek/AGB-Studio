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

    /// <summary>
    /// The game's music volume 0..1 (a scene starts its track at its own music Volume). The player's Options music volume
    /// and the track's volume multiply it; this does not change the player's setting.
    /// </summary>
    /// <param name="volume">Volume 0..1.</param>
    [Cpp("agb::api::Music::SetVolume({0})")]
    public static void SetVolume(@fixed volume) { }

    /// <summary>Ramps the music's fade level over a number of frames (stops at 0); the next Play starts at full level.</summary>
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

/// <summary>Draws on the picture of a scene in a bitmap video mode: Mode 3 (240 x 160), Mode 4 (240 x 160, palette) or
/// Mode 5 (160 x 128). A colour is <see cref="Rgb"/> in Modes 3 and 5 and a palette index in Mode 4. In Modes 4 and 5
/// drawing goes to a hidden page, and <see cref="Flip"/> shows it. Drawing happens in VBlank, in order. In Modes 0, 1 and
/// 2 these calls do nothing.</summary>
[Cpp("agb::api::Bitmap")]
public static class Bitmap
{
    /// <summary>Fills the whole page with a colour.</summary>
    /// <param name="color">The colour.</param>
    [Cpp("agb::api::Bitmap::Clear({0})")]
    [ScriptOp("native:BitmapClear")]
    public static void Clear(int color) { }

    /// <summary>Sets one pixel. Up to 512 pixels a frame: draw larger areas with <see cref="FillRect"/>.</summary>
    /// <param name="x">Left to right, from 0.</param>
    /// <param name="y">Top to bottom, from 0.</param>
    /// <param name="color">The colour.</param>
    [Cpp("agb::api::Bitmap::SetPixel({0}, {1}, {2})")]
    [ScriptOp("native:BitmapSetPixel")]
    public static void SetPixel(int x, int y, int color) { }

    /// <summary>Draws a filled rectangle, clipped to the picture.</summary>
    /// <param name="x">Left edge.</param>
    /// <param name="y">Top edge.</param>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <param name="color">The colour.</param>
    [Cpp("agb::api::Bitmap::FillRect({0}, {1}, {2}, {3}, {4})")]
    [ScriptOp("native:BitmapFillRect")]
    public static void FillRect(int x, int y, int width, int height, int color) { }

    /// <summary>Draws a picture from assets/images over the page: 240 x 160 in Modes 3 and 4, 160 x 128 in Mode 5. It takes
    /// a few frames. In Mode 4 its colours replace the palette at once, so show it under a fade or use pictures that share
    /// colours.</summary>
    /// <param name="name">Image name without the extension, for example <c>"title"</c>.</param>
    [Cpp("agb::api::Bitmap::Show({0})")]
    [ScriptOp("native:BitmapShow")]
    public static void Show([Asset(AssetKind.Picture)] string name) { }

    /// <summary>Modes 4 and 5: shows the page just drawn, once everything drawn so far is on it. Drawing then goes to the
    /// other page. Nothing happens in Mode 3, which has one page.</summary>
    [Cpp("agb::api::Bitmap::Flip()")]
    [ScriptOp("native:BitmapFlip")]
    public static void Flip() { }

    /// <summary>A colour for Modes 3 and 5, and for <see cref="SetPaletteColor"/>.</summary>
    /// <param name="r">Red, 0 to 31.</param>
    /// <param name="g">Green, 0 to 31.</param>
    /// <param name="b">Blue, 0 to 31.</param>
    /// <returns>The 15-bit colour.</returns>
    [Cpp("agb::api::Bitmap::Rgb({0}, {1}, {2})")]
    [ScriptOp("native:BitmapRgb")]
    public static int Rgb(int r, int g, int b) => 0;

    /// <summary>Mode 4: sets a colour of the palette. Index 0 is see-through: the backdrop shows there.</summary>
    /// <param name="index">Palette index, 1 to 255.</param>
    /// <param name="color">The colour, from <see cref="Rgb"/>.</param>
    [Cpp("agb::api::Bitmap::SetPaletteColor({0}, {1})")]
    [ScriptOp("native:BitmapSetPaletteColor")]
    public static void SetPaletteColor(int index, int color) { }
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

    /// <summary>Loads a scene by index and puts the player at a world position (the way back from a battle).</summary>
    /// <param name="index">Scene index.</param>
    /// <param name="x">World x in pixels.</param>
    /// <param name="y">World y in pixels.</param>
    [Cpp("agb::api::Scene::LoadAt(static_cast<agb::u16>({0}), {1}, {2})")]
    public static void LoadAt(int index, int x, int y) { }

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

    /// <summary>The collision of the tile at this world pixel (Tileset Editor "Collision"): <c>Collision.Water</c>.</summary>
    /// <param name="x">World x in pixels.</param>
    /// <param name="y">World y in pixels.</param>
    [Cpp("agb::scene::collisionAt({0}, {1})")]
    public static Collision CollisionAt(int x, int y) => Collision.None;

    /// <summary>The tag of the tile at this world pixel (Tileset Editor "Tag"); compare it with a string:
    /// <c>Scene.TagAt(x, y) == "Grass"</c>.</summary>
    /// <param name="x">World x in pixels.</param>
    /// <param name="y">World y in pixels.</param>
    [Cpp("agb::scene::tagAt({0}, {1})")]
    [Asset(AssetKind.Tag)]
    public static string TagAt(int x, int y) => null;

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

    /// <summary>Makes a copy of an actor of this scene at a position and returns it (null when all actor slots are in
    /// use). Bullets, enemies and items are usually hidden "template" actors that only exist to be copied.</summary>
    /// <param name="name">Hierarchy name of the actor to copy.</param>
    /// <param name="x">World x in pixels.</param>
    /// <param name="y">World y in pixels.</param>
    [Cpp("agb::scene::spawn({0}, {1}, {2})")]
    public static Actor Spawn([Asset(AssetKind.ActorName)] string name, int x, int y) => null;

    /// <summary>Shows the scene's rotate / scale layer as a floor seen from a camera (Mode 7, the Racing template).
    /// Call it every frame; the floor stays as it is while the game is paused.</summary>
    /// <param name="x">Camera x on the layer in pixels.</param>
    /// <param name="y">Camera y on the layer in pixels.</param>
    /// <param name="angle">Heading in 1/256 turns: 0 looks along +x, 64 along +y.</param>
    /// <param name="height">Camera height above the floor in pixels.</param>
    /// <param name="horizon">Screen line of the horizon; lines above it show the layers behind.</param>
    [Cpp("agb::scene::mode7({0}, {1}, {2}, {3}, {4})")]
    public static void Mode7(@fixed x, @fixed y, int angle, int height, int horizon) { }

    /// <summary>Stops the Mode 7 floor (the layer shows flat again).</summary>
    [Cpp("agb::scene::stopMode7()")]
    public static void StopMode7() { }

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

    /// <summary>Remembers the current scene and the player's position, then writes a slot.</summary>
    /// <param name="slot">Slot index.</param>
    [Cpp("agb::api::Save::Checkpoint({0})")]
    public static void Checkpoint(int slot) { }

    /// <summary>Reads a slot and goes to the scene and player position it holds; false when it is empty or damaged.</summary>
    /// <param name="slot">Slot index.</param>
    [Cpp("agb::api::Save::Continue({0})")]
    public static bool Continue(int slot) => false;

    /// <summary>Shows a slot in the Game fields without losing the game in progress, for a save list. Call EndPreview in the
    /// same frame. False when the slot is empty or damaged.</summary>
    /// <param name="slot">Slot index.</param>
    [Cpp("agb::api::Save::BeginPreview({0})")]
    public static bool BeginPreview(int slot) => false;

    /// <summary>Puts back the game in progress after BeginPreview.</summary>
    [Cpp("agb::api::Save::EndPreview()")]
    public static void EndPreview() { }

    /// <summary>Sets every Game field and saved flag back to zero for a new game. Nothing is written to the cartridge.</summary>
    [Cpp("agb::api::Save::NewGame()")]
    public static void NewGame() { }

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

    /// <summary>Typing speed of the dialogue box in characters a frame (a Text Speed option); 0 shows a line at once.</summary>
    /// <param name="charsPerFrame">Characters a frame.</param>
    [Cpp("agb::api::Text::SetSpeed({0})")]
    public static void SetSpeed(int charsPerFrame) { }

    /// <summary>Sets the value of {0}..{3} in strings to a number.</summary>
    /// <param name="index">0 to 3.</param>
    /// <param name="value">The number.</param>
    /// <param name="digits">Pads with zeros to this many digits (0 = no padding).</param>
    [Cpp("agb::api::Text::SetVar({0}, {1}, {2})")]
    public static void SetVar(int index, int value, int digits = 0) { }

    /// <summary>Sets the value of {0}..{3} in strings to the text of a string key.</summary>
    [Cpp("agb::api::Text::SetVarText({0}, {1})")]
    public static void SetVarText(int index, [Asset(AssetKind.Text)] string key) { }

    /// <summary>Sets the value of {0}..{3} in strings to a string id from a database row.</summary>
    [Cpp("agb::api::Text::SetVarText({0}, {1})")]
    public static void SetVarText(int index, int textId) { }

    /// <summary>Sets one character of a typed value of {0}..{3} (a player name); 0 ends the text there.</summary>
    [Cpp("agb::api::Text::SetVarChar({0}, {1}, {2})")]
    public static void SetVarChar(int index, int position, int ch) { }
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

    /// <summary>When the condition is false, writes the message with this file and line to the Console and stops the
    /// game. Debug builds only, and Release builds with Project Settings · Build "Keep asserts"; otherwise it does
    /// nothing and costs nothing.</summary>
    /// <param name="condition">What must be true here.</param>
    /// <param name="message">What went wrong, for the Console.</param>
    [Cpp("AGB_ASSERT({0}, {1})")]
    public static void Assert(bool condition, string message) { }
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
