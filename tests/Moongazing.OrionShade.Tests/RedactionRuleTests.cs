namespace Moongazing.OrionShade.Tests;

using System.Text.RegularExpressions;

using Moongazing.OrionShade.Redaction;

using Xunit;

/// <summary>
/// Covers the <see cref="RedactionRule"/> constructor: property assignment and argument validation.
/// </summary>
public sealed class RedactionRuleTests
{
    [Fact]
    public void Constructor_assigns_name_pattern_and_mask()
    {
        var pattern = new Regex(@"\d+");
        var mask = Masks.Full();
        var rule = new RedactionRule("digits", pattern, mask);

        Assert.Equal("digits", rule.Name);
        Assert.Same(pattern, rule.Pattern);
        Assert.Same(mask, rule.Mask);
    }

    [Fact]
    public void Constructor_throws_when_the_name_is_null()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RedactionRule(null!, new Regex("x"), Masks.Full()));
    }

    [Fact]
    public void Constructor_throws_when_the_name_is_empty()
    {
        Assert.Throws<ArgumentException>(
            () => new RedactionRule(string.Empty, new Regex("x"), Masks.Full()));
    }

    [Fact]
    public void Constructor_throws_when_the_pattern_is_null()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RedactionRule("x", null!, Masks.Full()));
    }

    [Fact]
    public void Constructor_throws_when_the_mask_is_null()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RedactionRule("x", new Regex("x"), null!));
    }
}
