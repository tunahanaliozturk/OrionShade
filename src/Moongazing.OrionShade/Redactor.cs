namespace Moongazing.OrionShade;

using System.Text.Json;

using Moongazing.OrionShade.Diagnostics;
using Moongazing.OrionShade.Redaction;

/// <summary>
/// Default <see cref="IRedactor"/>. Applies the configured pattern rules in order to free text, and
/// masks values whose key names appear in the sensitive keyset. Every redaction is counted in
/// telemetry, tagged with the rule that matched.
/// </summary>
public sealed class Redactor : IRedactor
{
    private readonly IReadOnlyList<RedactionRule> rules;
    private readonly SensitiveKeyset sensitiveKeys;
    private readonly Func<string, string> keyMask;
    private readonly ShadeDiagnostics diagnostics;

    /// <summary>Create a redactor.</summary>
    /// <param name="rules">The pattern rules applied to free text.</param>
    /// <param name="sensitiveKeys">The key names whose values are masked wholesale.</param>
    /// <param name="keyMask">The mask used for a sensitive-key value.</param>
    /// <param name="diagnostics">The shared metrics instance.</param>
    public Redactor(
        IReadOnlyList<RedactionRule> rules,
        SensitiveKeyset sensitiveKeys,
        Func<string, string> keyMask,
        ShadeDiagnostics diagnostics)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(sensitiveKeys);
        ArgumentNullException.ThrowIfNull(keyMask);
        ArgumentNullException.ThrowIfNull(diagnostics);
        this.rules = rules;
        this.sensitiveKeys = sensitiveKeys;
        this.keyMask = keyMask;
        this.diagnostics = diagnostics;
    }

    /// <inheritdoc />
    public string Redact(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Length == 0)
        {
            return input;
        }

        var result = input;
        foreach (var rule in rules)
        {
            result = rule.Pattern.Replace(result, match =>
            {
                diagnostics.Record(rule.Name);
                return rule.Mask(match.Value);
            });
        }

        return result;
    }

    /// <inheritdoc />
    public string RedactValue(string key, string value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        if (sensitiveKeys.IsSensitive(key))
        {
            diagnostics.Record("sensitive_key");
            return keyMask(value);
        }

        return Redact(value);
    }

    /// <inheritdoc />
    public string RedactJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (json.Length == 0)
        {
            return json;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            // Not valid JSON: fall back to treating the whole input as free text.
            return Redact(json);
        }

        using (document)
        {
            using var buffer = new MemoryStream();
            using (var writer = new Utf8JsonWriter(buffer))
            {
                WriteRedacted(writer, document.RootElement, ownerKey: null);
            }

            return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
        }
    }

    /// <summary>
    /// Recursively copy <paramref name="element"/> into <paramref name="writer"/>, redacting string
    /// leaves in the context of <paramref name="ownerKey"/> (the property name that owns the value,
    /// or null at the root and for array elements that inherit no distinct key).
    /// </summary>
    private void WriteRedacted(Utf8JsonWriter writer, JsonElement element, string? ownerKey)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject())
                {
                    writer.WritePropertyName(property.Name);
                    WriteRedacted(writer, property.Value, property.Name);
                }

                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    // Array elements inherit the owning key so a sensitive key applied to an array
                    // masks each string element wholesale.
                    WriteRedacted(writer, item, ownerKey);
                }

                writer.WriteEndArray();
                break;

            case JsonValueKind.String:
                var original = element.GetString() ?? string.Empty;
                var redacted = ownerKey is null ? Redact(original) : RedactValue(ownerKey, original);
                writer.WriteStringValue(redacted);
                break;

            default:
                // Numbers, booleans, null, and any other non-string leaf are preserved verbatim.
                element.WriteTo(writer);
                break;
        }
    }
}
