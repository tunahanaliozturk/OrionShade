namespace Moongazing.OrionShade.Serilog.Tests;

using System.Collections.Concurrent;
using System.Globalization;

using global::Serilog.Core;
using global::Serilog.Events;

/// <summary>
/// A test sink: an <see cref="ILogEventSink"/> that records every <see cref="LogEvent"/> it receives,
/// so a test can assert what a real sink would have seen after the OrionShade redaction wrapper or
/// enricher has run. Captures the event itself, the rendered message text, and the rendered exception
/// text.
/// </summary>
internal sealed class CapturingSink : ILogEventSink
{
    private readonly ConcurrentQueue<LogEvent> events = new();

    /// <summary>Every event captured so far, in emit order.</summary>
    public IReadOnlyCollection<LogEvent> Events => events;

    /// <summary>The most recently captured event.</summary>
    public LogEvent Last => events.Last();

    /// <summary>The rendered message of the most recently captured event.</summary>
    public string LastMessage => Last.RenderMessage(CultureInfo.InvariantCulture);

    /// <summary>The rendered exception text of the most recently captured event, if any.</summary>
    public string? LastExceptionText => Last.Exception?.ToString();

    /// <inheritdoc />
    public void Emit(LogEvent logEvent) => events.Enqueue(logEvent);
}
