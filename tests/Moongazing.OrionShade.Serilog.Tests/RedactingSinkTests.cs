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
    private static readonly string[] Recipients = { "heidi@example.com", "ivan@example.com" };
    private static readonly string[] ExpectedAccountPropertyNames = { "Email", "Age" };

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

    [Fact]
    public void redacts_pii_inside_a_destructured_object_property()
    {
        var (logger, sink) = BuildLogger();

        // The @ operator destructures the object into a StructureValue whose Email property carries PII.
        logger.Information("Signup {@User}", new { Name = "Grace", Email = "grace@example.com" });

        var structure = Assert.IsType<StructureValue>(sink.Last.Properties["User"]);
        var email = structure.Properties.Single(p => p.Name == "Email");
        Assert.Equal("[REDACTED]", Assert.IsType<ScalarValue>(email.Value).Value);
        Assert.DoesNotContain("grace@example.com", sink.LastMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void redacts_pii_inside_a_list_element()
    {
        var (logger, sink) = BuildLogger();

        // Box the array as a single positional argument so Serilog binds it to {Recipients} as one
        // SequenceValue, rather than spreading it across the params array.
        logger.Information("Recipients {Recipients}", (object)Recipients);

        var sequence = Assert.IsType<SequenceValue>(sink.Last.Properties["Recipients"]);
        Assert.All(sequence.Elements, e => Assert.Equal("[REDACTED]", Assert.IsType<ScalarValue>(e).Value));
        Assert.DoesNotContain("heidi@example.com", sink.LastMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("ivan@example.com", sink.LastMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void redacts_pii_inside_a_dictionary_value()
    {
        var (logger, sink) = BuildLogger();

        // A dictionary destructures into a DictionaryValue; the PII rides in the value, keyed by "primary".
        logger.Information(
            "Contacts {@Contacts}",
            new Dictionary<string, string> { ["primary"] = "judy@example.com" });

        var dictionary = Assert.IsType<DictionaryValue>(sink.Last.Properties["Contacts"]);
        var entry = dictionary.Elements.Single(e => (string?)e.Key.Value == "primary");
        Assert.Equal("[REDACTED]", Assert.IsType<ScalarValue>(entry.Value).Value);
        Assert.DoesNotContain("judy@example.com", sink.LastMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void preserves_a_non_string_scalar_and_the_structure_shape()
    {
        var (logger, sink) = BuildLogger();

        // A nested non-string scalar is left untouched and the destructured shape (type tag, property
        // names, order) is rebuilt unchanged even when a sibling string is redacted.
        logger.Information("Account {@Account}", new { Email = "mallory@example.com", Age = 42 });

        var structure = Assert.IsType<StructureValue>(sink.Last.Properties["Account"]);
        Assert.Equal(ExpectedAccountPropertyNames, structure.Properties.Select(p => p.Name).ToArray());
        Assert.Equal("[REDACTED]", Assert.IsType<ScalarValue>(structure.Properties[0].Value).Value);
        Assert.Equal(42, Assert.IsType<ScalarValue>(structure.Properties[1].Value).Value);
    }
}
