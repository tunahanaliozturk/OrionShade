namespace Moongazing.OrionShade.Redaction;

using System.Collections.Frozen;

/// <summary>
/// The set of key names whose values are sensitive regardless of content, matched
/// case-insensitively. Used to redact a value because of the field it belongs to (a
/// <c>password</c> or <c>authorization</c> value) rather than because of a pattern it matches.
/// </summary>
public sealed class SensitiveKeyset
{
    private readonly FrozenSet<string> keys;

    /// <summary>Create a keyset.</summary>
    /// <param name="keys">The sensitive key names.</param>
    public SensitiveKeyset(IEnumerable<string> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        this.keys = keys.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The default sensitive keys: common credential and PII field names.</summary>
    public static SensitiveKeyset Default { get; } = new(
    [
        "password", "passwd", "pwd", "secret", "token", "authorization", "auth",
        "apikey", "api_key", "access_token", "refresh_token", "client_secret",
        "ssn", "creditcard", "credit_card", "cardnumber", "card_number", "cvv", "pin",
    ]);

    /// <summary>Is a key name sensitive?</summary>
    /// <param name="key">The key name.</param>
    public bool IsSensitive(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return keys.Contains(key);
    }
}
