namespace Moongazing.OrionShade.Tests;

using Moongazing.OrionShade;
using Moongazing.OrionShade.Diagnostics;
using Moongazing.OrionShade.Redaction;

using Xunit;

/// <summary>
/// Exercises the connection-string secret rule added in 0.3.0: the secret value of a credential pair
/// (<c>Password=</c>, <c>Pwd=</c>, <c>AccountKey=</c>, <c>SharedAccessKey=</c>, <c>Secret=</c>) is
/// masked while the key and the rest of the connection string stay readable. Prose that merely
/// mentions one of the key words without a <c>key=value</c> shape is left untouched.
/// </summary>
[Collection(nameof(MeterSerial))]
public sealed class ConnectionStringSecretRuleTests
{
    private static Redactor Build(ShadeDiagnostics diagnostics) =>
        new(BuiltInRules.All, SensitiveKeyset.Default, Masks.Full(), diagnostics);

    [Fact]
    public void Password_value_is_masked_and_the_key_stays_visible()
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        var result = redactor.Redact("Server=db;Database=app;Password=P@ssw0rd!;Pooling=true");

        // The key and the surrounding pairs remain readable; only the value is gone.
        Assert.Contains("Password=", result, StringComparison.Ordinal);
        Assert.DoesNotContain("P@ssw0rd!", result, StringComparison.Ordinal);
        Assert.Contains("Server=db", result, StringComparison.Ordinal);
        Assert.Contains("Database=app", result, StringComparison.Ordinal);
        Assert.Contains("Pooling=true", result, StringComparison.Ordinal);
        Assert.Equal("Server=db;Database=app;Password=[REDACTED];Pooling=true", result);
    }

    [Theory]
    [InlineData("Pwd=s3cr3t;", "s3cr3t")]
    [InlineData("Secret=topsecretvalue", "topsecretvalue")]
    [InlineData("SharedAccessKey=abc/def+ghi=", "abc/def+ghi")]
    public void Each_supported_key_has_its_value_masked(string pair, string secret)
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        var result = redactor.Redact($"conn: {pair}");

        Assert.DoesNotContain(secret, result, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Key_matching_is_case_insensitive()
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        var result = redactor.Redact("PASSWORD=hunter2;Trusted_Connection=false");

        Assert.DoesNotContain("hunter2", result, StringComparison.Ordinal);
        Assert.Contains("Trusted_Connection=false", result, StringComparison.Ordinal);
    }

    [Fact]
    public void An_account_key_with_base64_padding_is_masked_whole_including_the_padding()
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        // Azure storage account keys are base64 and end with '=' padding. The value runs to the next
        // ';' or end of text, so the padding is part of the masked value, not left dangling.
        var result = redactor.Redact("AccountName=store;AccountKey=Zm9vYmFyYmF6cXV4MTIzNDU2Nzg5MA==;Endpoint=core");

        Assert.DoesNotContain("Zm9vYmFyYmF6cXV4MTIzNDU2Nzg5MA", result, StringComparison.Ordinal);
        Assert.Contains("AccountKey=[REDACTED]", result, StringComparison.Ordinal);
        Assert.Contains("AccountName=store", result, StringComparison.Ordinal);
        Assert.Contains("Endpoint=core", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Several_secrets_in_one_string_are_all_masked()
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        var result = redactor.Redact("Password=one;Host=h;AccountKey=two");

        Assert.DoesNotContain("=one", result, StringComparison.Ordinal);
        Assert.DoesNotContain("=two", result, StringComparison.Ordinal);
        Assert.Contains("Host=h", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Prose_mentioning_password_without_a_key_value_pair_is_untouched()
    {
        using var diag = new ShadeDiagnostics();
        var redactor = Build(diag);

        // No 'key=value' shape: the word appears in a sentence, so the rule does not fire.
        const string text = "the user reset their password and signed in again";
        Assert.Equal(text, redactor.Redact(text));
    }

    [Fact]
    public void The_rule_carries_the_expected_name()
    {
        Assert.Equal("connection_string_secret", BuiltInRules.ConnectionStringSecret.Name);
    }
}
