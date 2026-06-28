namespace Moongazing.OrionShade;

using System.Text.RegularExpressions;

using Moongazing.OrionShade.Diagnostics;
using Moongazing.OrionShade.Redaction;

/// <summary>
/// Declares the rules, sensitive keys, and key mask the redactor uses. Start from the built-in
/// defaults with <see cref="UseDefaults"/> or compose your own.
/// </summary>
public sealed class OrionShadeBuilder
{
    private readonly List<RedactionRule> rules = [];
    private readonly List<string> sensitiveKeys = [];
    private Func<string, string> keyMask = Masks.Full();
    private bool useDefaultKeys;

    /// <summary>Add the built-in pattern rules (email, card, JWT) and the default sensitive keys.</summary>
    public OrionShadeBuilder UseDefaults()
    {
        rules.AddRange(BuiltInRules.All);
        useDefaultKeys = true;
        return this;
    }

    /// <summary>Add a pattern rule from a compiled regex.</summary>
    /// <param name="name">The rule name (telemetry tag).</param>
    /// <param name="pattern">The pattern that matches sensitive text.</param>
    /// <param name="mask">How to mask each match. Defaults to a full mask.</param>
    public OrionShadeBuilder AddRule(string name, Regex pattern, Func<string, string>? mask = null)
    {
        rules.Add(new RedactionRule(name, pattern, mask ?? Masks.Full()));
        return this;
    }

    /// <summary>Add a pattern rule from a regex string (compiled culture-invariant, case-insensitive).</summary>
    /// <param name="name">The rule name (telemetry tag).</param>
    /// <param name="pattern">The pattern string.</param>
    /// <param name="mask">How to mask each match. Defaults to a full mask.</param>
    public OrionShadeBuilder AddRule(string name, string pattern, Func<string, string>? mask = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(pattern);
        var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return AddRule(name, regex, mask);
    }

    /// <summary>Mark one or more key names as sensitive (their values are masked wholesale).</summary>
    /// <param name="keys">The key names.</param>
    public OrionShadeBuilder AddSensitiveKeys(params string[] keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        sensitiveKeys.AddRange(keys);
        return this;
    }

    /// <summary>Set the mask used for sensitive-key values. Defaults to a full mask.</summary>
    /// <param name="mask">The mask.</param>
    public OrionShadeBuilder UseKeyMask(Func<string, string> mask)
    {
        ArgumentNullException.ThrowIfNull(mask);
        keyMask = mask;
        return this;
    }

    /// <summary>
    /// Build a standalone <see cref="IRedactor"/> from this configuration, without registering it in a
    /// service container. Useful for composing a named rule set to apply to a specific log category
    /// through <see cref="Logging.LogRedactionOptions.RedactCategory"/>.
    /// </summary>
    /// <param name="diagnostics">
    /// The metrics instance the redactor records to. When null a private <see cref="ShadeDiagnostics"/>
    /// is created; pass the shared registered instance to keep all redaction on one meter.
    /// </param>
    public IRedactor Build(ShadeDiagnostics? diagnostics = null) =>
        new Redactor(BuildRules(), BuildKeyset(), KeyMask, diagnostics ?? new ShadeDiagnostics());

    internal IReadOnlyList<RedactionRule> BuildRules() => rules.ToArray();

    internal SensitiveKeyset BuildKeyset()
    {
        var keys = new List<string>(sensitiveKeys);
        if (useDefaultKeys)
        {
            keys.AddRange(DefaultKeyNames);
        }

        return new SensitiveKeyset(keys);
    }

    internal Func<string, string> KeyMask => keyMask;

    private static readonly string[] DefaultKeyNames =
    [
        "password", "passwd", "pwd", "secret", "token", "authorization", "auth",
        "apikey", "api_key", "access_token", "refresh_token", "client_secret",
        "ssn", "creditcard", "credit_card", "cardnumber", "card_number", "cvv", "pin",
    ];
}
