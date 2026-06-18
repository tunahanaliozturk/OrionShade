namespace Moongazing.OrionShade.Tests;

using System.Text.RegularExpressions;

using Moongazing.OrionShade;
using Moongazing.OrionShade.Diagnostics;
using Moongazing.OrionShade.Redaction;

using Xunit;

/// <summary>
/// Covers <see cref="Redactor.Redact"/> over free text: argument handling, the empty-input fast
/// path, clean input passing through untouched, idempotence, and multi-rule application order.
/// </summary>
public sealed class RedactFreeTextTests
{
    private static Redactor Build(ShadeDiagnostics diagnostics) =>
        new(BuiltInRules.All, SensitiveKeyset.Default, Masks.Full(), diagnostics);

    [Fact]
    public void Redact_throws_when_input_is_null()
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        Assert.Throws<ArgumentNullException>(() => redactor.Redact(null!));
    }

    [Fact]
    public void Redact_returns_empty_input_unchanged()
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        Assert.Equal(string.Empty, redactor.Redact(string.Empty));
    }

    [Theory]
    [InlineData("the quick brown fox")]
    [InlineData("order 123 shipped to warehouse 7")]
    [InlineData("status: OK, retries: 3")]
    [InlineData("no at sign here, just text")]
    [InlineData("a partial @ symbol and a lone . dot")]
    public void Redact_leaves_clean_text_unchanged_with_no_false_positives(string clean)
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        Assert.Equal(clean, redactor.Redact(clean));
    }

    [Fact]
    public void Redact_does_not_match_a_short_digit_run_as_a_card()
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        // Only three groups of digits; the card pattern needs four groups.
        const string text = "ref 1234 5678 9012";
        Assert.Equal(text, redactor.Redact(text));
    }

    [Fact]
    public void Redact_is_idempotent_on_already_redacted_text()
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        var once = redactor.Redact("mail a@b.com and card 4111 1111 1111 1234");
        var twice = redactor.Redact(once);

        Assert.Equal(once, twice);
    }

    [Fact]
    public void Redact_applies_rules_in_declaration_order()
    {
        using var diag = new ShadeDiagnostics();
        // First rule blanks the whole string to a constant; the second rule then has nothing to do.
        var rules = new[]
        {
            new RedactionRule("everything", new Regex(".+", RegexOptions.Singleline), _ => "X"),
            new RedactionRule("digits", new Regex(@"\d+"), Masks.Full("#")),
        };
        var redactor = new Redactor(rules, SensitiveKeyset.Default, Masks.Full(), diag);

        Assert.Equal("X", redactor.Redact("anything 123"));
    }

    [Fact]
    public void Redact_with_no_rules_returns_the_input_unchanged()
    {
        using var diag = new ShadeDiagnostics();
        var redactor = new Redactor(
            Array.Empty<RedactionRule>(), SensitiveKeyset.Default, Masks.Full(), diag);

        const string text = "card 4111 1111 1111 1234 email a@b.com";
        Assert.Equal(text, redactor.Redact(text));
    }
}
