namespace Moongazing.OrionShade.Serilog;

using global::Serilog.Core;
using global::Serilog.Events;

/// <summary>
/// An <see cref="ILogEventEnricher"/> that redacts each string-typed scalar property of a
/// <see cref="LogEvent"/> in place, masking the value in the context of its property name. Because the
/// rendered message binds its placeholders to these property values, redacting the properties also
/// scrubs the message text wherever a secret was carried through a property.
/// </summary>
/// <remarks>
/// An enricher runs against the only mutable part of a Serilog event, its property collection, so this
/// seam covers the structured-logging case (the usual way PII reaches a log) with minimal overhead. It
/// cannot reach a secret written as a literal in the message template, nor the exception, because both
/// are fixed when the event is constructed; for those, wrap the sink with
/// <see cref="OrionShadeSinkConfigurationExtensions.OrionShadeRedaction(global::Serilog.Configuration.LoggerSinkConfiguration, IRedactor, Action{global::Serilog.Configuration.LoggerSinkConfiguration})"/>
/// instead, which rebuilds the whole event. Non-string scalars and non-scalar values (structures,
/// sequences, dictionaries) are left untouched.
/// </remarks>
internal sealed class RedactingEnricher : ILogEventEnricher
{
    private readonly IRedactor? redactor;

    /// <summary>Create an enricher that redacts string properties with <paramref name="redactor"/>.</summary>
    /// <param name="redactor">The redactor applied to each string property, or null to do nothing.</param>
    public RedactingEnricher(IRedactor? redactor) => this.redactor = redactor;

    /// <inheritdoc />
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        if (redactor is null)
        {
            return;
        }

        // Snapshot the names first: AddOrUpdateProperty mutates the collection being read otherwise.
        string[]? names = null;
        var count = 0;
        foreach (var (name, value) in logEvent.Properties)
        {
            if (value is ScalarValue { Value: string })
            {
                names ??= new string[logEvent.Properties.Count];
                names[count++] = name;
            }
        }

        if (names is null)
        {
            return;
        }

        for (var i = 0; i < count; i++)
        {
            var name = names[i];
            var text = ((ScalarValue)logEvent.Properties[name]).Value as string;
            var redacted = redactor.RedactValue(name, text!);
            if (!ReferenceEquals(redacted, text) && redacted != text)
            {
                logEvent.AddOrUpdateProperty(new LogEventProperty(name, new ScalarValue(redacted)));
            }
        }
    }
}
