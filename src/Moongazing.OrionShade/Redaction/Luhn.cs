namespace Moongazing.OrionShade.Redaction;

/// <summary>
/// The Luhn (mod 10) checksum used by payment card numbers. Used to tell a real card number from an
/// arbitrary digit run of the same length (an order id, a reference number) so the credit-card rule
/// only masks candidates that could actually be cards.
/// </summary>
internal static class Luhn
{
    /// <summary>
    /// Is the digit content of <paramref name="candidate"/> a valid Luhn sequence? Non-digit
    /// characters (spaces and hyphens used as card-group separators) are ignored. A candidate with
    /// fewer than <paramref name="minDigits"/> digits is rejected, so short runs are never treated as
    /// cards even if their checksum happens to pass.
    /// </summary>
    /// <param name="candidate">The matched text, which may contain space or hyphen separators.</param>
    /// <param name="minDigits">The fewest digits a candidate must have to be considered. Defaults to
    /// 12, below the 13-digit floor of real card numbers, so genuine cards always qualify while short
    /// incidental runs do not.</param>
    public static bool IsValid(string candidate, int minDigits = 12)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var sum = 0;
        var digitCount = 0;
        var takeDoubled = false;

        // Walk from the rightmost character so the doubling alternation is anchored at the check
        // digit, independent of how many separators sit between groups.
        for (var i = candidate.Length - 1; i >= 0; i--)
        {
            var c = candidate[i];
            if (c is < '0' or > '9')
            {
                continue;
            }

            var digit = c - '0';
            if (takeDoubled)
            {
                digit *= 2;
                if (digit > 9)
                {
                    digit -= 9;
                }
            }

            sum += digit;
            takeDoubled = !takeDoubled;
            digitCount++;
        }

        return digitCount >= minDigits && sum % 10 == 0;
    }
}
