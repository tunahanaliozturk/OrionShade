namespace Moongazing.OrionShade.Tests;

using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

/// <summary>
/// A test sink: an <see cref="ILoggerProvider"/> whose loggers record the formatted message of every
/// entry, tagged with its category. Lets a test assert what text a sink would actually have written
/// after the OrionShade redaction decorator has run.
/// </summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<CapturedEntry> entries = new();

    /// <summary>Every message captured so far, in log order.</summary>
    public IReadOnlyCollection<CapturedEntry> Entries => entries;

    /// <summary>The captured messages, ignoring category.</summary>
    public IEnumerable<string> Messages => entries.Select(e => e.Message);

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, entries);

    /// <inheritdoc />
    public void Dispose()
    {
    }

    private sealed class CapturingLogger(string category, ConcurrentQueue<CapturedEntry> sink) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            sink.Enqueue(new CapturedEntry(category, formatter(state, exception)));
    }
}

/// <summary>One captured log entry.</summary>
/// <param name="Category">The logger category that produced it.</param>
/// <param name="Message">The final formatted message the sink received.</param>
internal sealed record CapturedEntry(string Category, string Message);
