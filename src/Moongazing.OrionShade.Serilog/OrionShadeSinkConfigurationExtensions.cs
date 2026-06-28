namespace Moongazing.OrionShade.Serilog;

using global::Serilog;
using global::Serilog.Configuration;
using global::Serilog.Core;

/// <summary>
/// <c>WriteTo</c> configuration helpers that wrap one or more Serilog sinks so every log event is
/// redacted before it reaches them. This is the complete redaction seam: it scrubs the rendered
/// message, string property values, and the exception text, because the wrapper rebuilds the otherwise
/// immutable parts of the event.
/// </summary>
public static class OrionShadeSinkConfigurationExtensions
{
    /// <summary>
    /// Wrap the sinks configured in <paramref name="configureWrappedSinks"/> so each log event is
    /// redacted with <paramref name="redactor"/> before they receive it. Register the sinks to protect
    /// inside the callback, for example
    /// <c>.WriteTo.OrionShadeRedaction(redactor, w =&gt; w.Console())</c>.
    /// </summary>
    /// <param name="loggerSinkConfiguration">The <c>WriteTo</c> configuration to extend.</param>
    /// <param name="redactor">The redactor applied to every event before the wrapped sinks see it.</param>
    /// <param name="configureWrappedSinks">Registers the sinks whose input is redacted.</param>
    /// <returns>The logger configuration, for chaining.</returns>
    public static LoggerConfiguration OrionShadeRedaction(
        this LoggerSinkConfiguration loggerSinkConfiguration,
        IRedactor redactor,
        Action<LoggerSinkConfiguration> configureWrappedSinks)
    {
        ArgumentNullException.ThrowIfNull(loggerSinkConfiguration);
        ArgumentNullException.ThrowIfNull(redactor);
        ArgumentNullException.ThrowIfNull(configureWrappedSinks);

        return LoggerSinkConfiguration.Wrap(
            loggerSinkConfiguration,
            inner => new RedactingSink(inner, redactor),
            configureWrappedSinks);
    }

    /// <summary>
    /// Wrap an already-constructed sink so each log event is redacted with <paramref name="redactor"/>
    /// before that sink receives it. Use this overload when you hold an <see cref="ILogEventSink"/>
    /// instance rather than configuring one through <c>WriteTo</c>.
    /// </summary>
    /// <param name="loggerSinkConfiguration">The <c>WriteTo</c> configuration to extend.</param>
    /// <param name="redactor">The redactor applied to every event before the wrapped sink sees it.</param>
    /// <param name="wrappedSink">The sink whose input is redacted.</param>
    /// <returns>The logger configuration, for chaining.</returns>
    public static LoggerConfiguration OrionShadeRedaction(
        this LoggerSinkConfiguration loggerSinkConfiguration,
        IRedactor redactor,
        ILogEventSink wrappedSink)
    {
        ArgumentNullException.ThrowIfNull(loggerSinkConfiguration);
        ArgumentNullException.ThrowIfNull(redactor);
        ArgumentNullException.ThrowIfNull(wrappedSink);

        return loggerSinkConfiguration.Sink(new RedactingSink(wrappedSink, redactor));
    }
}
