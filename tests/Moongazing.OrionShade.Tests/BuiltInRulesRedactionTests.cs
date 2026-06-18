namespace Moongazing.OrionShade.Tests;

using Moongazing.OrionShade;
using Moongazing.OrionShade.Diagnostics;
using Moongazing.OrionShade.Redaction;

using Xunit;

/// <summary>
/// Exercises each built-in pattern rule (email, credit card, JWT) individually and in combination
/// through a redactor wired with <see cref="BuiltInRules.All"/>.
/// </summary>
[Collection(nameof(MeterSerial))]
public sealed class BuiltInRulesRedactionTests
{
    private static Redactor Build(ShadeDiagnostics diagnostics) =>
        new(BuiltInRules.All, SensitiveKeyset.Default, Masks.Full(), diagnostics);

    [Theory]
    [InlineData("jane.doe@example.com")]
    [InlineData("a@b.co")]
    [InlineData("first.last+tag@sub.domain.io")]
    [InlineData("UPPER.CASE@EXAMPLE.COM")]
    [InlineData("user_name@my-host.example.org")]
    public void Email_rule_masks_a_variety_of_addresses(string email)
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        var result = redactor.Redact($"reach me at {email} today");

        Assert.DoesNotContain(email, result, StringComparison.Ordinal);
        Assert.Contains(Masks.DefaultToken, result, StringComparison.Ordinal);
    }

    [Fact]
    public void Email_rule_masks_every_address_when_several_are_present()
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        var result = redactor.Redact("from a@b.com to c@d.org cc e@f.net");

        Assert.DoesNotContain("a@b.com", result, StringComparison.Ordinal);
        Assert.DoesNotContain("c@d.org", result, StringComparison.Ordinal);
        Assert.DoesNotContain("e@f.net", result, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("4111 1111 1111 1234", "1234")]
    [InlineData("4111-1111-1111-1234", "1234")]
    [InlineData("4111111111111234", "1234")]
    public void Card_rule_keeps_only_the_last_four_digits(string card, string lastFour)
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        var result = redactor.Redact($"charge to {card} now");

        Assert.DoesNotContain(card, result, StringComparison.Ordinal);
        Assert.Contains(lastFour, result, StringComparison.Ordinal);
        // The leading digits must be masked: the original first group must be gone.
        Assert.DoesNotContain("4111", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Card_rule_masks_the_separators_inside_the_run()
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        // The card mask (KeepLast) replaces matched characters one-for-one including spaces/hyphens,
        // so the trailing visible portion is the last four matched characters verbatim.
        var result = redactor.Redact("4111 1111 1111 1234");

        Assert.EndsWith("1234", result, StringComparison.Ordinal);
        Assert.StartsWith("*", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Jwt_rule_masks_a_token_entirely()
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);
        const string jwt = "eyJhbGc.eyJzdWIiOiIxMjM0.SflKxwRJSMeKKF2QT4";

        var result = redactor.Redact($"Authorization: Bearer {jwt}");

        Assert.DoesNotContain(jwt, result, StringComparison.Ordinal);
        Assert.Contains(Masks.DefaultToken, result, StringComparison.Ordinal);
    }

    [Fact]
    public void Combined_input_masks_email_card_and_jwt_in_one_pass()
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);
        const string jwt = "eyJhbGc.eyJzdWIiOiIxMjM0.SflKxwRJSMeKKF2QT4";
        const string email = "ops@example.com";
        const string card = "4111 1111 1111 9999";

        var result = redactor.Redact($"user {email} paid with {card} token {jwt}");

        Assert.DoesNotContain(email, result, StringComparison.Ordinal);
        Assert.DoesNotContain(card, result, StringComparison.Ordinal);
        Assert.DoesNotContain(jwt, result, StringComparison.Ordinal);
        Assert.Contains("9999", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Built_in_rules_collection_exposes_the_named_rules()
    {
        Assert.Equal(5, BuiltInRules.All.Count);
        Assert.Contains(BuiltInRules.All, r => r.Name == "email");
        Assert.Contains(BuiltInRules.All, r => r.Name == "credit_card");
        Assert.Contains(BuiltInRules.All, r => r.Name == "iban");
        Assert.Contains(BuiltInRules.All, r => r.Name == "phone");
        Assert.Contains(BuiltInRules.All, r => r.Name == "jwt");
    }

    [Fact]
    public void Built_in_rule_properties_carry_the_expected_names()
    {
        Assert.Equal("email", BuiltInRules.Email.Name);
        Assert.Equal("credit_card", BuiltInRules.CreditCard.Name);
        Assert.Equal("jwt", BuiltInRules.Jwt.Name);
    }
}
