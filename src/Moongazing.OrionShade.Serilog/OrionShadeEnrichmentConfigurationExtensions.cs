namespace Moongazing.OrionShade.Serilog;

using global::Serilog;
using global::Serilog.Configuration;

/// <summary>
/// <c>Enrich</c> configuration helpers that redact the string property values of every log event as it
/// passes through the pipeline. This is the lighter-weight redaction seam: it covers the
/// structured-logging case, where PII reaches a log through a property, with the overhead of an
/// enricher rather than rebuilding the event.
/// </summary>
/// <remarks>
/// An enricher can only touch the mutable property collection of a Serilog event. To also scrub a
/// secret written as a literal in the message template, or carried in an exception message, wrap the
/// sink with <see cref="OrionShadeSinkConfigurationExtensions.OrionShadeRedaction(global::Serilog.Configuration.LoggerSinkConfiguration, IRedactor, Action{global::Serilog.Configuration.LoggerSinkConfiguration})"/>
/// instead, which rebuilds the whole event before any sink sees it.
/// </remarks>
public static class OrionShadeEnrichmentConfigurationExtensions
{
    /// <summary>
    /// Redact each string-typed scalar property of every log event with <paramref name="redactor"/>,
    /// masking the value in the context of its property name. Because the rendered message binds its
    /// placeholders to these property values, this also scrubs the message wherever a secret was
    /// carried through a property.
    /// </summary>
    /// <param name="enrichmentConfiguration">The <c>Enrich</c> configuration to extend.</param>
    /// <param name="redactor">The redactor applied to each string property value.</param>
    /// <returns>The logger configuration, for chaining.</returns>
    public static LoggerConfiguration WithOrionShadeRedaction(
        this LoggerEnrichmentConfiguration enrichmentConfiguration,
        IRedactor redactor)
    {
        ArgumentNullException.ThrowIfNull(enrichmentConfiguration);
        ArgumentNullException.ThrowIfNull(redactor);

        return enrichmentConfiguration.With(new RedactingEnricher(redactor));
    }
}
