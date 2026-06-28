namespace Moongazing.OrionShade.Serilog.Tests;

using global::Serilog;
using global::Serilog.Events;

using Moongazing.OrionShade;

using Xunit;

/// <summary>
/// Tests the sink-wrapper seam (<c>WriteTo.OrionShadeRedaction</c>): the complete path that redacts the
/// rendered message, string property values, and the exception text before the wrapped sink sees them.
/// </summary>
public sealed class RedactingSinkTests
{
    private static readonly IRedactor Redactor = new OrionShadeBuilder().UseDefaults().Build();

    private static (ILogger Logger, CapturingSink Sink) BuildLogger()
    {
        var sink = new CapturingSink();
        var logger = new LoggerConfiguration()
            .WriteTo.OrionShadeRedaction(Redactor, w => w.Sink(sink))
            .CreateLogger();
        return (logger, sink);
    }

    [Fact]
    public void redacts_pii_in_the_rendered_message_carried_through_a_property()
    {
        var (logger, sink) = BuildLogger();

        logger.Information("Contact {Email} for details", "alice@example.com");

        Assert.DoesNotContain("alice@example.com", sink.LastMessage, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", sink.LastMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void redacts_pii_written_as_a_literal_in_the_message_template()
    {
        var (logger, sink) = BuildLogger();

        // No property: the email sits in the literal template text, which only the wrapper can reach.
        logger.Information("Reset link sent to bob@example.com now");

        Assert.DoesNotContain("bob@example.com", sink.LastMessage, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", sink.LastMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void redacts_a_string_property_value_in_place()
    {
        var (logger, sink) = BuildLogger();

        logger.Information("User logged in {Email}", "carol@example.com");

        var value = Assert.IsType<ScalarValue>(sink.Last.Properties["Email"]);
        Assert.Equal("[REDACTED]", value.Value);
    }

    [Fact]
    public void masks_a_sensitive_key_property_wholesale()
    {
        var (logger, sink) = BuildLogger();

        // "password" is a default sensitive key: the whole value is masked, not pattern-matched.
        logger.Information("Auth attempt {password}", "hunter2-not-a-pattern");

        var value = Assert.IsType<ScalarValue>(sink.Last.Properties["password"]);
        Assert.Equal("[REDACTED]", value.Value);
    }

    [Fact]
    public void leaves_a_non_string_property_untouched()
    {
        var (logger, sink) = BuildLogger();

        logger.Information("Order {OrderId} total {Amount}", 4242, 19.95m);

        var orderId = Assert.IsType<ScalarValue>(sink.Last.Properties["OrderId"]);
        var amount = Assert.IsType<ScalarValue>(sink.Last.Properties["Amount"]);
        Assert.Equal(4242, orderId.Value);
        Assert.Equal(19.95m, amount.Value);
    }

    [Fact]
    public void leaves_a_non_matching_string_property_untouched()
    {
        var (logger, sink) = BuildLogger();

        logger.Information("Status {State}", "active");

        var state = Assert.IsType<ScalarValue>(sink.Last.Properties["State"]);
        Assert.Equal("active", state.Value);
    }

    [Fact]
    public void redacts_pii_in_the_exception_message()
    {
        var (logger, sink) = BuildLogger();
        var exception = new InvalidOperationException("login failed for dave@example.com");

        logger.Error(exception, "Operation failed");

        Assert.NotNull(sink.LastExceptionText);
        Assert.DoesNotContain("dave@example.com", sink.LastExceptionText, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", sink.LastExceptionText, StringComparison.Ordinal);
        // The original exception type survives so the log still diagnoses the failure.
        Assert.Contains(nameof(InvalidOperationException), sink.LastExceptionText, StringComparison.Ordinal);
    }

    [Fact]
    public void passes_a_clean_event_through_unchanged()
    {
        var (logger, sink) = BuildLogger();

        logger.Information("Job {JobId} completed in {Ms}ms", "nightly-sync", 1200);

        Assert.Equal("Job \"nightly-sync\" completed in 1200ms", sink.LastMessage);
        Assert.Equal("nightly-sync", Assert.IsType<ScalarValue>(sink.Last.Properties["JobId"]).Value);
    }
}
