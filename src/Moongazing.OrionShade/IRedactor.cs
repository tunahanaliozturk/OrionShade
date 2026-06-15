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
}
