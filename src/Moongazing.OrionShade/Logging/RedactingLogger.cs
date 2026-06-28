namespace Moongazing.OrionShade.Logging;

using Microsoft.Extensions.Logging;

/// <summary>
/// An <see cref="ILogger"/> decorator that redacts the formatted log message before the wrapped
/// logger (and therefore its sink) sees it. It substitutes the caller's formatter with one that runs
/// the produced message through an <see cref="IRedactor"/>, so structured state and scopes reach the
/// inner logger unchanged while the rendered text is scrubbed of secrets and PII.
/// </summary>
internal sealed class RedactingLogger : ILogger
{
    private readonly ILogger inner;
    private readonly IRedactor redactor;

    /// <summary>Wrap <paramref name="inner"/> so its formatted output is redacted.</summary>
    /// <param name="inner">The logger to forward to after redaction.</param>
    /// <param name="redactor">The redactor applied to each formatted message.</param>
    public RedactingLogger(ILogger inner, IRedactor redactor)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(redactor);
        this.inner = inner;
        this.redactor = redactor;
    }

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => inner.BeginScope(state);

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => inner.IsEnabled(logLevel);

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        // Wrap the caller's formatter so the message the inner logger renders is redacted. The
        // original state is forwarded untouched, so structured providers still receive the raw
        // values; only the rendered string a text sink writes is scrubbed.
        inner.Log(
            logLevel,
            eventId,
            state,
            exception,
            (s, e) => redactor.Redact(formatter(s, e)));
    }
}
