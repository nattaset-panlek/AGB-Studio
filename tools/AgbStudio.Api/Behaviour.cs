using System;
using System.Threading.Tasks;

namespace GBA;

/// <summary>
/// Members shared by <see cref="Behaviour"/> and <see cref="SceneScript"/>: the cutscene helpers used with
/// <c>await</c> inside <c>[Cutscene] async Task</c> methods, which compile to Event Script bytecode.
/// </summary>
public abstract class ScriptBase
{
    /// <summary>The player actor (the first actor of kind Player in the scene).</summary>
    [Cpp("agb::script::natives::player()")]
    [ScriptOp("actor:player")]
    protected static Actor Player => null;

    /// <summary>Shows a line of dialogue and waits until the player closes it.</summary>
    /// <param name="key">String key from Localization.</param>
    /// <param name="speaker">Speaker name, or null for none.</param>
    /// <param name="portrait">Portrait name, or null for the speaker's default.</param>
    /// <param name="speed">Characters per frame, or -1 for the project default.</param>
    /// <param name="blip">Blip sound name, or null for the speaker's default.</param>
    [ScriptOp("native:Say")]
    protected static Task Say([Asset(AssetKind.Text)] string key, [Asset(AssetKind.Speaker)] string speaker = null,
        [Asset(AssetKind.Portrait)] string portrait = null, int speed = -1, [Asset(AssetKind.Sound)] string blip = null) => Task.CompletedTask;

    /// <summary>Shows a choice menu and waits; the result is the index of the picked option.</summary>
    /// <param name="options">String keys of the options (null hides an option).</param>
    [ScriptOp("native:Choice")]
    protected static Task<int> Choice([Asset(AssetKind.Text)] params string[] options) => Task.FromResult(0);

    /// <summary>Waits a number of frames (60 per second).</summary>
    /// <param name="frames">Frames to wait.</param>
    [ScriptOp("op:WaitFrames")]
    protected static Task Wait(int frames) => Task.CompletedTask;

    /// <summary>Moves an actor along a path and waits until it arrives.</summary>
    /// <param name="actor">Actor to move.</param>
    /// <param name="path">Path name (see the generated <c>Path</c> class).</param>
    [ScriptOp("native:Move")]
    protected static Task Move(Actor actor, [Asset(AssetKind.Path)] string path) => Task.CompletedTask;

    /// <summary>Fades the screen in from black over a time in seconds.</summary>
    /// <param name="seconds">Duration in seconds.</param>
    [Cpp("agb::script::natives::fadeIn({0})")]
    [ScriptOp("native:FadeIn")]
    protected static Task FadeIn(@fixed seconds) => Task.CompletedTask;

    /// <summary>Fades the screen out to black over a time in seconds.</summary>
    /// <param name="seconds">Duration in seconds.</param>
    [Cpp("agb::script::natives::fadeOut({0})")]
    [ScriptOp("native:FadeOut")]
    protected static Task FadeOut(@fixed seconds) => Task.CompletedTask;

    /// <summary>
    /// Freezes the player and the other actors until the end of the block:
    /// <c>using var _ = LockAll();</c>
    /// </summary>
    [ScriptOp("lock")]
    protected static LockScope LockAll() => default;
}

/// <summary>Returned by <see cref="ScriptBase.LockAll"/>; unlocks at the end of the <c>using</c> block.</summary>
public struct LockScope : IDisposable
{
    /// <summary>Unlocks the actors.</summary>
    public void Dispose() { }
}

/// <summary>
/// Base class of every script attached to an actor. Name methods <c>Start</c>, <c>Update</c>, <c>LateUpdate</c>,
/// <c>OnTouch(Actor other)</c> and <c>OnDestroy</c> to receive those events. Public <c>int</c>, <c>fixed</c>,
/// <c>bool</c> and enum fields show in the Inspector.
/// </summary>
[Cpp("agb::Behaviour")]
public abstract class Behaviour : ScriptBase
{
    /// <summary>The actor this behaviour is attached to.</summary>
    [Cpp("{this.}actor")]
    [ScriptOp("actor:self")]
    public Actor actor => null;

    /// <summary>When false the behaviour gets no events.</summary>
    [Cpp("{this.}enabled")]
    public bool enabled;

    /// <summary>Another behaviour on the same actor, or null when it has none.</summary>
    /// <typeparam name="T">Behaviour type.</typeparam>
    [Cpp("{this.}actor->get<{T}>()")]
    public T Get<T>() where T : Behaviour => null;
}

/// <summary>
/// Script of a scene (Scene Settings): methods <c>OnLoad</c>, <c>OnEnter</c> and <c>OnExit</c> run when the scene
/// loads, appears and closes, and <c>[Cutscene]</c> methods hold the scene's cutscenes.
/// </summary>
public abstract class SceneScript : ScriptBase
{
}

/// <summary>A thing in the scene: position, sprite, collider and behaviours.</summary>
[Cpp("agb::Actor")]
public sealed class Actor
{
    Actor() { }

    /// <summary>A behaviour of this actor, or null when it has none.</summary>
    /// <typeparam name="T">Behaviour type.</typeparam>
    [Cpp("{this.}get<{T}>()")]
    public T Get<T>() where T : Behaviour => null;

    /// <summary>True when the actor has a behaviour of this type.</summary>
    /// <typeparam name="T">Behaviour type.</typeparam>
    [Cpp("{this.}is<{T}>()")]
    public bool Is<T>() where T : Behaviour => false;

    /// <summary>Removes the actor at the end of the frame.</summary>
    [Cpp("{this.}destroy()")]
    public void Destroy() { }

    /// <summary>Tag name from Project Settings; compare it with a string: <c>other.tag == "Coin"</c>.</summary>
    [Cpp("{this.}tag")]
    [Asset(AssetKind.Tag)]
    public string tag { get => null; set { } }

    /// <summary>Name in the scene Hierarchy.</summary>
    [Cpp("{this.}name")]
    public string name => null;

    /// <summary>World position of the origin in pixels.</summary>
    [Cpp("{this.}pos")]
    public fixed2 pos;

    /// <summary>Velocity set by the physics body (read only for most uses).</summary>
    [Cpp("{this.}vel")]
    public fixed2 vel;

    /// <summary>True until the actor is destroyed.</summary>
    [Cpp("{this.}isAlive()")]
    public bool IsAlive => false;

    /// <summary>True while standing on solid ground.</summary>
    [Cpp("{this.}onGround")]
    public bool OnGround => false;

    /// <summary>Shows or hides the sprite.</summary>
    [Cpp("{this.}visible", Set = "{this.}setVisible({value})")]
    public bool visible { get => false; set { } }

    /// <summary>When true, bodies (Top-down Mover) cannot walk through this actor: doors, crates, people.</summary>
    [Cpp("{this.}blocking")]
    public bool blocking { get => false; set { } }

    /// <summary>Mirrors the sprite horizontally.</summary>
    [Cpp("{this.}facingLeft", Set = "{this.}setFacingLeft({value})")]
    public bool facingLeft { get => false; set { } }

    /// <summary>Starts an animation of the sprite sheet by name.</summary>
    /// <param name="name">Animation name from the Sprite Editor.</param>
    [Cpp("{this.}playAnimation({0})")]
    public void PlayAnimation(string name) { }

    /// <summary>For a Text Label actor (HUD): shows its text followed by a number, e.g. "SCORE 0120".</summary>
    /// <param name="value">The number.</param>
    /// <param name="digits">Pads with zeros to this many digits (0 = no padding).</param>
    [Cpp("agb::text::setLabelValue({this.}textLabel, {this.}label, {0}, {1})")]
    public void SetLabelNumber(int value, int digits) { }
}

/// <summary>Limits an Inspector field to a range (a slider in the Inspector).</summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public sealed class RangeAttribute : Attribute
{
    /// <summary>Creates the range.</summary>
    /// <param name="min">Lowest value.</param>
    /// <param name="max">Highest value.</param>
    public RangeAttribute(double min, double max) { Min = min; Max = max; }

    /// <summary>Lowest value.</summary>
    public double Min { get; }

    /// <summary>Highest value.</summary>
    public double Max { get; }
}

/// <summary>
/// Lets database script cells call this static class's methods by name: an Items table cell "Unlock(door)"
/// calls <c>Unlock</c> of a <c>[DatabaseActions]</c> class.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class DatabaseActionsAttribute : Attribute
{
    /// <summary>Marks the class.</summary>
    public DatabaseActionsAttribute() { }
}

/// <summary>
/// Marks an <c>async Task</c> method as a cutscene: it compiles to Event Script bytecode and can <c>await</c>
/// Say, Choice, Move and Wait. Calling it from normal code starts it.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class CutsceneAttribute : Attribute
{
    /// <summary>Marks the method as a cutscene.</summary>
    public CutsceneAttribute() { }
}

/// <summary>
/// Makes a static method callable from cutscenes and dialogue. With <c>waits: true</c> the cutscene waits
/// until <see cref="EventScript.Continue"/> is called.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ScriptNativeAttribute : Attribute
{
    /// <summary>Declares the native.</summary>
    /// <param name="waits">True when the calling cutscene waits for <see cref="EventScript.Continue"/>.</param>
    public ScriptNativeAttribute(bool waits = false) { Waits = waits; }

    /// <summary>True when the calling cutscene waits for <see cref="EventScript.Continue"/>.</summary>
    public bool Waits { get; }
}

/// <summary>Event Script VM control from normal code.</summary>
[Cpp("agb::script::natives")]
public static class EventScript
{
    /// <summary>Resumes cutscenes waiting in a <c>[ScriptNative(waits: true)]</c> call.</summary>
    /// <param name="result">Value returned to the cutscene by the native.</param>
    [Cpp("agb::script::natives::resume({0})")]
    public static void Continue(int result = 0) { }

    /// <summary>True while a cutscene runs (player input is locked).</summary>
    [Cpp("agb::script::cutsceneRunning()")]
    public static bool CutsceneRunning => false;
}
