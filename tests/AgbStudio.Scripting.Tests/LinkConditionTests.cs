namespace AgbStudio.Scripting.Tests;

/// <summary>P17.05: what the Link Settings builder writes compiles, and what it cannot read stays as text.</summary>
public sealed class LinkConditionTests
{
    [Theory]
    [InlineData("Game.coins >= 10", "coins", ">=", "10")]
    [InlineData("Game.coins>=10", "coins", ">=", "10")]
    [InlineData("  Game.speed != -1.5 ", "speed", "!=", "-1.5")]
    [InlineData("Game.lives < 3", "lives", "<", "3")]
    public void Parse_ReadsOneFieldAComparisonAndANumber(string text, string field, string op, string value) =>
        Assert.Equal(new LinkCondition(field, op, value), LinkCondition.Parse(text));

    [Theory]
    [InlineData("")]
    [InlineData("Game.coins > Game.best")]
    [InlineData("Game.coins >= 10 && Game.lives > 1")]
    [InlineData("Story.metOwl")]
    [InlineData("Game.metOwl == true")]
    [InlineData("Seconds(2) > 1")]
    public void Parse_LeavesEveryOtherConditionToTheText(string text) => Assert.Null(LinkCondition.Parse(text));

    [Theory]
    [InlineData("10", true)]
    [InlineData("-1.5", true)]
    [InlineData("abc", false)]
    [InlineData("", false)]
    [InlineData("1.", false)]
    public void IsValue_TakesNumbersOnly(string value, bool ok) => Assert.Equal(ok, LinkCondition.IsValue(value));

    [Fact]
    public void BuilderOutput_CompilesForEveryComparison()
    {
        var ctx = TestSupport.Context();
        var conditions = new SceneEventsDef { Scene = "Forest_01", File = Path.Combine(TestSupport.TestsDir, "scenes", "Forest_01.scene") };
        for (var i = 0; i < LinkCondition.Ops.Count; i++) conditions.LinkConditions[i] = new LinkCondition("score", LinkCondition.Ops[i], "10").Text;
        ctx.SceneEvents.Add(conditions);
        var r = TestSupport.Compile("link-condition", ctx, TestSupport.Script("Forest.cs"));
        Assert.True(r.Success, TestSupport.Errors(r));
        var hpp = File.ReadAllText(Path.Combine(r.IncludeDir, "agb_scripts.hpp"));
        for (var i = 0; i < LinkCondition.Ops.Count; i++) Assert.Contains($"bool agb_link_Forest_01_{i}();", hpp);
    }
}
