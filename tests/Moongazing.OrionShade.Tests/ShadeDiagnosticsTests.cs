namespace Moongazing.OrionShade.Tests;

using System.Diagnostics.Metrics;

using Moongazing.OrionShade;
using Moongazing.OrionShade.Diagnostics;
using Moongazing.OrionShade.Redaction;

using Xunit;

/// <summary>
/// Covers <see cref="ShadeDiagnostics"/> and its integration with the redactor: the meter name, the
/// redaction counter, and the rule tag attached to each recorded redaction.
/// </summary>
public sealed class ShadeDiagnosticsTests
{
    [Fact]
    public void Meter_name_is_the_published_constant()
    {
        Assert.Equal("Moongazing.OrionShade", ShadeDiagnostics.MeterName);
    }

    [Fact]
    public void Record_increments_the_counter_with_the_rule_tag()
    {
        using var diag = new ShadeDiagnostics();
        var measurements = Collect(diag, () => diag.Record("email"));

        var measurement = Assert.Single(measurements);
        Assert.Equal(1, measurement.Value);
        Assert.Equal("email", measurement.Rule);
    }

    [Fact]
    public void Redact_records_one_measurement_per_match_tagged_with_the_rule_name()
    {
        using var diag = new ShadeDiagnostics();
        var redactor = new Redactor(BuiltInRules.All, SensitiveKeyset.Default, Masks.Full(), diag);

        var measurements = Collect(diag, () => redactor.Redact("mail a@b.com and c@d.org"));

        Assert.Equal(2, measurements.Count);
        Assert.All(measurements, m => Assert.Equal("email", m.Rule));
    }

    [Fact]
    public void RedactValue_records_a_sensitive_key_measurement()
    {
        using var diag = new ShadeDiagnostics();
        var redactor = new Redactor(BuiltInRules.All, SensitiveKeyset.Default, Masks.Full(), diag);

        var measurements = Collect(diag, () => redactor.RedactValue("password", "hunter2"));

        var measurement = Assert.Single(measurements);
        Assert.Equal("sensitive_key", measurement.Rule);
    }

    [Fact]
    public void Dispose_can_be_called_more_than_once_safely()
    {
        var diag = new ShadeDiagnostics();
        diag.Dispose();
        diag.Dispose();
    }

    private static List<(long Value, string? Rule)> Collect(ShadeDiagnostics diag, Action act)
    {
        var measurements = new List<(long, string?)>();

        // Filter to this diagnostics instance's exact counter instrument. Several test classes run
        // in parallel and all create a meter named ShadeDiagnostics.MeterName, so filtering by name
        // alone would capture stray measurements from a concurrent redactor. Identity does not.
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
