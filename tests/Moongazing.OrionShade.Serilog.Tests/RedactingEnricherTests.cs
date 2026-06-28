namespace Moongazing.OrionShade.Serilog.Tests;

using global::Serilog;
using global::Serilog.Events;

using Moongazing.OrionShade;

using Xunit;

/// <summary>
/// Tests the enricher seam (<c>Enrich.WithOrionShadeRedaction</c>): the lighter-weight path that
/// redacts string property values, which also scrubs the rendered message wherever a secret reached it
/// through a property.
/// </summary>
public sealed class RedactingEnricherTests
{
    private static readonly IRedactor Redactor = new OrionShadeBuilder().UseDefaults().Build();

    private static (ILogger Logger, CapturingSink Sink) BuildLogger()
    {
        var sink = new CapturingSink();
        var logger = new LoggerConfiguration()
            .Enrich.WithOrionShadeRedaction(Redactor)
            .WriteTo.Sink(sink)
            .CreateLogger();
        return (logger, sink);
    }

    [Fact]
    public void redacts_a_string_property_value()
    {
        var (logger, sink) = BuildLogger();

        logger.Information("User {Email} signed in", "erin@example.com");

        var value = Assert.IsType<ScalarValue>(sink.Last.Properties["Email"]);
        Assert.Equal("[REDACTED]", value.Value);
    }

    [Fact]
    public void redacts_the_rendered_message_through_the_property()
    {
        var (logger, sink) = BuildLogger();

        logger.Information("User {Email} signed in", "frank@example.com");

        Assert.DoesNotContain("frank@example.com", sink.LastMessage, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", sink.LastMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void leaves_a_non_string_property_untouched()
    {
        var (logger, sink) = BuildLogger();

        logger.Information("Retry count {Count}", 3);

        var count = Assert.IsType<ScalarValue>(sink.Last.Properties["Count"]);
        Assert.Equal(3, count.Value);
    }

    [Fact]
    public void leaves_a_non_matching_string_property_untouched()
    {
        var (logger, sink) = BuildLogger();

        logger.Information("Region {Region}", "eu-west-1");

        var region = Assert.IsType<ScalarValue>(sink.Last.Properties["Region"]);
        Assert.Equal("eu-west-1", region.Value);
    }
}
