namespace Moongazing.OrionShade.Serilog;

using global::Serilog.Core;
using global::Serilog.Events;

/// <summary>
/// An <see cref="ILogEventSink"/> wrapper that redacts each <see cref="LogEvent"/> before forwarding
/// it to the wrapped sink, so the message a sink renders, its string property values, and any attached
/// exception are scrubbed of secrets and PII. This is the complete Serilog seam: unlike an enricher,
/// which can only touch the mutable property collection, the wrapper rebuilds the immutable message
/// template and exception too, so a secret written as a literal in the template or carried in an
/// exception message is masked as well.
/// </summary>
/// <remarks>
/// Disposal is delegated to the wrapped sink when it is disposable, matching Serilog's wrapper
/// convention. When no redactor is configured the event is forwarded unchanged, so the wrapper is
/// inert until a rule set is supplied.
/// </remarks>
internal sealed class RedactingSink : ILogEventSink, IDisposable
{
    private readonly ILogEventSink wrapped;
    private readonly IRedactor? redactor;

    /// <summary>Wrap <paramref name="wrapped"/> so every event is redacted before it reaches it.</summary>
    /// <param name="wrapped">The sink to forward redacted events to.</param>
    /// <param name="redactor">The redactor applied to each event, or null to forward unchanged.</param>
    public RedactingSink(ILogEventSink wrapped, IRedactor? redactor)
    {
        ArgumentNullException.ThrowIfNull(wrapped);
        this.wrapped = wrapped;
        this.redactor = redactor;
    }

    /// <inheritdoc />
    public void Emit(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        wrapped.Emit(LogEventRedaction.Redact(logEvent, redactor));
    }

    /// <inheritdoc />
    public void Dispose() => (wrapped as IDisposable)?.Dispose();
}
