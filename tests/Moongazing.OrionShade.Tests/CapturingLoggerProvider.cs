namespace Moongazing.OrionShade.Tests;

using System.Collections.Concurrent;
using System.Text;

using Microsoft.Extensions.Logging;

/// <summary>
/// A test sink: an <see cref="ILoggerProvider"/> whose loggers record the formatted message, the
/// rendered exception text, and any active scope values of every entry, tagged with its category. Lets
/// a test assert what text a sink would actually have written after the OrionShade redaction decorator
/// has run. Implements <see cref="ISupportExternalScope"/> so a test can verify scope forwarding
/// through the decorator.
/// </summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly ConcurrentQueue<CapturedEntry> entries = new();

    private IExternalScopeProvider? scopeProvider;

    /// <summary>Every message captured so far, in log order.</summary>
    public IReadOnlyCollection<CapturedEntry> Entries => entries;

    /// <summary>The captured messages, ignoring category.</summary>
    public IEnumerable<string> Messages => entries.Select(e => e.Message);

    /// <summary>True once <see cref="SetScopeProvider"/> has been called.</summary>
    public bool ScopeProviderWasSet { get; private set; }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) =>
        new CapturingLogger(categoryName, entries, () => scopeProvider);

    /// <inheritdoc />
    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        ScopeProviderWasSet = true;
        this.scopeProvider = scopeProvider;
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }

    private sealed class CapturingLogger(
        string category,
        ConcurrentQueue<CapturedEntry> sink,
        Func<IExternalScopeProvider?> scopeAccessor) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => scopeAccessor()?.Push(state);

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var scopeText = new StringBuilder();
            scopeAccessor()?.ForEachScope(
                (scope, builder) => builder.Append(scope).Append(';'),
                scopeText);

            sink.Enqueue(new CapturedEntry(
                category,
                formatter(state, exception),
                exception?.ToString(),
                scopeText.ToString()));
        }
    }
}

/// <summary>One captured log entry.</summary>
/// <param name="Category">The logger category that produced it.</param>
/// <param name="Message">The final formatted message the sink received.</param>
/// <param name="ExceptionText">The rendered text of the exception the sink received, if any.</param>
/// <param name="ScopeText">The rendered active scope values, concatenated.</param>
internal sealed record CapturedEntry(string Category, string Message, string? ExceptionText, string ScopeText);
