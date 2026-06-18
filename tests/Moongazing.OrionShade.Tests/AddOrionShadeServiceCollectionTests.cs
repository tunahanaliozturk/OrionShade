namespace Moongazing.OrionShade.Tests;

using Microsoft.Extensions.DependencyInjection;

using Moongazing.OrionShade;
using Moongazing.OrionShade.Diagnostics;
using Moongazing.OrionShade.Redaction;

using Xunit;

/// <summary>
/// Covers <see cref="OrionShadeServiceCollectionExtensions.AddOrionShade"/>: registration of the
/// redactor and diagnostics, singleton lifetime, idempotent registration, and argument validation.
/// </summary>
public sealed class AddOrionShadeServiceCollectionTests
{
    [Fact]
    public void AddOrionShade_registers_an_IRedactor()
    {
        var services = new ServiceCollection();
        services.AddOrionShade();

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<IRedactor>());
    }

    [Fact]
    public void AddOrionShade_registers_ShadeDiagnostics()
    {
        var services = new ServiceCollection();
        services.AddOrionShade();

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<ShadeDiagnostics>());
    }

    [Fact]
    public void AddOrionShade_resolves_the_redactor_as_a_singleton()
    {
        var services = new ServiceCollection();
        services.AddOrionShade();

        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<IRedactor>();
        var second = provider.GetRequiredService<IRedactor>();

        Assert.Same(first, second);
    }

    [Fact]
    public void AddOrionShade_resolves_diagnostics_as_a_singleton()
    {
        var services = new ServiceCollection();
        services.AddOrionShade();

        using var provider = services.BuildServiceProvider();
        Assert.Same(
            provider.GetRequiredService<ShadeDiagnostics>(),
            provider.GetRequiredService<ShadeDiagnostics>());
    }

    [Fact]
    public void AddOrionShade_returns_the_same_service_collection_for_chaining()
    {
        var services = new ServiceCollection();
        var returned = services.AddOrionShade();

        Assert.Same(services, returned);
    }

    [Fact]
    public void AddOrionShade_with_no_config_uses_the_built_in_defaults()
    {
        var services = new ServiceCollection();
        services.AddOrionShade();

        using var provider = services.BuildServiceProvider();
        var redactor = provider.GetRequiredService<IRedactor>();

        Assert.DoesNotContain("a@b.com", redactor.Redact("mail a@b.com"), StringComparison.Ordinal);
        Assert.Equal(Masks.DefaultToken, redactor.RedactValue("password", "x"));
    }

    [Fact]
    public void AddOrionShade_does_not_replace_an_already_registered_redactor()
    {
        var sentinel = new StubRedactor();
        var services = new ServiceCollection();
        services.AddSingleton<IRedactor>(sentinel);

        // TryAddSingleton inside AddOrionShade must respect the prior registration.
        services.AddOrionShade();

        using var provider = services.BuildServiceProvider();
        Assert.Same(sentinel, provider.GetRequiredService<IRedactor>());
    }

    [Fact]
    public void AddOrionShade_throws_when_the_service_collection_is_null()
    {
        Assert.Throws<ArgumentNullException>(
            () => OrionShadeServiceCollectionExtensions.AddOrionShade(null!));
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
        Assert.Equal(Masks.DefaultToken, redactor.RedactValue("national_id", "123"));
        // Defaults were not opted into, so an email passes through.
        Assert.Equal("a@b.com", redactor.Redact("a@b.com"));
    }

    private sealed class StubRedactor : IRedactor
    {
        public string Redact(string input) => input;

        public string RedactValue(string key, string value) => value;
    }
}
