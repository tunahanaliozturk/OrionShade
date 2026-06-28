namespace Moongazing.OrionShade.Serilog;

using global::Serilog.Events;
using global::Serilog.Parsing;

/// <summary>
/// Rebuilds a Serilog <see cref="LogEvent"/> with its sensitive text scrubbed through an
/// <see cref="IRedactor"/>: the literal segments of the message template, every string-typed scalar
/// property value, and the rendered text of any attached exception. Numbers, booleans, and other
/// non-string scalars are left untouched, and the message template's property placeholders are kept
/// verbatim so the event still renders against the (now redacted) property values.
/// </summary>
/// <remarks>
/// A Serilog <see cref="LogEvent"/> is immutable in its message template and exception (both are set
/// at construction), so redaction cannot mutate the event in place; this builder produces a new event
/// instead. Redaction is applied at two layers so the rendered message is clean whatever the source of
/// the secret: a value interpolated through a property is scrubbed by the property pass, and a secret
/// written as a literal in the template text is scrubbed by the template pass. The exception is wrapped
/// in a <see cref="OrionShade.Logging.RedactedException"/> from the core, so a sink that renders the
/// exception writes the scrubbed form while the type name and stack frames survive.
/// </remarks>
internal static class LogEventRedaction
{
    /// <summary>
    /// Produce a copy of <paramref name="logEvent"/> with its message-template literals, string scalar
    /// properties, and exception text redacted. Returns the original event when
    /// <paramref name="redactor"/> is null, so an unconfigured pipeline logs unchanged.
    /// </summary>
    /// <param name="logEvent">The event to scrub.</param>
    /// <param name="redactor">The redactor to apply, or null to pass the event through unchanged.</param>
    public static LogEvent Redact(LogEvent logEvent, IRedactor? redactor)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        if (redactor is null)
        {
            return logEvent;
        }

        var template = RedactTemplate(logEvent.MessageTemplate, redactor);
        var properties = RedactProperties(logEvent.Properties, redactor);
        var exception = logEvent.Exception is { } ex
            ? new OrionShade.Logging.RedactedException(ex, redactor)
            : null;

        return new LogEvent(
            logEvent.Timestamp,
            logEvent.Level,
            exception,
            template,
            properties,
            logEvent.TraceId ?? default,
            logEvent.SpanId ?? default);
    }

    /// <summary>
    /// Rebuild a message template with each literal <see cref="TextToken"/> redacted and every
    /// <see cref="PropertyToken"/> kept verbatim, so a secret written into the template text is masked
    /// while the placeholders still bind to the redacted property values at render time.
    /// </summary>
    private static MessageTemplate RedactTemplate(MessageTemplate template, IRedactor redactor)
    {
        List<MessageTemplateToken>? rebuilt = null;
        var index = 0;
        foreach (var token in template.Tokens)
        {
            if (token is TextToken text)
            {
                var redacted = redactor.Redact(text.Text);
                if (!ReferenceEquals(redacted, text.Text) && redacted != text.Text)
                {
                    rebuilt ??= new List<MessageTemplateToken>(template.Tokens);
                    rebuilt[index] = new TextToken(redacted);
                }
            }

            index++;
        }

        // No literal token changed: keep the original template instance so a clean line allocates nothing.
        return rebuilt is null ? template : new MessageTemplate(rebuilt);
    }

    /// <summary>
    /// Redact each string-typed scalar property value in the context of its property name, leaving
    /// every other property (non-string scalars, structures, sequences, dictionaries) untouched.
    /// </summary>
    private static List<LogEventProperty> RedactProperties(
        IReadOnlyDictionary<string, LogEventPropertyValue> properties,
        IRedactor redactor)
    {
        var result = new List<LogEventProperty>(properties.Count);
        foreach (var (name, value) in properties)
        {
            result.Add(new LogEventProperty(name, RedactValue(name, value, redactor)));
        }

        return result;
    }

    /// <summary>
    /// Redact a single property value: a string scalar is masked in the context of its key (so a
    /// sensitive key name masks the whole value, any other value runs through the pattern rules);
    /// every other value is returned unchanged.
    /// </summary>
    private static LogEventPropertyValue RedactValue(string name, LogEventPropertyValue value, IRedactor redactor)
    {
        if (value is ScalarValue { Value: string text })
        {
            var redacted = redactor.RedactValue(name, text);
            return ReferenceEquals(redacted, text) || redacted == text ? value : new ScalarValue(redacted);
        }

        return value;
    }
}
