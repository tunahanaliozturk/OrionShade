namespace Moongazing.OrionShade.Logging;

using Microsoft.Extensions.Logging;

/// <summary>
/// An <see cref="ILoggerProvider"/> decorator that wraps every logger a concrete provider creates in
/// a <see cref="RedactingLogger"/>, so the message reaching that provider's sink is redacted. The
/// redactor used for a given logger is chosen per category from <see cref="LogRedactionOptions"/>,
/// letting different categories run different rule sets. A category that resolves to no redactor is
/// passed through to the inner logger unchanged.
/// </summary>
internal sealed class RedactingLoggerProvider : ILoggerProvider
{
    private readonly ILoggerProvider inner;
    private readonly LogRedactionOptions options;

    /// <summary>Wrap <paramref name="inner"/> so the loggers it creates redact their output.</summary>
    /// <param name="inner">The concrete provider whose loggers are decorated.</param>
    /// <param name="options">The per-category redactor configuration.</param>
    public RedactingLoggerProvider(ILoggerProvider inner, LogRedactionOptions options)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(options);
        this.inner = inner;
        this.options = options;
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
    {
        var logger = inner.CreateLogger(categoryName);
        var redactor = options.ResolveFor(categoryName);

        // No redactor for this category (no matching prefix and no default): forward the inner logger
        // untouched so redaction is genuinely off for it, with no wrapper overhead.
        return redactor is null ? logger : new RedactingLogger(logger, redactor);
    }

    /// <inheritdoc />
    public void Dispose() => inner.Dispose();
}
