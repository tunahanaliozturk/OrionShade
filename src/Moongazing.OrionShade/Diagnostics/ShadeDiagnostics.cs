namespace Moongazing.OrionShade.Diagnostics;

using System.Diagnostics.Metrics;
using System.Reflection;

using Moongazing.Orion.Abstractions.Diagnostics;

/// <summary>
/// Derives the diagnostics meter version from the assembly informational version so it never drifts
/// from the package version.
/// </summary>
internal static class MeterVersion
{
    /// <summary>The resolved meter version (the package version without any build metadata).</summary>
    public static string Value { get; } = Resolve();

    private static string Resolve()
    {
        var asm = typeof(MeterVersion).Assembly;
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrEmpty(info))
        {
            var plus = info.IndexOf('+');
            return plus >= 0 ? info[..plus] : info;
        }

        return asm.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}

/// <summary>
/// OpenTelemetry instrumentation for redaction. Built on the Orion family's
/// <see cref="OrionInstrumentation"/> spine, so it shares the family's naming and static-tag
/// conventions: a <see cref="Meter"/> named <c>Moongazing.OrionShade</c> (subscribe by that name)
/// exposing the redaction counter <c>orion.shade.redactions</c>, tagged by rule. Multi-tenant /
/// multi-region labels configured through <see cref="OrionInstrumentation.SetStaticTags"/> are
/// stamped onto every measurement. Registered as a singleton; dispose to release the meter.
/// </summary>
public sealed class ShadeDiagnostics : OrionInstrumentation
{
    /// <summary>The meter name OpenTelemetry consumers subscribe to.</summary>
    public const string MeterName = "Moongazing.OrionShade";

    /// <summary>Create the meter and its instruments.</summary>
    public ShadeDiagnostics()
        : base(OrionTelemetry.ScopeName("OrionShade"), MeterVersion.Value)
    {
        Redactions = Meter.CreateCounter<long>(
            OrionTelemetry.MetricName("shade", "redactions"),
            unit: "{redaction}",
            description: "Sensitive values redacted, tagged rule (the pattern name or 'sensitive_key').");
    }

    /// <summary>Counts redactions by rule.</summary>
    public Counter<long> Redactions { get; }

    /// <summary>Record one redaction.</summary>
    /// <param name="rule">The rule that matched.</param>
    public void Record(string rule) =>
        Redactions.Add(1, Tag(new KeyValuePair<string, object?>("rule", rule)));
}
