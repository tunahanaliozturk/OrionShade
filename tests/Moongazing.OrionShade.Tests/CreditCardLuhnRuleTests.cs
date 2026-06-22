namespace Moongazing.OrionShade.Tests;

using System.Diagnostics.Metrics;

using Moongazing.OrionShade;
using Moongazing.OrionShade.Diagnostics;
using Moongazing.OrionShade.Redaction;

using Xunit;

/// <summary>
/// Exercises the Luhn gate on the credit-card rule added in 0.3.0: a digit run that passes the Luhn
/// checksum is masked keeping the last four digits, while a run of the same shape that fails the
/// checksum (an order id, a reference number) is left untouched and is not counted in telemetry.
/// </summary>
[Collection(nameof(MeterSerial))]
public sealed class CreditCardLuhnRuleTests
{
    private static Redactor Build(ShadeDiagnostics diagnostics) =>
        new(BuiltInRules.All, SensitiveKeyset.Default, Masks.Full(), diagnostics);

    [Theory]
    [InlineData("4242 4242 4242 4242", "4242")] // Visa test PAN
    [InlineData("4242-4242-4242-4242", "4242")]
    [InlineData("4242424242424242", "4242")]
    [InlineData("5555 5555 5555 4444", "4444")] // Mastercard test PAN
    [InlineData("4111 1111 1111 1111", "1111")] // Visa test PAN
    public void Luhn_valid_card_is_masked_keeping_the_last_four(string card, string lastFour)
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        var result = redactor.Redact($"charge to {card} now");

        Assert.DoesNotContain(card, result, StringComparison.Ordinal);
        Assert.Contains('*', result);
        Assert.EndsWith($"{lastFour} now", result, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("1234 5678 9012 3456")] // arbitrary 16-digit order number, fails Luhn
    [InlineData("4111 1111 1111 1234")] // looks card-shaped but the checksum is wrong
    [InlineData("0000 0000 0000 0001")] // fails Luhn
    public void A_non_luhn_digit_run_is_left_untouched(string notACard)
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        var text = $"order {notACard} confirmed";
        Assert.Equal(text, redactor.Redact(text));
    }

    [Fact]
    public void A_non_luhn_run_is_not_counted_in_telemetry()
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        // The card pattern matches this run as a candidate, but the Luhn gate declines it, so no
        // redaction is recorded: telemetry must reflect what was masked, not what was examined.
        var measurements = Collect(diag, () => redactor.Redact("order 1234 5678 9012 3456 confirmed"));

        Assert.Empty(measurements);
    }

    [Fact]
    public void A_luhn_valid_card_is_counted_once_tagged_credit_card()
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        var measurements = Collect(diag, () => redactor.Redact("charge 4242 4242 4242 4242 today"));

        var measurement = Assert.Single(measurements);
        Assert.Equal("credit_card", measurement.Rule);
    }

    [Fact]
    public void The_card_rule_carries_the_expected_name()
    {
        Assert.Equal("credit_card", BuiltInRules.CreditCard.Name);
    }

    // Runs an action while listening to a diagnostics instance's redaction counter and returns the
    // measurements observed, filtered by instrument identity because several test classes run in
    // parallel against meters sharing ShadeDiagnostics.MeterName.
    private static List<(long Value, string? Rule)> Collect(ShadeDiagnostics diag, Action act)
    {
        var measurements = new List<(long, string?)>();
        var target = diag.Redactions;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (ReferenceEquals(instrument, target))
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, state) =>
        {
            if (!ReferenceEquals(instrument, target))
            {
                return;
            }

            string? rule = null;
            foreach (var tag in tags)
            {
                if (tag.Key == "rule")
                {
                    rule = tag.Value as string;
                }
            }

            measurements.Add((value, rule));
        });
        listener.Start();

        act();

        listener.Dispose();
        return measurements;
    }
}
