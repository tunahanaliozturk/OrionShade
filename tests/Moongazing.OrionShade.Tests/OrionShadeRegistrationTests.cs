namespace Moongazing.OrionShade.Tests;

using Microsoft.Extensions.DependencyInjection;

using Moongazing.OrionShade;
using Moongazing.OrionShade.Redaction;

using Xunit;

public sealed class OrionShadeRegistrationTests
{
    [Fact]
    public void AddOrionShade_with_no_config_uses_the_built_in_defaults()
    {
        var services = new ServiceCollection();
        services.AddOrionShade();

        using var provider = services.BuildServiceProvider();
        var redactor = provider.GetRequiredService<IRedactor>();

        Assert.DoesNotContain("a@b.com", redactor.Redact("mail a@b.com"), StringComparison.Ordinal);
        Assert.Equal("[REDACTED]", redactor.RedactValue("password", "x"));
    }

    [Fact]
    public void AddOrionShade_honours_a_custom_configuration()
    {
        var services = new ServiceCollection();
        services.AddOrionShade(b => b
            .AddRule("digits", @"\d+", Masks.Full("#"))
            .AddSensitiveKeys("national_id"));

        using var provider = services.BuildServiceProvider();
        var redactor = provider.GetRequiredService<IRedactor>();

        Assert.Equal("order #", redactor.Redact("order 12345"));
        Assert.Equal("[REDACTED]", redactor.RedactValue("national_id", "123"));
        // Defaults were not opted into, so an email passes through.
        Assert.Equal("a@b.com", redactor.Redact("a@b.com"));
    }
}
