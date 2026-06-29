namespace Moongazing.OrionShade.Serilog;

using global::Serilog.Events;
using global::Serilog.Parsing;

/// <summary>
/// Rebuilds a Serilog <see cref="LogEvent"/> with its sensitive text scrubbed through an
/// <see cref="IRedactor"/>: the literal segments of the message template, every string-typed scalar
/// property value (at the top level and nested inside structures, sequences, and dictionaries), and
/// the rendered text of any attached exception. Numbers, booleans, and other non-string scalars are
/// left untouched, the shape of nested values (type tag, element order, keys) is preserved, and the
/// message template's property placeholders are kept verbatim so the event still renders against the
/// (now redacted) property values.
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
    /// Redact each property value in the context of its property name, recursing into nested
    /// structures, sequences, and dictionaries so a secret carried inside a destructured object,
    /// a collection element, or a dictionary entry is scrubbed at every depth.
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
    /// Redact a single property value, in the context of the key <paramref name="name"/> it belongs to.
    /// A string scalar is masked (a sensitive key name masks the whole value, any other value runs
    /// through the pattern rules). A <see cref="StructureValue"/>, <see cref="SequenceValue"/>, or
    /// <see cref="DictionaryValue"/> is walked recursively, its leaves redacted and its shape (type tag,
    /// element order, keys) rebuilt unchanged. Non-string scalars are returned as-is. The original
    /// instance is returned whenever nothing inside it changed, so a clean value allocates nothing.
    /// </summary>
    private static LogEventPropertyValue RedactValue(string name, LogEventPropertyValue value, IRedactor redactor)
    {
        switch (value)
        {
            case ScalarValue { Value: string text }:
            {
                var redacted = redactor.RedactValue(name, text);
                return ReferenceEquals(redacted, text) || redacted == text ? value : new ScalarValue(redacted);
            }

            case StructureValue structure:
            {
                List<LogEventProperty>? rebuilt = null;
                var properties = structure.Properties;
                for (var i = 0; i < properties.Count; i++)
                {
                    var property = properties[i];
                    var redacted = RedactValue(property.Name, property.Value, redactor);
                    if (!ReferenceEquals(redacted, property.Value))
                    {
                        rebuilt ??= new List<LogEventProperty>(properties);
                        rebuilt[i] = new LogEventProperty(property.Name, redacted);
                    }
                }

                return rebuilt is null ? value : new StructureValue(rebuilt, structure.TypeTag);
            }

            case SequenceValue sequence:
            {
                List<LogEventPropertyValue>? rebuilt = null;
                var elements = sequence.Elements;
                for (var i = 0; i < elements.Count; i++)
                {
                    var element = elements[i];

                    // A sequence element has no key of its own, so it is redacted in the context of the
                    // sequence's property name.
                    var redacted = RedactValue(name, element, redactor);
                    if (!ReferenceEquals(redacted, element))
                    {
                        rebuilt ??= new List<LogEventPropertyValue>(elements);
                        rebuilt[i] = redacted;
                    }
                }

                return rebuilt is null ? value : new SequenceValue(rebuilt);
            }

            case DictionaryValue dictionary:
            {
                List<KeyValuePair<ScalarValue, LogEventPropertyValue>>? rebuilt = null;
                var index = 0;
                foreach (var (key, element) in dictionary.Elements)
                {
                    // The dictionary key names the entry, so its value is redacted in the key's context;
                    // a string key is itself scrubbed through the pattern rules.
                    var redactedKey = key.Value is string keyText
                        ? RedactKey(keyText, key, redactor)
                        : key;
                    var keyName = key.Value as string ?? name;
                    var redactedValue = RedactValue(keyName, element, redactor);

                    if (!ReferenceEquals(redactedKey, key) || !ReferenceEquals(redactedValue, element))
                    {
                        rebuilt ??= new List<KeyValuePair<ScalarValue, LogEventPropertyValue>>(dictionary.Elements);
                        rebuilt[index] = new KeyValuePair<ScalarValue, LogEventPropertyValue>(redactedKey, redactedValue);
                    }

                    index++;
                }

                return rebuilt is null ? value : new DictionaryValue(rebuilt);
            }

            default:
                return value;
        }
    }

    /// <summary>
    /// Redact a string dictionary key through the pattern rules, returning the original
    /// <see cref="ScalarValue"/> when nothing matched so an unchanged key allocates nothing.
    /// </summary>
    private static ScalarValue RedactKey(string keyText, ScalarValue key, IRedactor redactor)
    {
        var redacted = redactor.Redact(keyText);
        return ReferenceEquals(redacted, keyText) || redacted == keyText ? key : new ScalarValue(redacted);
    }
}
