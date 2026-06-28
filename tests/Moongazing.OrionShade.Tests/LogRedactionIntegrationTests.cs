namespace Moongazing.OrionShade.Tests;

using Microsoft.Extensions.DependencyInjection;
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

    [Fact]
    public void An_exception_carrying_pii_is_redacted_before_it_reaches_the_sink()
    {
        var sink = new CapturingLoggerProvider();
        var redactor = new OrionShadeBuilder().UseDefaults().Build();

        using var factory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(sink);
            builder.AddOrionShadeRedaction(redactor);
        });

        // The exception's message and rendered text carry an email; the formatted log message itself
        // does not. Only redacting the message would leak the email through the exception.
        var exception = new InvalidOperationException("failed for jane@acme.com");
        factory.CreateLogger("App.Orders").LogError(exception, "request failed");

        var entry = Assert.Single(sink.Entries);
        Assert.NotNull(entry.ExceptionText);
        Assert.DoesNotContain("jane@acme.com", entry.ExceptionText, StringComparison.Ordinal);
        Assert.Contains(Masks.DefaultToken, entry.ExceptionText, StringComparison.Ordinal);

        // Structure is preserved: the original exception type name still appears in the rendered text.
        Assert.Contains(nameof(InvalidOperationException), entry.ExceptionText, StringComparison.Ordinal);
    }

    [Fact]
    public void Pii_in_an_inner_exception_is_redacted_too()
    {
        var sink = new CapturingLoggerProvider();
        var redactor = new OrionShadeBuilder().UseDefaults().Build();

        using var factory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(sink);
            builder.AddOrionShadeRedaction(redactor);
        });

        var cause = new InvalidOperationException("inner leak bob@acme.com");
        var outer = new InvalidOperationException("outer failure", cause);
        factory.CreateLogger("App.Orders").LogError(outer, "request failed");

        var entry = Assert.Single(sink.Entries);
        Assert.NotNull(entry.ExceptionText);
        Assert.DoesNotContain("bob@acme.com", entry.ExceptionText, StringComparison.Ordinal);
    }

    [Fact]
    public void Calling_the_integration_twice_redacts_only_once()
    {
        var sink = new CapturingLoggerProvider();

        // A deliberately non-idempotent rule: the match "secret" is replaced with "secret#", which still
        // contains "secret". One redaction layer therefore yields a single trailing '#'; a second
        // stacked layer would re-match the masked text and append another, so the number of '#' counts
        // the layers.
        var redactor = new OrionShadeBuilder()
            .AddRule("marker", "secret", _ => "secret#")
            .Build();

        using var factory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(sink);

            // Register the integration twice. The second call must not stack another decorator.
            builder.AddOrionShadeRedaction(redactor);
            builder.AddOrionShadeRedaction(redactor);
        });

        factory.CreateLogger("App.Orders").LogInformation("the {Value}", "secret");

        var message = Assert.Single(sink.Messages);

        // Exactly one layer ran: a single appended '#'. Two layers would produce "secret##".
        Assert.Equal("the secret#", message);
    }

    [Fact]
    public void A_provider_alias_keyed_log_level_filter_still_applies_through_the_decorator()
    {
        var aliased = new AliasedCapturingProvider();
        var redactor = new OrionShadeBuilder().UseDefaults().Build();

        using var serviceProvider = new ServiceCollection()
            .AddLogging(builder =>
            {
                builder.AddProvider(aliased);
                builder.AddOrionShadeRedaction(redactor);

                // A provider-alias-keyed rule: suppress everything below Error for the "Probe" provider.
                // MEL resolves this rule by the provider's [ProviderAlias], read off the runtime type of
                // the instance it holds. Without the decorator re-stamping that alias the rule would no
                // longer match and the Information entry would leak through.
                builder.Services.Configure<LoggerFilterOptions>(options =>
                    options.Rules.Add(new LoggerFilterRule(
                        providerName: AliasedCapturingProvider.Alias,
                        categoryName: null,
                        logLevel: LogLevel.Error,
                        filter: null)));
            })
            .BuildServiceProvider();

        var factory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var logger = factory.CreateLogger("App.Orders");

        logger.LogInformation("dropped {Email}", "jane@acme.com");
        logger.LogError("kept");

        // The alias filter suppressed the Information entry; only the Error survives.
        var entry = Assert.Single(aliased.Captured.Entries);
        Assert.Equal("kept", entry.Message);
    }

    [Fact]
    public void External_scope_is_forwarded_to_the_inner_provider_through_the_decorator()
    {
        var sink = new CapturingLoggerProvider();
        var redactor = new OrionShadeBuilder().UseDefaults().Build();

        using var serviceProvider = new ServiceCollection()
            .AddLogging(builder =>
            {
                builder.Configure(o => o.ActivityTrackingOptions = ActivityTrackingOptions.None);
                builder.AddProvider(sink);
                builder.AddOrionShadeRedaction(redactor);
            })
            .BuildServiceProvider();

        var factory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var logger = factory.CreateLogger("App.Orders");

        using (logger.BeginScope("scope-value"))
        {
            logger.LogInformation("inside scope");
        }

        // The factory pushed an external scope provider through the decorator to the sink, so the scope
        // value reaches the sink and is rendered.
        Assert.True(sink.ScopeProviderWasSet);
        var entry = Assert.Single(sink.Entries);
        Assert.Contains("scope-value", entry.ScopeText, StringComparison.Ordinal);
    }
}

/// <summary>
/// A capturing sink that carries a <see cref="ProviderAliasAttribute"/> so a test can verify that a
/// provider-alias-keyed filter still resolves through the OrionShade decorator. Delegates capture to an
/// inner <see cref="CapturingLoggerProvider"/>.
/// </summary>
[ProviderAlias(Alias)]
internal sealed class AliasedCapturingProvider : ILoggerProvider, ISupportExternalScope
{
    public const string Alias = "Probe";

    public CapturingLoggerProvider Captured { get; } = new();

    public ILogger CreateLogger(string categoryName) => Captured.CreateLogger(categoryName);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) =>
        Captured.SetScopeProvider(scopeProvider);

    public void Dispose() => Captured.Dispose();
}
