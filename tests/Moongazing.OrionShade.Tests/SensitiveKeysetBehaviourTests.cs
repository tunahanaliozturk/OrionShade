namespace Moongazing.OrionShade.Tests;

using Moongazing.OrionShade.Redaction;

using Xunit;

/// <summary>
/// Additional coverage for <see cref="SensitiveKeyset"/>: the full default key set, case handling,
/// custom keysets, duplicate tolerance, and argument validation.
/// </summary>
public sealed class SensitiveKeysetBehaviourTests
{
    public static TheoryData<string> DefaultKeys
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var key in new[]
            {
                "password", "passwd", "pwd", "secret", "token", "authorization", "auth",
                "apikey", "api_key", "access_token", "refresh_token", "client_secret",
                "ssn", "creditcard", "credit_card", "cardnumber", "card_number", "cvv", "pin",
            })
            {
                data.Add(key);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(DefaultKeys))]
    public void Default_keyset_contains_every_documented_key(string key)
    {
        Assert.True(SensitiveKeyset.Default.IsSensitive(key));
        Assert.True(SensitiveKeyset.Default.IsSensitive(key.ToUpperInvariant()));
    }

    [Fact]
    public void An_unlisted_key_is_not_sensitive()
    {
        Assert.False(SensitiveKeyset.Default.IsSensitive("username"));
        Assert.False(SensitiveKeyset.Default.IsSensitive("email"));
        Assert.False(SensitiveKeyset.Default.IsSensitive(string.Empty));
    }

    [Fact]
    public void A_custom_keyset_uses_only_its_own_keys()
    {
        var keyset = new SensitiveKeyset(["national_id"]);

        Assert.True(keyset.IsSensitive("national_id"));
        Assert.True(keyset.IsSensitive("NATIONAL_ID"));
        Assert.False(keyset.IsSensitive("password"));
    }

    [Fact]
    public void An_empty_keyset_treats_nothing_as_sensitive()
    {
        var keyset = new SensitiveKeyset([]);

        Assert.False(keyset.IsSensitive("password"));
    }

    [Fact]
    public void Duplicate_keys_are_tolerated()
    {
        var keyset = new SensitiveKeyset(["dup", "dup", "DUP"]);

        Assert.True(keyset.IsSensitive("dup"));
    }

    [Fact]
    public void Constructor_throws_when_keys_enumerable_is_null()
    {
        Assert.Throws<ArgumentNullException>(() => new SensitiveKeyset(null!));
    }

    [Fact]
    public void IsSensitive_throws_when_the_key_is_null()
    {
        Assert.Throws<ArgumentNullException>(() => SensitiveKeyset.Default.IsSensitive(null!));
    }
}
