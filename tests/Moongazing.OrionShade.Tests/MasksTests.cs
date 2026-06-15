namespace Moongazing.OrionShade.Tests;

using Moongazing.OrionShade.Redaction;

using Xunit;

public sealed class MasksTests
{
    [Fact]
    public void Full_replaces_with_the_default_token()
    {
        Assert.Equal("[REDACTED]", Masks.Full()("anything"));
    }

    [Fact]
    public void Full_replaces_with_a_custom_token()
    {
        Assert.Equal("##", Masks.Full("##")("anything"));
    }

    [Fact]
    public void KeepLast_keeps_the_trailing_characters()
    {
        Assert.Equal("************1111", Masks.KeepLast(4)("4111111111111111"));
    }

    [Fact]
    public void KeepLast_fully_masks_a_value_no_longer_than_the_visible_count()
    {
        Assert.Equal("****", Masks.KeepLast(4)("1234"));
        Assert.Equal("**", Masks.KeepLast(4)("12"));
    }

    [Fact]
    public void KeepLast_rejects_a_negative_count()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Masks.KeepLast(-1));
    }
}
