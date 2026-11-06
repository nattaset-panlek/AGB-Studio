using System.Text.RegularExpressions;

namespace AgbStudio.Scripting;

/// <summary>
/// The kind of link "Only if" condition the Link Settings builder can show: one Save data field, a comparison and a
/// number (P17.05). Any other condition is valid too; it stays as typed text.
/// </summary>
public sealed partial record LinkCondition(string Field, string Op, string Value)
{
    public static readonly IReadOnlyList<string> Ops = [">=", ">", "<=", "<", "==", "!="];

    public string Text => $"Game.{Field} {Op} {Value}";

    /// <summary>The condition's parts, or null when the builder cannot show it.</summary>
    public static LinkCondition? Parse(string text)
    {
        var m = ConditionRx().Match(text);
        return m.Success ? new LinkCondition(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value) : null;
    }

    public static bool IsValue(string value) => ValueRx().IsMatch(value);

    [GeneratedRegex(@"^\s*Game\.([A-Za-z_][A-Za-z0-9_]*)\s*(>=|<=|==|!=|>|<)\s*(-?\d+(?:\.\d+)?)\s*$")]
    private static partial Regex ConditionRx();

    [GeneratedRegex(@"^-?\d+(?:\.\d+)?$")]
    private static partial Regex ValueRx();
}
