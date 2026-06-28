namespace Moongazing.OrionShade.Logging;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Registration helpers that fold OrionShade redaction into the
/// <c>Microsoft.Extensions.Logging</c> pipeline. The redaction runs over the formatted message of
/// every log entry before it reaches any sink, and which rule set applies can vary by category.
/// </summary>
/// <remarks>
/// The integration is built only on <c>Microsoft.Extensions.Logging.Abstractions</c>. It works by
/// decorating each registered <see cref="ILoggerProvider"/> so the logger handed to a sink is a
/// <see cref="RedactingLogger"/>. Decoration is applied to the <see cref="ILoggerProvider"/> service
/// descriptors present when the method is called, so register your sink providers first and call this
/// last (for example after the <c>AddConsole</c> / <c>AddProvider</c> calls). A provider added after
/// this call is not wrapped. Calling it has no effect on logging until at least one redactor is
/// configured; a category that resolves to no redactor is logged unchanged.
/// </remarks>
public static class OrionShadeLoggingBuilderExtensions
{
    /// <summary>
    /// Redact log messages in the <c>Microsoft.Extensions.Logging</c> pipeline. Every registered
    /// <see cref="ILoggerProvider"/> is decorated so the formatted message is scrubbed before the
    /// sink writes it.
    /// </summary>
    /// <param name="builder">The logging builder.</param>
    /// <param name="configure">
    /// Declares the redactors: a default applied to every category, and optional per-category-prefix
    /// redactors so different categories run different rule sets. When null, redaction is wired in but
    /// inert until configured.
    /// </param>
    /// <returns>The same logging builder, for chaining.</returns>
    public static ILoggingBuilder AddOrionShadeRedaction(
        this ILoggingBuilder builder,
        Action<LogRedactionOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new LogRedactionOptions();
        configure?.Invoke(options);

        DecorateLoggerProviders(builder.Services, options);
        return builder;
    }

    /// <summary>
    /// Redact log messages with a single redactor applied to every category. A convenience over
    /// <see cref="AddOrionShadeRedaction(ILoggingBuilder, Action{LogRedactionOptions}?)"/> for the
    /// common case of one rule set for the whole application.
    /// </summary>
    /// <param name="builder">The logging builder.</param>
    /// <param name="redactor">The redactor applied to every log category.</param>
    /// <returns>The same logging builder, for chaining.</returns>
    public static ILoggingBuilder AddOrionShadeRedaction(this ILoggingBuilder builder, IRedactor redactor)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(redactor);
        return builder.AddOrionShadeRedaction(options => options.DefaultRedactor = redactor);
    }

    /// <summary>
    /// Replace each registered <see cref="ILoggerProvider"/> descriptor with one that resolves the
    /// original instance and wraps it in a <see cref="RedactingLoggerProvider"/>. The original
    /// implementation factory or type is preserved, so the concrete provider is still constructed
    /// exactly as registered; only the public service is the decorator.
    /// </summary>
    private static void DecorateLoggerProviders(IServiceCollection services, LogRedactionOptions options)
    {
        for (var i = 0; i < services.Count; i++)
        {
            var descriptor = services[i];
            if (descriptor.ServiceType != typeof(ILoggerProvider))
            {
                continue;
            }

            if (descriptor.ImplementationType == typeof(RedactingLoggerProvider))
            {
                // Already decorated by a prior call; do not stack wrappers.
                continue;
            }

            services[i] = ServiceDescriptor.Describe(
                typeof(ILoggerProvider),
                provider => new RedactingLoggerProvider(ResolveInner(provider, descriptor), options),
                descriptor.Lifetime);
        }
    }

    /// <summary>
    /// Materialise the inner provider a descriptor describes, whether it was registered as an
    /// instance, an implementation type, or a factory.
    /// </summary>
    private static ILoggerProvider ResolveInner(IServiceProvider provider, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is ILoggerProvider instance)
        {
            return instance;
        }

        if (descriptor.ImplementationFactory is not null)
        {
            return (ILoggerProvider)descriptor.ImplementationFactory(provider);
        }

        if (descriptor.ImplementationType is not null)
        {
            return (ILoggerProvider)ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType);
        }

        throw new InvalidOperationException(
            "An ILoggerProvider registration has no instance, factory, or implementation type to decorate.");
    }
}
