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

    /// <summary>All built-in rules.</summary>
    public static IReadOnlyList<RedactionRule> All { get; } = [Email, CreditCard, Jwt];

    [GeneratedRegex(@"[\w.+-]+@[\w-]+\.[\w.-]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"\b\d{4}[ -]?\d{4}[ -]?\d{4}[ -]?\d{1,4}\b", RegexOptions.CultureInvariant)]
    private static partial Regex CreditCardRegex();

    [GeneratedRegex(@"eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+", RegexOptions.CultureInvariant)]
    private static partial Regex JwtRegex();
}
