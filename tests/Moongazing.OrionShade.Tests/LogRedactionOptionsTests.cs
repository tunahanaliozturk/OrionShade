namespace Moongazing.OrionShade.Tests;

using Moongazing.OrionShade;
using Moongazing.OrionShade.Logging;

using Xunit;

/// <summary>
/// Covers <see cref="LogRedactionOptions"/> category-to-redactor resolution: longest-prefix wins,
/// the default fallback, and the unconfigured (no redactor) case.
/// </summary>
public sealed class LogRedactionOptionsTests
{
    private static IRedactor NewRedactor() => new OrionShadeBuilder().UseDefaults().Build();

    [Fact]
    public void ResolveFor_returns_the_default_when_no_prefix_matches()
    {
        var fallback = NewRedactor();
        var options = new LogRedactionOptions { DefaultRedactor = fallback };
        options.RedactCategory("Audit.", NewRedactor());

        Assert.Same(fallback, options.ResolveFor("Other.Service"));
    }

    [Fact]
    public void ResolveFor_returns_null_when_nothing_matches_and_no_default()
    {
        var options = new LogRedactionOptions();
        options.RedactCategory("Audit.", NewRedactor());

        Assert.Null(options.ResolveFor("Other.Service"));
    }

    [Fact]
    public void ResolveFor_picks_the_longest_matching_prefix()
    {
        var broad = NewRedactor();
        var specific = NewRedactor();
        var options = new LogRedactionOptions();
        options.RedactCategory("App.", broad);
        options.RedactCategory("App.Secure.", specific);

        Assert.Same(specific, options.ResolveFor("App.Secure.Auth"));
        Assert.Same(broad, options.ResolveFor("App.Orders"));
    }

    [Fact]
    public void ResolveFor_is_case_sensitive_on_the_prefix()
    {
        var redactor = NewRedactor();
        var options = new LogRedactionOptions();
        options.RedactCategory("Audit.", redactor);

        Assert.Null(options.ResolveFor("audit.payments"));
        Assert.Same(redactor, options.ResolveFor("Audit.Payments"));
    }

    [Fact]
    public void RedactCategory_rejects_an_empty_prefix()
    {
        var options = new LogRedactionOptions();
        Assert.Throws<ArgumentException>(() => options.RedactCategory("", NewRedactor()));
    }

    [Fact]
    public void RedactCategory_rejects_a_null_redactor()
    {
        var options = new LogRedactionOptions();
        Assert.Throws<ArgumentNullException>(() => options.RedactCategory("Audit.", null!));
    }
}
