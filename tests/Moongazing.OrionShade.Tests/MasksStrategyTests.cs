namespace Moongazing.OrionShade.Tests;

using Moongazing.OrionShade.Redaction;

using Xunit;

/// <summary>
/// Additional coverage for the <see cref="Masks"/> strategies: custom tokens, custom mask
/// characters, KeepLast boundary conditions, and argument validation.
/// </summary>
public sealed class MasksStrategyTests
{
    [Fact]
    public void DefaultToken_is_the_expected_constant()
    {
        Assert.Equal("[REDACTED]", Masks.DefaultToken);
    }

    [Fact]
    public void Full_ignores_its_input_and_always_returns_the_token()
    {
        var mask = Masks.Full();
        Assert.Equal("[REDACTED]", mask(string.Empty));
        Assert.Equal("[REDACTED]", mask("a very long secret value"));
    }

    [Fact]
    public void Full_with_a_custom_token_ignores_its_input()
    {
        var mask = Masks.Full("<hidden>");
        Assert.Equal("<hidden>", mask("anything"));
        Assert.Equal("<hidden>", mask(string.Empty));
    }

    [Fact]
    public void Full_with_an_empty_token_is_allowed()
    {
        var mask = Masks.Full(string.Empty);
        Assert.Equal(string.Empty, mask("anything"));
    }

    [Fact]
    public void Full_throws_when_the_token_is_null()
    {
        Assert.Throws<ArgumentNullException>(() => Masks.Full(null!));
    }

    [Fact]
    public void KeepLast_keeps_the_trailing_characters_with_the_default_mask_char()
    {
        Assert.Equal("************1111", Masks.KeepLast(4)("4111111111111111"));
    }

    [Fact]
    public void KeepLast_supports_a_custom_mask_char()
    {
        Assert.Equal("############1111", Masks.KeepLast(4, '#')("4111111111111111"));
    }

    [Theory]
    [InlineData("1234", 4, "****")]
    [InlineData("12", 4, "**")]
    [InlineData("", 4, "")]
    [InlineData("abcd", 0, "****")]
    public void KeepLast_fully_masks_when_input_is_no_longer_than_visible(
        string input, int visible, string expected)
    {
        Assert.Equal(expected, Masks.KeepLast(visible)(input));
    }

    [Fact]
    public void KeepLast_with_zero_visible_masks_the_entire_value()
    {
        Assert.Equal("******", Masks.KeepLast(0)("secret"));
    }

    [Fact]
    public void KeepLast_keeps_exactly_the_requested_number_of_trailing_chars()
    {
        Assert.Equal("*bcde", Masks.KeepLast(4)("abcde"));
    }

    [Fact]
    public void KeepLast_rejects_a_negative_count()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Masks.KeepLast(-1));
        Assert.Equal("visible", ex.ParamName);
    }

    [Fact]
    public void KeepLast_handles_a_single_visible_char()
    {
        Assert.Equal("****t", Masks.KeepLast(1)("abcdt"));
    }
}
