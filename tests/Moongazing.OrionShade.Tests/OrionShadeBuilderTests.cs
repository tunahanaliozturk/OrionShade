namespace Moongazing.OrionShade.Tests;

using System.Text.RegularExpressions;

using Microsoft.Extensions.DependencyInjection;

using Moongazing.OrionShade;
using Moongazing.OrionShade.Redaction;

using Xunit;

/// <summary>
/// Covers <see cref="OrionShadeBuilder"/> composition: custom rules (regex and string overloads),
/// custom sensitive keys, the default-mask behaviour, a custom key mask, opting into defaults, and
/// argument validation. The builder's outputs are observed through a registered redactor.
/// </summary>
public sealed class OrionShadeBuilderTests
{
    private static IRedactor BuildRedactor(Action<OrionShadeBuilder> configure)
    {
        var services = new ServiceCollection();
        services.AddOrionShade(configure);
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IRedactor>();
    }

    [Fact]
    public void AddRule_with_a_compiled_regex_is_applied()
    {
        var redactor = BuildRedactor(b => b
            .AddRule("phone", new Regex(@"\d{10}"), Masks.KeepLast(2)));

        var result = redactor.Redact("call 5551234567 now");

        Assert.Contains("********67", result, StringComparison.Ordinal);
    }

    [Fact]
    public void AddRule_with_a_string_pattern_compiles_case_insensitively()
    {
        var redactor = BuildRedactor(b => b
            .AddRule("flag", "secretword", Masks.Full("#")));

        // Pattern was lower-case but the input is mixed-case: IgnoreCase is applied.
        Assert.Equal("#", redactor.Redact("SecretWord"));
    }

    [Fact]
    public void AddRule_string_overload_defaults_to_a_full_mask()
    {
        var redactor = BuildRedactor(b => b.AddRule("digits", @"\d+"));

        Assert.Equal($"order {Masks.DefaultToken}", redactor.Redact("order 12345"));
    }

    [Fact]
    public void AddRule_throws_when_the_string_pattern_is_null()
    {
        var builder = new OrionShadeBuilder();
        Assert.Throws<ArgumentNullException>(() => builder.AddRule("x", (string)null!));
    }

    [Fact]
    public void AddRule_throws_when_the_string_pattern_is_empty()
    {
        var builder = new OrionShadeBuilder();
        Assert.Throws<ArgumentException>(() => builder.AddRule("x", string.Empty));
    }

    [Fact]
    public void AddSensitiveKeys_marks_custom_keys_as_sensitive()
    {
        var redactor = BuildRedactor(b => b.AddSensitiveKeys("national_id", "iban"));

        Assert.Equal(Masks.DefaultToken, redactor.RedactValue("national_id", "123"));
        Assert.Equal(Masks.DefaultToken, redactor.RedactValue("IBAN", "DE0000"));
    }

    [Fact]
    public void AddSensitiveKeys_throws_when_keys_array_is_null()
    {
        var builder = new OrionShadeBuilder();
        Assert.Throws<ArgumentNullException>(() => builder.AddSensitiveKeys(null!));
    }

    [Fact]
    public void Without_UseDefaults_the_built_in_rules_and_keys_are_absent()
    {
        var redactor = BuildRedactor(b => b.AddSensitiveKeys("national_id"));

        // No built-in email rule, so the address passes through.
        Assert.Equal("a@b.com", redactor.Redact("a@b.com"));
        // "password" is a default key, but defaults were not opted into.
        Assert.Equal("a@b.com", redactor.RedactValue("password", "a@b.com"));
    }

    [Fact]
    public void UseDefaults_adds_the_built_in_rules_and_keys()
    {
        var redactor = BuildRedactor(b => b.UseDefaults());

        Assert.DoesNotContain("a@b.com", redactor.Redact("mail a@b.com"), StringComparison.Ordinal);
        Assert.Equal(Masks.DefaultToken, redactor.RedactValue("password", "x"));
    }

    [Fact]
    public void UseDefaults_can_be_combined_with_custom_rules_and_keys()
    {
        var redactor = BuildRedactor(b => b
            .UseDefaults()
            .AddRule("ticket", @"TCK-\d+", Masks.Full("[TICKET]"))
            .AddSensitiveKeys("national_id"));

        Assert.DoesNotContain("a@b.com", redactor.Redact("a@b.com"), StringComparison.Ordinal);
        Assert.Equal("[TICKET]", redactor.Redact("TCK-99"));
        Assert.Equal(Masks.DefaultToken, redactor.RedactValue("national_id", "123"));
    }

    [Fact]
    public void UseKeyMask_overrides_the_mask_used_for_sensitive_keys()
    {
        var redactor = BuildRedactor(b => b
            .UseDefaults()
            .UseKeyMask(Masks.KeepLast(2)));

        // KeepLast(2) over "hunter2" keeps the trailing "r2".
        Assert.Equal("*****r2", redactor.RedactValue("password", "hunter2"));
    }

    [Fact]
    public void UseKeyMask_throws_when_the_mask_is_null()
    {
        var builder = new OrionShadeBuilder();
        Assert.Throws<ArgumentNullException>(() => builder.UseKeyMask(null!));
    }

    [Fact]
    public void The_default_key_mask_is_a_full_mask_when_UseKeyMask_is_not_called()
    {
        var redactor = BuildRedactor(b => b.AddSensitiveKeys("national_id"));

        Assert.Equal(Masks.DefaultToken, redactor.RedactValue("national_id", "any value"));
    }

    [Fact]
    public void Builder_methods_are_chainable_and_return_the_same_builder()
    {
        var builder = new OrionShadeBuilder();

        var returned = builder
            .UseDefaults()
            .AddRule("a", @"\d")
            .AddRule("b", new Regex("x"))
            .AddSensitiveKeys("k")
            .UseKeyMask(Masks.Full());

        Assert.Same(builder, returned);
    }
}
