namespace Moongazing.OrionShade.Tests;

using Moongazing.OrionShade.Redaction;

using Xunit;

public sealed class SensitiveKeysetTests
{
    [Fact]
    public void Default_keys_match_case_insensitively()
    {
        Assert.True(SensitiveKeyset.Default.IsSensitive("password"));
        Assert.True(SensitiveKeyset.Default.IsSensitive("PASSWORD"));
        Assert.True(SensitiveKeyset.Default.IsSensitive("Authorization"));
    }

    [Fact]
    public void An_unlisted_key_is_not_sensitive()
    {
        Assert.False(SensitiveKeyset.Default.IsSensitive("username"));
    }

    [Fact]
    public void A_custom_keyset_uses_its_own_keys()
    {
        var keyset = new SensitiveKeyset(["national_id"]);
        Assert.True(keyset.IsSensitive("NATIONAL_ID"));
        Assert.False(keyset.IsSensitive("password"));
    }
}
