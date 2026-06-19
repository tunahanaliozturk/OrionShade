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

    /// <summary>Masks card-like digit runs, keeping the last four digits.</summary>
    public static RedactionRule CreditCard { get; } = new("credit_card", CreditCardRegex(), Masks.KeepLast(4));

    /// <summary>Masks JSON Web Tokens entirely.</summary>
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
    /// All built-in rules. The IBAN and phone rules are matched before the credit-card rule so a more
    /// specific anchored pattern claims its digits first: IBAN by its country-code prefix, and phone by
    /// its leading <c>+</c>. Without this, the credit-card rule would partially consume a compact
    /// <c>+</c>-prefixed international number and leave its trailing digits visible.
    /// </summary>
    public static IReadOnlyList<RedactionRule> All { get; } = [Email, Iban, Phone, CreditCard, Jwt];

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
}
