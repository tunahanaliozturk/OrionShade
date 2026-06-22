namespace Moongazing.OrionShade.Redaction;

using System.Text.RegularExpressions;

/// <summary>
/// A set of ready-made redaction rules for the most common sensitive values. The patterns are
/// source-generated for speed and are deliberately conservative: they aim to catch obvious leaks in
/// logs, not to be a complete data-loss-prevention engine.
/// </summary>
public static partial class BuiltInRules
{
    /// <summary>Masks email addresses entirely.</summary>
    public static RedactionRule Email { get; } = new("email", EmailRegex(), Masks.Full());

    /// <summary>
    /// Masks card-like digit runs, keeping the last four digits, but only when the run is a valid
    /// Luhn (mod 10) sequence. A 13-to-16 digit run that fails the checksum (an order id, a reference
    /// number) is left untouched, so the rule does not mask arbitrary long digit runs.
    /// </summary>
    public static RedactionRule CreditCard { get; } = new("credit_card", CreditCardRegex(), LuhnKeepLast(4));

    /// <summary>Masks JSON Web Tokens entirely. Matches the <c>eyJ</c>-prefixed three-segment base64url shape.</summary>
    public static RedactionRule Jwt { get; } = new("jwt", JwtRegex(), Masks.Full());

    /// <summary>
    /// Masks IBAN bank account numbers entirely. Matches the ISO 13616 shape of a two-letter country
    /// code, two check digits, and an alphanumeric account body, optionally written in space- or
    /// hyphen-separated groups.
    /// </summary>
    public static RedactionRule Iban { get; } = new("iban", IbanRegex(), Masks.Full());

    /// <summary>
    /// Masks phone numbers written in international form, keeping the last two digits. Requires a
    /// leading <c>+</c> country prefix so ordinary grouped digit runs (order ids, quantities,
    /// reference numbers) are not mistaken for phone numbers.
    /// </summary>
    public static RedactionRule Phone { get; } = new("phone", PhoneRegex(), Masks.KeepLast(2));

    /// <summary>
    /// Masks the secret value of a connection-string credential pair while leaving the key visible.
    /// Covers the common secret-bearing keys <c>Password</c>, <c>Pwd</c>, <c>AccountKey</c>,
    /// <c>SharedAccessKey</c>, and <c>Secret</c>, matched case-insensitively. The value runs to the
    /// next <c>;</c> delimiter or the end of the text, so the surrounding connection string stays
    /// readable (for example <c>Server=db;Password=[REDACTED];Database=app</c>).
    /// </summary>
    public static RedactionRule ConnectionStringSecret { get; } =
        new("connection_string_secret", ConnectionStringSecretRegex(), MaskValueAfterEquals());

    /// <summary>
    /// All built-in rules, in application order. The IBAN and phone rules run before the credit-card
    /// rule so a more specific anchored pattern claims its digits first: IBAN by its country-code
    /// prefix, and phone by its leading <c>+</c>. Without this, the credit-card rule would partially
    /// consume a compact <c>+</c>-prefixed international number and leave its trailing digits visible.
    /// The connection-string rule runs first so a secret value is masked as a whole before any inner
    /// pattern (a card-like or JWT-like fragment inside the secret) could partially rewrite it.
    /// </summary>
    public static IReadOnlyList<RedactionRule> All { get; } =
        [ConnectionStringSecret, Email, Iban, Phone, CreditCard, Jwt];

    /// <summary>
    /// A mask that keeps the last <paramref name="visible"/> characters only when the matched run is a
    /// valid Luhn sequence; a run that fails the checksum is returned unchanged so the redactor leaves
    /// it in the clear and does not count it.
    /// </summary>
    private static Func<string, string> LuhnKeepLast(int visible)
    {
        var keepLast = Masks.KeepLast(visible);
        return value => Luhn.IsValid(value) ? keepLast(value) : value;
    }

    /// <summary>
    /// A mask for a captured <c>key=value</c> pair: keep everything up to and including the first
    /// <c>=</c> (the key and separator) and replace the value with the default token. The value's own
    /// characters after the first <c>=</c> (for example base64 padding in an account key) are part of
    /// the masked portion.
    /// </summary>
    private static Func<string, string> MaskValueAfterEquals() => match =>
    {
        var separator = match.IndexOf('=');
        if (separator < 0)
        {
            // The pattern always includes an '=', so this is unreachable in practice; mask whole to be safe.
            return Masks.DefaultToken;
        }

        return string.Concat(match.AsSpan(0, separator + 1), Masks.DefaultToken);
    };

    [GeneratedRegex(@"[\w.+-]+@[\w-]+\.[\w.-]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"\b\d{4}[ -]?\d{4}[ -]?\d{4}[ -]?\d{1,4}\b", RegexOptions.CultureInvariant)]
    private static partial Regex CreditCardRegex();

    [GeneratedRegex(@"eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+", RegexOptions.CultureInvariant)]
    private static partial Regex JwtRegex();

    [GeneratedRegex(@"\b[A-Z]{2}\d{2}(?:[ -]?[A-Z0-9]){11,30}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IbanRegex();

    [GeneratedRegex(@"\+\d[\d -]{7,15}\d", RegexOptions.CultureInvariant)]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(
        @"\b(?:password|pwd|accountkey|sharedaccesskey|secret)\s*=\s*[^;]*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ConnectionStringSecretRegex();
}
