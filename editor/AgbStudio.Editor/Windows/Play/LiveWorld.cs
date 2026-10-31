using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;
using AgbStudio.Emu;

namespace AgbStudio.Editor.Windows.Play;

/// <summary>A value kind of a live field: int, fixed (24.8), bool, u16 or float.</summary>
public enum LiveKind { Int, Fixed, Bool, U16, Float, S16, U8, S8 }

/// <summary>One exposed field of a behaviour read from emulator memory (Inspector · Live).</summary>
public sealed record LiveField(string Name, LiveKind Kind, uint Address, double Value)
{
    public string Text => Kind switch
    {
        LiveKind.Bool => Value != 0 ? "true" : "false",
        LiveKind.Fixed or LiveKind.Float => Value.ToString("0.0##", CultureInfo.InvariantCulture),
        _ => Value.ToString("0", CultureInfo.InvariantCulture),
    };
}

/// <summary>A behaviour instance on a live actor: a built-in (PlatformerBody ...) or a C# script class.</summary>
public sealed record LiveBehaviour(string Type, bool IsScript, uint Address, IReadOnlyList<LiveField> Fields)
{
    public LiveField? Field(string name) => Fields.FirstOrDefault(f => f.Name == name);
}

/// <summary>A live actor with its memory address, collider and behaviours, joined with its scene data.</summary>
public sealed record LiveActorInfo(LiveActor Actor, uint Address, int ColliderX, int ColliderY, int ColliderW, int ColliderH,
    IReadOnlyList<LiveBehaviour> Behaviours, ActorData? Data)
{
    public string Name => Actor.Name;

    /// <summary>The short state of the Play Mode Inspector ("state Jump"): the runtime state id when a script sets one, else the body's motion.</summary>
    public string State => Actor.State != 0 ? Actor.State.ToString(CultureInfo.InvariantCulture)
        : !Actor.OnGround && Actor.Vy < 0 ? "Jump" : !Actor.OnGround && Actor.Vy > 0 ? "Fall" : Math.Abs(Actor.Vx) > 0.01 ? "Run" : "Idle";
}

/// <summary>What one frame of the running game looks like to the editor: scene, camera and actors.</summary>
public sealed record LiveWorld(int SceneIndex, string SceneName, int CameraX, int CameraY, IReadOnlyList<LiveActorInfo> Actors)
{
    public static readonly LiveWorld Empty = new(-1, "", 0, 0, []);
    public LiveActorInfo? Find(string? name) => name is null ? null : Actors.FirstOrDefault(a => a.Name == name);
}

/// <summary>Byte offset and C++ type of a member, from a class declaration.</summary>
public sealed record MemberLayout(string Name, string Type, int Offset, int Size);

/// <summary>
/// Lays out the data members of a behaviour class the way GCC (ARM EABI, Itanium C++ ABI) does, from its C++
/// declaration: the generated script headers (build/gen/scripts/&lt;Class&gt;.hpp) and runtime/include/agb/behaviours.hpp.
/// The base agb::Behaviour holds the vtable pointer, the actor pointer and two bools (10 data bytes); members follow
/// in declaration order with natural alignment, starting in the base's tail padding.
/// </summary>
public static class BehaviourLayout
{
    public const int BaseDataSize = 10;

    static readonly Regex Decl = new(@"^(?<type>(?:const\s+)?[A-Za-z_][\w:]*(?:<[^<>]*>)?(?:\s*\*+)?)\s+(?<rest>[^;]+);$", RegexOptions.Compiled);

    public static (int Size, int Align)? TypeSize(string type)
    {
        type = type.Replace(" ", "");
        if (type.EndsWith('*')) return (4, 4);
        type = type.StartsWith("agb::", StringComparison.Ordinal) ? type[5..] : type;
        return type switch
        {
            "fixed" or "s32" or "u32" or "int" or "unsigned" or "long" or "float" or "Id" or "render::sprites::Id32" => (4, 4),
            "fixed2" => (8, 4),
            "s16" or "u16" or "short" or "render::sprites::Id" => (2, 2),
            "s8" or "u8" or "bool" or "char" => (1, 1),
            _ => null,
        };
    }

    /// <summary>Data members of class <paramref name="className"/> in <paramref name="header"/>, with offsets; stops at the first member of unknown size.</summary>
    public static IReadOnlyList<MemberLayout> Members(string header, string className)
    {
        var list = new List<MemberLayout>();
        var lines = header.Split('\n');
        var start = Array.FindIndex(lines, l => Regex.IsMatch(l, $@"\b(class|struct)\s+{Regex.Escape(className)}\b[^;]*:\s*public\s+(agb::)?Behaviour\b"));
        if (start < 0) return list;
        var offset = BaseDataSize;
        var depth = 0;
        for (var i = start; i < lines.Length; i++)
        {
            var line = lines[i];
            var cut = line.IndexOf("//", StringComparison.Ordinal);
            if (cut >= 0) line = line[..cut];
            line = line.Trim();
            var opens = line.Count(c => c == '{');
            var closes = line.Count(c => c == '}');
            if (i == start) { depth += opens - closes; continue; }
            var before = depth;
            depth += opens - closes;
            if (depth <= 0) break;
            if (before != 1 || line.Length == 0 || line.StartsWith('#') || !line.EndsWith(';')) continue;
            if (Regex.IsMatch(line, @"^(static|virtual|using|friend|template|typedef|enum|struct|class|public|private|protected|explicit|constexpr|inline)\b")) continue;
            var paren = line.IndexOf('(');
            var eq = line.IndexOf('=');
            if (paren >= 0 && (eq < 0 || paren < eq)) continue;  // a method
            if (opens > 0 && (eq < 0 || line.IndexOf('{') < eq)) continue;
            var m = Decl.Match(line);
            if (!m.Success) continue;
            var type = m.Groups["type"].Value;
            foreach (var d in SplitTop(m.Groups["rest"].Value))
            {
                var decl = d.Trim();
                var stars = 0;
                while (decl.StartsWith('*')) { stars++; decl = decl[1..].TrimStart(); }
                var nm = Regex.Match(decl, @"^(?<name>[A-Za-z_]\w*)\s*(\[(?<n>\d+)\])?");
                if (!nm.Success) return list;
                var t = stars > 0 ? type + new string('*', stars) : type;
                if (TypeSize(t) is not { } sz) return list;
                var count = nm.Groups["n"].Success ? int.Parse(nm.Groups["n"].Value, CultureInfo.InvariantCulture) : 1;
                offset = (offset + sz.Align - 1) / sz.Align * sz.Align;
                list.Add(new MemberLayout(nm.Groups["name"].Value, t.Replace(" ", ""), offset, sz.Size * count));
                offset += sz.Size * count;
            }
        }
        return list;
    }

    static IEnumerable<string> SplitTop(string s)
    {
        var depth = 0;
        var from = 0;
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] is '(' or '{' or '<') depth++;
            else if (s[i] is ')' or '}' or '>') depth--;
            else if (s[i] == ',' && depth == 0) { yield return s[from..i]; from = i + 1; }
        }
        yield return s[from..];
    }
}

/// <summary>
/// Reads the live world from emulator memory: the debug channel actor table (agb::ActorDebug), each actor's
/// agb::Actor record (collider, behaviours) and the behaviours' exposed fields, plus the scene camera from the ELF.
/// Layouts are checked against the values the debug channel publishes before they are used.
/// </summary>
public sealed class LiveWorldReader
{
    // agb::Actor (runtime/include/agb/actor.hpp): blocking (after solid) and textLabel (after label) moved the behaviour
    // table by 4 bytes. A layout change shows as missing script fields in Play Mode E2E P12.06 and Debugger P13.05.
    const uint ActorPos = 12, ActorCollider = 28, ActorBehaviours = 60, ActorBehaviourCount = 84;

    readonly GameProject _project;
    readonly ElfSymbols? _elf;
    readonly Dictionary<string, (IReadOnlyList<MemberLayout> Members, IReadOnlyList<(string Name, LiveKind Kind)> Exposed, bool Script)> _types = new(StringComparer.Ordinal);
    readonly Dictionary<string, SceneFile?> _scenes = new(StringComparer.Ordinal);
    readonly List<string> _sceneNames;
    int _lastCamX, _lastCamY;

    public LiveWorldReader(GameProject project, ElfSymbols? elf)
    {
        _project = project;
        _elf = elf;
        _sceneNames = project.SceneFiles().Select(GameProject.AssetName).ToList();
        LoadTypes();
    }

    public IReadOnlyList<string> SceneNames => _sceneNames;

    void LoadTypes()
    {
        string? runtimeHeader = null;
        try
        {
            var path = Path.Combine(AgbStudio.Build.NativeBuild.RuntimeDir, "include", "agb", "behaviours.hpp");
            if (File.Exists(path)) runtimeHeader = File.ReadAllText(path);
        }
        catch (InvalidOperationException) { }
        foreach (var b in BuiltinBehaviours.All)
        {
            var members = runtimeHeader is null ? [] : BehaviourLayout.Members(runtimeHeader, b.CppClass.Replace("agb::", ""));
            var exposed = b.Fields.Where(f => f.Kind is BuiltinBehaviours.Kind.Fixed or BuiltinBehaviours.Kind.Int or BuiltinBehaviours.Kind.Bool or BuiltinBehaviours.Kind.U16)
                .Select(f => (f.CppMember, f.Kind switch { BuiltinBehaviours.Kind.Fixed => LiveKind.Fixed, BuiltinBehaviours.Kind.Bool => LiveKind.Bool, BuiltinBehaviours.Kind.U16 => LiveKind.U16, _ => LiveKind.Int })).ToList();
            if (b.Name == "PlatformerBody") exposed.Add(("onGround", LiveKind.Bool));
            _types[b.Name] = (members, exposed, false);
        }
        var genDir = Path.Combine(_project.BuildDir, "gen", "scripts");
        var manifest = Path.Combine(genDir, "scripts.json");
        if (!File.Exists(manifest)) return;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(manifest));
            if (!doc.RootElement.TryGetProperty("behaviours", out var behaviours)) return;
            foreach (var b in behaviours.EnumerateArray())
            {
                var name = b.GetProperty("name").GetString() ?? "";
                var header = Path.Combine(genDir, name + ".hpp");
                var members = File.Exists(header) ? BehaviourLayout.Members(File.ReadAllText(header), name) : [];
                var exposed = new List<(string, LiveKind)>();
                if (b.TryGetProperty("fields", out var fields))
                    foreach (var f in fields.EnumerateArray())
                        exposed.Add((f.GetProperty("name").GetString() ?? "", f.GetProperty("kind").GetInt32() switch { 1 => LiveKind.Fixed, 2 => LiveKind.Bool, _ => LiveKind.Int }));
                _types[name] = (members, exposed, true);
            }
        }
        catch (JsonException) { }
        catch (KeyNotFoundException) { }
    }

    SceneFile? Scene(string name)
    {
        if (_scenes.TryGetValue(name, out var s)) return s;
        var path = Path.Combine(_project.ScenesDir, name + ".scene");
        try { s = File.Exists(path) ? GameProject.Load<SceneFile>(path) : null; }
        catch (JsonException) { s = null; }
        return _scenes[name] = s;
    }

    public SceneFile? CurrentScene(LiveWorld w) => w.SceneName.Length == 0 ? null : Scene(w.SceneName);

    public LiveWorld Read(LibretroCore core, DebugChannel ch)
    {
        try { return ReadCore(core, ch); }
        catch (ArgumentOutOfRangeException) { return LiveWorld.Empty; }
    }

    LiveWorld ReadCore(LibretroCore core, DebugChannel ch)
    {
        var sceneIndex = ch.Game != 0 ? core.ReadU16(ch.Game) : -1;
        var sceneName = sceneIndex >= 0 && sceneIndex < _sceneNames.Count ? _sceneNames[sceneIndex] : "";
        var scene = sceneName.Length > 0 ? Scene(sceneName) : null;
        var actors = new List<LiveActorInfo>();
        var table = ch.ActorTable;
        var stride = ch.ActorStride;
        var live = Inspector.Actors(core, ch);
        for (var i = 0; i < live.Count && table != 0 && stride >= 28; i++)
        {
            var debug = table + (uint)i * stride;
            var addr = core.ReadU32(debug + 24);
            var a = live[i];
            int cx = 0, cy = 0, cw = 0, ch2 = 0;
            var behaviours = new List<LiveBehaviour>();
            var data = scene?.Actors.FirstOrDefault(x => x.Name == a.Name);
            if (IsRam(addr) && core.ReadU32(addr + ActorPos) == core.ReadU32(debug + 4))
            {
                cx = (short)core.ReadU16(addr + ActorCollider);
                cy = (short)core.ReadU16(addr + ActorCollider + 2);
                cw = core.ReadU16(addr + ActorCollider + 4);
                ch2 = core.ReadU16(addr + ActorCollider + 6);
                int count = core.Memory(addr + ActorBehaviourCount, 1)[0];
                for (var k = 0; k < Math.Min(count, 6); k++)
                {
                    var b = core.ReadU32(addr + ActorBehaviours + (uint)k * 4);
                    if (!IsRam(b) || core.ReadU32(b + 4) != addr) continue;
                    var type = data != null && k < data.Components.Count ? data.Components[k].Type : "";
                    behaviours.Add(ReadBehaviour(core, type, b));
                }
            }
            actors.Add(new LiveActorInfo(a, addr, cx, cy, cw, ch2, behaviours, data));
        }
        var (camX, camY) = Camera(core, scene, actors);
        return new LiveWorld(sceneIndex, sceneName, camX, camY, actors);
    }

    LiveBehaviour ReadBehaviour(LibretroCore core, string type, uint address)
    {
        if (!_types.TryGetValue(type, out var t)) return new LiveBehaviour(type, false, address, []);
        var fields = new List<LiveField>();
        foreach (var (name, kind) in t.Exposed)
        {
            var m = t.Members.FirstOrDefault(x => x.Name == name);
            if (m is null) continue;
            var at = address + (uint)m.Offset;
            var k = KindOf(m.Type) ?? kind;
            fields.Add(new LiveField(name, k, at, ReadValue(core, at, k)));
        }
        return new LiveBehaviour(type, t.Script, address, fields);
    }

    /// <summary>The value kind of a C++ member type; null for 32-bit ints and enums (the declared kind decides).</summary>
    public static LiveKind? KindOf(string cppType) => (cppType.StartsWith("agb::", StringComparison.Ordinal) ? cppType[5..] : cppType) switch
    {
        "fixed" => LiveKind.Fixed,
        "float" => LiveKind.Float,
        "bool" => LiveKind.Bool,
        "u16" => LiveKind.U16,
        "s16" or "short" => LiveKind.S16,
        "u8" or "char" => LiveKind.U8,
        "s8" => LiveKind.S8,
        _ => null,
    };

    public static double ReadValue(LibretroCore core, uint at, LiveKind kind) => kind switch
    {
        LiveKind.Fixed => (int)core.ReadU32(at) / 256.0,
        LiveKind.Bool => core.Memory(at, 1)[0] != 0 ? 1 : 0,
        LiveKind.U8 => core.Memory(at, 1)[0],
        LiveKind.S8 => (sbyte)core.Memory(at, 1)[0],
        LiveKind.U16 => core.ReadU16(at),
        LiveKind.S16 => (short)core.ReadU16(at),
        LiveKind.Float => BitConverter.Int32BitsToSingle((int)core.ReadU32(at)),
        _ => (int)core.ReadU32(at),
    };

    /// <summary>Writes an Inspector edit into the running game (D-15: value edits apply live without a rebuild).</summary>
    public static void WriteValue(LibretroCore core, uint at, LiveKind kind, double value)
    {
        switch (kind)
        {
            case LiveKind.Fixed: core.WriteU32(at, (uint)(int)Math.Round(value * 256)); break;
            case LiveKind.Bool: core.Memory(at, 1)[0] = (byte)(value != 0 ? 1 : 0); break;
            case LiveKind.U8 or LiveKind.S8: core.Memory(at, 1)[0] = unchecked((byte)(int)Math.Round(value)); break;
            case LiveKind.U16 or LiveKind.S16: BitConverter.TryWriteBytes(core.Memory(at, 2), unchecked((ushort)(int)Math.Round(value))); break;
            case LiveKind.Float: core.WriteU32(at, (uint)BitConverter.SingleToInt32Bits((float)value)); break;
            default: core.WriteU32(at, (uint)(int)Math.Round(value)); break;
        }
    }

    /// <summary>Writes an actor position (Transform in Inspector · Live).</summary>
    public static void WritePosition(LibretroCore core, LiveActorInfo a, double x, double y)
    {
        if (!IsRam(a.Address)) return;
        core.WriteU32(a.Address + ActorPos, (uint)(int)Math.Round(x * 256));
        core.WriteU32(a.Address + ActorPos + 4, (uint)(int)Math.Round(y * 256));
    }

    /// <summary>
    /// The scene camera: agb's SceneState keeps camX, camY and the followed actor next to each other, so the two
    /// ints before the follow pointer inside the ELF symbol g_scene are the camera. Scenes no larger than the screen
    /// have the camera at 0, 0 (clampCamera).
    /// </summary>
    (int X, int Y) Camera(LibretroCore core, SceneFile? scene, List<LiveActorInfo> actors)
    {
        if (scene != null && scene.Width * 16 <= 240 && scene.Height * 16 <= 160) return (_lastCamX = 0, _lastCamY = 0);
        var sym = _elf?.Find("agb::(anonymous namespace)::g_scene");
        var follow = scene?.Camera.Follow is { Length: > 0 } f ? actors.FirstOrDefault(a => a.Name == f) : null;
        if (sym != null && follow != null && IsRam(follow.Address))
        {
            for (uint o = 8; o + 4 <= sym.Size; o += 4)
            {
                if (core.ReadU32(sym.Address + o) != follow.Address) continue;
                int x = (int)core.ReadU32(sym.Address + o - 8), y = (int)core.ReadU32(sym.Address + o - 4);
                if (Math.Abs(follow.Actor.X - x - 120) > 240 || Math.Abs(follow.Actor.Y - y - 80) > 160) continue;
                return (_lastCamX = x, _lastCamY = y);
            }
        }
        return (_lastCamX, _lastCamY);
    }

    static bool IsRam(uint a) => a is >= 0x02000000 and < 0x02040000 or >= 0x03000000 and < 0x03008000;
}
