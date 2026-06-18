namespace Moongazing.OrionShade.Tests;

using Moongazing.OrionShade;
using Moongazing.OrionShade.Diagnostics;
using Moongazing.OrionShade.Redaction;

using Xunit;

/// <summary>
/// Covers <see cref="Redactor.RedactValue"/>: sensitive-key short-circuit masking across the full
/// default keyset (case-insensitively) and the non-sensitive-key fall-through to the pattern sweep.
/// </summary>
public sealed class RedactValueTests
{
    private static Redactor Build(ShadeDiagnostics diagnostics) =>
        new(BuiltInRules.All, SensitiveKeyset.Default, Masks.Full(), diagnostics);

    [Theory]
    [InlineData("password")]
    [InlineData("passwd")]
    [InlineData("pwd")]
    [InlineData("secret")]
    [InlineData("token")]
    [InlineData("authorization")]
    [InlineData("auth")]
    [InlineData("apikey")]
    [InlineData("api_key")]
    [InlineData("access_token")]
    [InlineData("refresh_token")]
    [InlineData("client_secret")]
    [InlineData("ssn")]
    [InlineData("creditcard")]
    [InlineData("credit_card")]
    [InlineData("cardnumber")]
    [InlineData("card_number")]
    [InlineData("cvv")]
    [InlineData("pin")]
    public void Every_default_sensitive_key_masks_the_whole_value(string key)
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        // A value that on its own matches no pattern rule, proving the masking is keyed by name.
        Assert.Equal(Masks.DefaultToken, redactor.RedactValue(key, "plainvalue123"));
    }

    [Theory]
    [InlineData("PASSWORD")]
    [InlineData("Password")]
    [InlineData("PassWord")]
    [InlineData("Authorization")]
    [InlineData("API_KEY")]
    [InlineData("Access_Token")]
    public void Sensitive_keys_match_case_insensitively(string key)
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        Assert.Equal(Masks.DefaultToken, redactor.RedactValue(key, "hunter2"));
    }

    [Fact]
    public void A_sensitive_key_masks_even_a_value_that_would_not_match_any_pattern()
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        // "the quick brown fox" matches no rule; it is masked purely because the key is sensitive.
        Assert.Equal(Masks.DefaultToken, redactor.RedactValue("secret", "the quick brown fox"));
    }

    [Fact]
    public void A_non_sensitive_key_falls_through_to_the_pattern_sweep_and_masks_matches()
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        var result = redactor.RedactValue("note", "email me at a@b.com");

        Assert.DoesNotContain("a@b.com", result, StringComparison.Ordinal);
        Assert.Contains(Masks.DefaultToken, result, StringComparison.Ordinal);
    }

    [Fact]
    public void A_non_sensitive_key_leaves_a_clean_value_unchanged()
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        const string clean = "order shipped";
        Assert.Equal(clean, redactor.RedactValue("status", clean));
    }

    [Fact]
    public void The_configured_key_mask_is_used_for_sensitive_keys()
    {
        using var diag = new ShadeDiagnostics();
        var redactor = new Redactor(
            BuiltInRules.All, SensitiveKeyset.Default, Masks.KeepLast(2), diag);

        // KeepLast(2) over "hunter2" keeps the trailing "r2" and masks the rest.
        Assert.Equal("*****r2", redactor.RedactValue("password", "hunter2"));
    }

    [Fact]
    public void RedactValue_throws_when_the_key_is_null()
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        Assert.Throws<ArgumentNullException>(() => redactor.RedactValue(null!, "x"));
    }

    [Fact]
    public void RedactValue_throws_when_the_value_is_null()
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        Assert.Throws<ArgumentNullException>(() => redactor.RedactValue("password", null!));
    }

    [Fact]
    public void RedactValue_masks_an_empty_value_for_a_sensitive_key()
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        // The full mask ignores its input, so an empty sensitive value still yields the token.
        Assert.Equal(Masks.DefaultToken, redactor.RedactValue("password", string.Empty));
    }

    [Fact]
    public void RedactValue_returns_an_empty_value_unchanged_for_a_non_sensitive_key()
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        // Non-sensitive key falls through to Redact, which returns empty input as-is.
        Assert.Equal(string.Empty, redactor.RedactValue("note", string.Empty));
    }
}
