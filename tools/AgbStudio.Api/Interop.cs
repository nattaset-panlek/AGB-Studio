using System;
using System.ComponentModel;

namespace GBA;

/// <summary>
/// Maps an API member to the C++ the translator writes. Placeholders: <c>{0}</c>..<c>{9}</c> arguments,
/// <c>{this}</c> the receiver as a pointer, <c>{this.}</c> the receiver followed by <c>-&gt;</c> or <c>.</c>
/// (nothing for an implicit <c>this</c>), <c>{T}</c> the first type argument, <c>{value}</c> the assigned value.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
[AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = false)]
public sealed class CppAttribute : Attribute
{
    /// <summary>Creates the mapping.</summary>
    /// <param name="template">C++ expression template (or type name for types).</param>
    public CppAttribute(string template) { Template = template; }

    /// <summary>C++ expression template, or the C++ type name when placed on a type.</summary>
    public string Template { get; }

    /// <summary>Template used when a property is assigned (<c>{value}</c> is the new value).</summary>
    public string Set { get; set; }

    /// <summary>Precedence of the template result (16 = primary, 14 = multiplicative, 13 = additive).</summary>
    public int Prec { get; set; } = 16;
}

/// <summary>Kinds of project names that the compiler turns into ids at build time.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public enum AssetKind
{
    /// <summary>Sound effect name (Audio page).</summary>
    Sound,
    /// <summary>Music module name.</summary>
    Music,
    /// <summary>Scene name.</summary>
    Scene,
    /// <summary>Actor tag (Project Settings tags).</summary>
    Tag,
    /// <summary>Localized string key.</summary>
    Text,
    /// <summary>Dialogue speaker name.</summary>
    Speaker,
    /// <summary>Portrait name.</summary>
    Portrait,
    /// <summary>Movement path name.</summary>
    Path,
    /// <summary>Actor name in the current scene.</summary>
    ActorName,
}

/// <summary>The string argument or property value is a project name resolved to an id at build time.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method, AllowMultiple = false)]
public sealed class AssetAttribute : Attribute
{
    /// <summary>Creates the marker.</summary>
    /// <param name="kind">What the name refers to.</param>
    public AssetAttribute(AssetKind kind) { Kind = kind; }

    /// <summary>What the name refers to.</summary>
    public AssetKind Kind { get; }
}

/// <summary>
/// How a member compiles inside a <c>[Cutscene]</c> method: <c>native:Name</c> (built-in native from
/// runtime/include/agb/natives.hpp), <c>op:Name</c> (VM opcode), <c>var:Id</c>, <c>flag:Id</c>,
/// <c>game:Index</c> (game state field), <c>actor:player</c>, <c>actor:self</c>, <c>lock</c>.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class ScriptOpAttribute : Attribute
{
    /// <summary>Creates the mapping.</summary>
    /// <param name="target">Native, opcode or store the member compiles to.</param>
    public ScriptOpAttribute(string target) { Target = target; }

    /// <summary>Native, opcode or store the member compiles to.</summary>
    public string Target { get; }
}
