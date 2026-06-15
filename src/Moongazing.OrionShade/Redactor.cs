namespace Moongazing.OrionShade;

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
}
