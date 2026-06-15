namespace Moongazing.OrionShade.Redaction;

/// <summary>
/// Built-in masking strategies. A mask takes the matched sensitive text and returns what should
/// appear in its place.
/// </summary>
public static class Masks
{
    /// <summary>The default replacement token used by <see cref="Full()"/>.</summary>
    public const string DefaultToken = "[REDACTED]";

    /// <summary>Replace the whole value with <see cref="DefaultToken"/>.</summary>
    public static Func<string, string> Full() => _ => DefaultToken;

    /// <summary>Replace the whole value with a fixed token.</summary>
    /// <param name="token">The replacement text.</param>
    public static Func<string, string> Full(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return _ => token;
    }

    /// <summary>
    /// Keep the last <paramref name="visible"/> characters and replace the rest with
    /// <paramref name="maskChar"/>. A value no longer than <paramref name="visible"/> is fully
    /// masked, so a short secret is never left in the clear.
    /// </summary>
    /// <param name="visible">How many trailing characters to keep.</param>
    /// <param name="maskChar">The character to mask with.</param>
    public static Func<string, string> KeepLast(int visible, char maskChar = '*')
    {
        if (visible < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(visible), visible, "visible cannot be negative.");
        }

        return value =>
        {
            if (string.IsNullOrEmpty(value) || value.Length <= visible)
            {
                return new string(maskChar, value?.Length ?? 0);
            }

            var masked = new string(maskChar, value.Length - visible);
            return masked + value[^visible..];
        };
    }
}
