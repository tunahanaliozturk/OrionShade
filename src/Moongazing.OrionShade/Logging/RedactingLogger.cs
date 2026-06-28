namespace Moongazing.OrionShade.Logging;

using Microsoft.Extensions.Logging;

/// <summary>
/// An <see cref="ILogger"/> decorator that redacts both the formatted message and any logged
/// exception before the wrapped logger (and therefore its sink) sees them. It substitutes the
/// caller's formatter with one that runs the produced message through an <see cref="IRedactor"/>, and
/// substitutes the exception with a <see cref="RedactedException"/> whose rendered text is scrubbed,
/// so structured state and scopes reach the inner logger unchanged while the rendered text a text sink
/// writes is free of secrets and PII.
/// </summary>
internal sealed class RedactingLogger : ILogger
{
    private readonly ILogger inner;
    private readonly IRedactor redactor;

    /// <summary>Wrap <paramref name="inner"/> so its formatted output is redacted.</summary>
    /// <param name="inner">The logger to forward to after redaction.</param>
    /// <param name="redactor">The redactor applied to each formatted message and exception.</param>
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

        // Wrap the exception so the text a sink renders from it (its Message and ToString(), which most
        // text sinks append after the message) is scrubbed too. Without this an exception carrying PII
        // in its message would leak unredacted even though the formatted message is clean. The original
        // type name and stack frames are preserved in the rendered output; only the sensitive text
        // inside is masked.
        var redactedException = exception is null ? null : new RedactedException(exception, redactor);

        // Wrap the caller's formatter so the message the inner logger renders is redacted. The original
        // state is forwarded untouched, so structured providers still receive the raw values; only the
        // rendered string a text sink writes is scrubbed. The formatter is invoked with the redacted
        // exception so a formatter that incorporates the exception text also sees the scrubbed form.
        inner.Log(
            logLevel,
            eventId,
            state,
            redactedException,
            (s, _) => redactor.Redact(formatter(s, exception)));
    }
}
