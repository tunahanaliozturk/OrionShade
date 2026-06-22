namespace Moongazing.OrionShade.Tests;

using System.Text.RegularExpressions;

using Moongazing.OrionShade;
using Moongazing.OrionShade.Diagnostics;
using Moongazing.OrionShade.Redaction;

using Xunit;

[Collection(nameof(MeterSerial))]
public sealed class RedactorTests
{
    private static Redactor Build(ShadeDiagnostics diagnostics) =>
        new(BuiltInRules.All, SensitiveKeyset.Default, Masks.Full(), diagnostics);

    [Fact]
    public void It_masks_an_email_in_free_text()
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        var result = redactor.Redact("contact me at jane.doe@example.com please");

        Assert.DoesNotContain("jane.doe@example.com", result, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", result, StringComparison.Ordinal);
    }

    [Fact]
    public void It_keeps_the_last_four_digits_of_a_card()
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        // A genuine Luhn-valid PAN (Visa test number); the card rule masks all but the last four.
        var result = redactor.Redact("card 4242 4242 4242 4242 on file");

        Assert.DoesNotContain("4242 4242 4242 4242", result, StringComparison.Ordinal);
        Assert.EndsWith("4242 on file", result, StringComparison.Ordinal);
        Assert.StartsWith("card *", result, StringComparison.Ordinal);
    }

    [Fact]
    public void It_masks_a_jwt()
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);
        const string jwt = "eyJhbGc.eyJzdWIiOiIxMjM0.SflKxwRJSMeKKF2QT4";

        var result = redactor.Redact($"token={jwt}");

        Assert.DoesNotContain(jwt, result, StringComparison.Ordinal);
    }

    [Fact]
    public void It_leaves_non_sensitive_text_unchanged()
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        const string clean = "the quick brown fox";
        Assert.Equal(clean, redactor.Redact(clean));
    }

    [Fact]
    public void A_sensitive_key_masks_the_whole_value()
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        Assert.Equal("[REDACTED]", redactor.RedactValue("password", "hunter2"));
        Assert.Equal("[REDACTED]", redactor.RedactValue("Authorization", "Bearer abc"));
    }

    [Fact]
    public void A_non_sensitive_key_still_runs_pattern_rules_on_the_value()
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        var result = redactor.RedactValue("note", "reach me at a@b.com");

        Assert.DoesNotContain("a@b.com", result, StringComparison.Ordinal);
    }

    [Fact]
    public void A_custom_rule_with_a_partial_mask_is_applied()
    {
        using var diag = new ShadeDiagnostics();
        var rules = new[] { new RedactionRule("phone", new Regex(@"\d{10}"), Masks.KeepLast(2)) };
        var redactor = new Redactor(rules, SensitiveKeyset.Default, Masks.Full(), diag);

        var result = redactor.Redact("call 5551234567 now");

        Assert.Contains("********67", result, StringComparison.Ordinal);
    }
}
