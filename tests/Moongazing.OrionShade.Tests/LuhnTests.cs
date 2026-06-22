namespace Moongazing.OrionShade.Tests;

using Moongazing.OrionShade.Redaction;

using Xunit;

/// <summary>
/// Unit tests for the internal <see cref="Luhn"/> checksum used by the credit-card rule: valid card
/// numbers pass, altered numbers and arbitrary runs fail, separators are ignored, and a run shorter
/// than the minimum digit count is rejected even when its checksum would otherwise pass.
/// </summary>
public sealed class LuhnTests
{
    [Theory]
    [InlineData("4242424242424242")] // Visa test PAN
    [InlineData("4242 4242 4242 4242")] // separators ignored
    [InlineData("4242-4242-4242-4242")]
    [InlineData("5555555555554444")] // Mastercard test PAN
    [InlineData("4111111111111111")] // Visa test PAN
    [InlineData("378282246310005")] // Amex test PAN (15 digits)
    public void Valid_card_numbers_pass(string candidate)
    {
        Assert.True(Luhn.IsValid(candidate));
    }

    [Theory]
    [InlineData("4242424242424243")] // last digit changed
    [InlineData("1234567890123456")] // arbitrary 16-digit run
    [InlineData("4111111111111234")] // card-shaped but wrong checksum
    public void Invalid_runs_fail(string candidate)
    {
        Assert.False(Luhn.IsValid(candidate));
    }

    [Fact]
    public void A_short_run_is_rejected_by_the_digit_floor_even_when_its_checksum_passes()
    {
        // "00" sums to zero, so the modulus alone would accept it. The default 12-digit floor rejects
        // it, which is what stops a short incidental number from being treated as a card.
        Assert.False(Luhn.IsValid("00"));

        // Lower the floor and the same run is accepted purely on its checksum, proving the floor (not
        // the modulus) is what rejected it above.
        Assert.True(Luhn.IsValid("00", minDigits: 2));
    }

    [Fact]
    public void Empty_input_is_not_valid()
    {
        Assert.False(Luhn.IsValid(string.Empty));
    }
}
