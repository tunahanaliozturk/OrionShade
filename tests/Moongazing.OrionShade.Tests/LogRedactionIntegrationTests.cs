namespace Moongazing.OrionShade.Tests;

using Microsoft.Extensions.Logging;

using Moongazing.OrionShade;
using Moongazing.OrionShade.Logging;
using Moongazing.OrionShade.Redaction;

using Xunit;

/// <summary>
/// Covers the <c>Microsoft.Extensions.Logging</c> redaction integration: a message routed through the
/// MEL pipeline reaches a captured test sink already redacted, per-category rule sets apply the right
/// rules to the right logger, and an unconfigured pipeline passes messages through untouched.
/// </summary>
[Collection(nameof(MeterSerial))]
public sealed class LogRedactionIntegrationTests
{
    [Fact]
    public void Pii_is_redacted_before_it_reaches_the_sink()
    {
        var sink = new CapturingLoggerProvider();
        var redactor = new OrionShadeBuilder().UseDefaults().Build();

        using var factory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(sink);
            builder.AddOrionShadeRedaction(redactor);
        });

        var logger = factory.CreateLogger("App.Orders");
        logger.LogInformation("contact {Email}", "jane@acme.com");

        var message = Assert.Single(sink.Messages);
        Assert.DoesNotContain("jane@acme.com", message, StringComparison.Ordinal);
        Assert.Contains(Masks.DefaultToken, message, StringComparison.Ordinal);
    }

    [Fact]
    public void Different_categories_use_different_rule_sets()
    {
        var sink = new CapturingLoggerProvider();

        // The audited category masks emails; the diagnostics category only masks a custom token and
        // leaves emails in the clear. Same registration, two rule sets.
        var audited = new OrionShadeBuilder().UseDefaults().Build();
        var diagnostics = new OrionShadeBuilder()
            .AddRule("ticket", @"TICKET-\d+", Masks.Full("[TICKET]"))
            .Build();

        using var factory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(sink);
            builder.AddOrionShadeRedaction(options => options
                .RedactCategory("Audit.", audited)
                .RedactCategory("Diag.", diagnostics));
        });

        factory.CreateLogger("Audit.Payments").LogInformation("user {Email}", "jane@acme.com");
        factory.CreateLogger("Diag.Trace").LogInformation("user {Email} on {Ref}", "bob@acme.com", "TICKET-42");

        var auditMessage = sink.Entries.Single(e => e.Category == "Audit.Payments").Message;
        var diagMessage = sink.Entries.Single(e => e.Category == "Diag.Trace").Message;

        // Audited logger ran the email rule.
        Assert.DoesNotContain("jane@acme.com", auditMessage, StringComparison.Ordinal);
        Assert.Contains(Masks.DefaultToken, auditMessage, StringComparison.Ordinal);

        // Diagnostics logger ran only the ticket rule: the email survives, the ticket is masked.
        Assert.Contains("bob@acme.com", diagMessage, StringComparison.Ordinal);
        Assert.Contains("[TICKET]", diagMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("TICKET-42", diagMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void The_longest_matching_category_prefix_wins()
    {
        var sink = new CapturingLoggerProvider();
        var broad = new OrionShadeBuilder().AddRule("digits", @"\d+", Masks.Full("#")).Build();
        var specific = new OrionShadeBuilder().UseDefaults().Build();

        using var factory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(sink);
            builder.AddOrionShadeRedaction(options => options
                .RedactCategory("App.", broad)
                .RedactCategory("App.Secure.", specific));
        });

        factory.CreateLogger("App.Secure.Auth").LogInformation("mail {Email}", "jane@acme.com");

        // The longer "App.Secure." prefix wins, so the email rule (not the digits rule) applies.
        var message = Assert.Single(sink.Messages);
        Assert.DoesNotContain("jane@acme.com", message, StringComparison.Ordinal);
        Assert.Contains(Masks.DefaultToken, message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_category_with_no_matching_redactor_is_left_unchanged()
    {
        var sink = new CapturingLoggerProvider();
        var redactor = new OrionShadeBuilder().UseDefaults().Build();

        using var factory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(sink);
            // Only "Audit." categories are redacted; there is no default redactor.
            builder.AddOrionShadeRedaction(options => options.RedactCategory("Audit.", redactor));
        });

        factory.CreateLogger("Other.Service").LogInformation("mail {Email}", "jane@acme.com");

        var message = Assert.Single(sink.Messages);
        Assert.Contains("jane@acme.com", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Without_the_integration_the_pipeline_does_not_redact()
    {
        var sink = new CapturingLoggerProvider();

        // No AddOrionShadeRedaction call: the message must pass through verbatim.
        using var factory = LoggerFactory.Create(builder => builder.AddProvider(sink));

        factory.CreateLogger("App.Orders").LogInformation("contact {Email}", "jane@acme.com");

        var message = Assert.Single(sink.Messages);
        Assert.Contains("jane@acme.com", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Calling_the_integration_with_no_redactor_configured_does_not_redact()
    {
        var sink = new CapturingLoggerProvider();

        // The integration is wired in but no default or category redactor is set, so it is inert.
        using var factory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(sink);
            builder.AddOrionShadeRedaction();
        });

        factory.CreateLogger("App.Orders").LogInformation("contact {Email}", "jane@acme.com");

        var message = Assert.Single(sink.Messages);
        Assert.Contains("jane@acme.com", message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_provider_registered_before_the_integration_is_decorated()
    {
        var sink = new CapturingLoggerProvider();
        var redactor = new OrionShadeBuilder().UseDefaults().Build();

        using var factory = LoggerFactory.Create(builder =>
        {
            // Sink provider first, redaction last: the contract is to call the integration after the
            // providers it should wrap.
            builder.AddProvider(sink);
            builder.AddOrionShadeRedaction(redactor);
        });

        factory.CreateLogger("App.Orders").LogInformation("mail {Email}", "jane@acme.com");

        var message = Assert.Single(sink.Messages);
        Assert.DoesNotContain("jane@acme.com", message, StringComparison.Ordinal);
    }
}
