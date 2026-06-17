namespace Moongazing.OrionShade.Benchmarks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Moongazing.OrionShade;
using Moongazing.OrionShade.Diagnostics;
using Moongazing.OrionShade.Redaction;

/// <summary>
/// Measures <see cref="Redactor.Redact"/>, the free-text hot path that runs every configured
/// pattern rule (email, card, JWT) over a string. Covers the common cases: text that matches
/// nothing (the rules still scan it), text with several matches, and a longer log-line.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net90)]
public class RedactBenchmarks
{
    private Redactor redactor = null!;
    private ShadeDiagnostics diagnostics = null!;

    private const string Clean =
        "GET /api/orders/4821 completed in 37ms with status 200 for tenant northwind";

    private const string WithMatches =
        "user jane@acme.com paid with 4111 1111 1111 1234 token eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0In0.dBjftJeZ4CVP";

    private const string LongLine =
        "incoming request body: {\"name\":\"Jane\",\"email\":\"jane@acme.com\",\"card\":\"4111111111111234\"," +
        "\"notes\":\"contact me at jane.doe@example.org or admin@acme.io\",\"trace\":\"no secrets here just plain text\"," +
        "\"more\":\"padding padding padding padding padding padding padding padding padding\"}";

    [GlobalSetup]
    public void Setup()
    {
        diagnostics = new ShadeDiagnostics();
        redactor = new Redactor(
            BuiltInRules.All,
            SensitiveKeyset.Default,
            Masks.Full(),
            diagnostics);
    }

    [GlobalCleanup]
    public void Cleanup() => diagnostics.Dispose();

    [Benchmark(Baseline = true)]
    public string CleanText() => redactor.Redact(Clean);

    [Benchmark]
    public string TextWithMatches() => redactor.Redact(WithMatches);

    [Benchmark]
    public string LongLogLine() => redactor.Redact(LongLine);
}
