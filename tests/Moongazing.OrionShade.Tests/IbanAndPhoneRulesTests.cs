namespace Moongazing.OrionShade.Tests;

using Moongazing.OrionShade;
using Moongazing.OrionShade.Diagnostics;
using Moongazing.OrionShade.Redaction;

using Xunit;

/// <summary>
/// Exercises the IBAN and phone built-in pattern rules added in 0.2.0: IBANs are masked entirely,
/// phone numbers keep only their last two digits, and ordinary text is left untouched.
/// </summary>
[Collection(nameof(MeterSerial))]
public sealed class IbanAndPhoneRulesTests
{
    private static Redactor Build(ShadeDiagnostics diagnostics) =>
        new(BuiltInRules.All, SensitiveKeyset.Default, Masks.Full(), diagnostics);

    [Theory]
    [InlineData("GB82 WEST 1234 5698 7654 32")]
    [InlineData("GB82WEST12345698765432")]
    [InlineData("DE89 3704 0044 0532 0130 00")]
    [InlineData("FR14 2004 1010 0505 0001 3M02 606")]
    public void Iban_rule_masks_account_numbers_entirely(string iban)
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        var result = redactor.Redact($"transfer to {iban} today");

        Assert.DoesNotContain(iban, result, StringComparison.Ordinal);
        Assert.Contains(Masks.DefaultToken, result, StringComparison.Ordinal);
    }

    [Fact]
    public void Iban_rule_carries_the_expected_name()
    {
        Assert.Equal("iban", BuiltInRules.Iban.Name);
    }

    [Theory]
    [InlineData("+1 415 555 0132")]
    [InlineData("+90 532 123 45 67")]
    [InlineData("+44 20 7946 0958")]
    public void Phone_rule_masks_the_number_keeping_the_last_two_digits(string phone)
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        var result = redactor.Redact($"call me on {phone} please");

        Assert.DoesNotContain(phone, result, StringComparison.Ordinal);
        // KeepLast(2) preserves the final two characters of the matched run.
        Assert.EndsWith("please", result, StringComparison.Ordinal);
        Assert.Contains(phone[^2..], result, StringComparison.Ordinal);
    }

    [Fact]
    public void Phone_rule_carries_the_expected_name()
    {
        Assert.Equal("phone", BuiltInRules.Phone.Name);
    }

    [Fact]
    public void Non_matching_text_is_left_untouched()
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        const string clean = "the package shipped on time and arrived safely";
        Assert.Equal(clean, redactor.Redact(clean));
    }

    [Fact]
    public void Short_incidental_number_runs_are_not_treated_as_phone_numbers()
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        // A short order id is below the phone-length threshold and must survive untouched.
        const string text = "order 12345 confirmed";
        Assert.Equal(text, redactor.Redact(text));
    }

    [Fact]
    public void The_default_rule_set_includes_iban_and_phone()
    {
        Assert.Contains(BuiltInRules.All, r => r.Name == "iban");
        Assert.Contains(BuiltInRules.All, r => r.Name == "phone");
        Assert.Equal(5, BuiltInRules.All.Count);
    }
}
