namespace Moongazing.OrionShade;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Moongazing.OrionShade.Diagnostics;

/// <summary>
/// Registration helpers for OrionShade.
/// </summary>
public static class OrionShadeServiceCollectionExtensions
{
    /// <summary>
    /// Register the redactor and diagnostics. When <paramref name="configure"/> is null, the
    /// built-in defaults (email, card, JWT patterns and the common sensitive keys) are used.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Declares rules and sensitive keys. Optional.</param>
    public static IServiceCollection AddOrionShade(
        this IServiceCollection services,
        Action<OrionShadeBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var builder = new OrionShadeBuilder();
        if (configure is null)
        {
            builder.UseDefaults();
        }
        else
        {
            configure(builder);
        }

        var rules = builder.BuildRules();
        var keyset = builder.BuildKeyset();
        var keyMask = builder.KeyMask;

        services.TryAddSingleton<ShadeDiagnostics>();
        services.TryAddSingleton<IRedactor>(sp => new Redactor(
            rules, keyset, keyMask, sp.GetRequiredService<ShadeDiagnostics>()));

        return services;
    }
}
