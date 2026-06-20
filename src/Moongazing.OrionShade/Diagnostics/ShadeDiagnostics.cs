namespace Moongazing.OrionShade.Diagnostics;

using System.Diagnostics.Metrics;
using System.Reflection;

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
/// OpenTelemetry instrumentation for redaction. Exposes a <see cref="Meter"/> named
/// <c>Moongazing.OrionShade</c> with a rule-tagged redaction counter, so you can see which kinds of
/// sensitive data are being caught and how often. Registered as a singleton; dispose to release the
/// meter.
/// </summary>
public sealed class ShadeDiagnostics : IDisposable
{
    /// <summary>The meter name OpenTelemetry consumers subscribe to.</summary>
    public const string MeterName = "Moongazing.OrionShade";

    private readonly Meter meter;

    /// <summary>Create the meter and its instruments.</summary>
    public ShadeDiagnostics()
    {
        meter = new Meter(MeterName, MeterVersion.Value);
        Redactions = meter.CreateCounter<long>(
            "orionshade.redactions",
            unit: "{redaction}",
            description: "Sensitive values redacted, tagged rule (the pattern name or 'sensitive_key').");
    }

    /// <summary>Counts redactions by rule.</summary>
    public Counter<long> Redactions { get; }

    /// <summary>Record one redaction.</summary>
    /// <param name="rule">The rule that matched.</param>
    public void Record(string rule) =>
        Redactions.Add(1, new KeyValuePair<string, object?>("rule", rule));

    /// <inheritdoc />
    public void Dispose() => meter.Dispose();
}
