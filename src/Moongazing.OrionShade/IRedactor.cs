namespace Moongazing.OrionShade;

/// <summary>
/// Redacts sensitive data before it is logged or persisted: pattern matches inside free text, and
/// values whose key names are known to be sensitive.
/// </summary>
public interface IRedactor
{
    /// <summary>
    /// Apply every configured pattern rule to a string, masking each match. Returns the input
    /// unchanged when nothing matches.
    /// </summary>
    /// <param name="input">The text to redact.</param>
    string Redact(string input);

    /// <summary>
    /// Redact a value in the context of its key. When the key name is sensitive the whole value is
    /// masked; otherwise the value is passed through the pattern rules.
    /// </summary>
    /// <param name="key">The field name the value belongs to.</param>
    /// <param name="value">The value to redact.</param>
    string RedactValue(string key, string value);

    /// <summary>
    /// Redact a JSON document, preserving its structure. Recurses through nested objects and arrays;
    /// each string value is redacted in the context of the property name that owns it (so a sensitive
    /// key masks its value wholesale, and any other string is passed through the pattern rules).
    /// Non-string values (numbers, booleans, null) are left untouched. When the input is not valid
    /// JSON it is treated as free text and passed through <see cref="Redact"/> instead.
    /// </summary>
    /// <param name="json">The JSON document to redact.</param>
    string RedactJson(string json);
}
