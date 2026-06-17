namespace Moongazing.OrionShade.Benchmarks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Moongazing.OrionShade;
using Moongazing.OrionShade.Diagnostics;
using Moongazing.OrionShade.Redaction;

/// <summary>
/// Measures <see cref="Redactor.RedactValue"/>, which branches on the key name: a sensitive key
/// short-circuits to the key mask, while any other key falls through to the full pattern sweep.
/// The two branches have very different cost, so they are benchmarked separately.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net90)]
public class RedactValueBenchmarks
{
    private Redactor redactor = null!;
    private ShadeDiagnostics diagnostics = null!;

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

    /// <summary>Sensitive key: keyset hit, value masked wholesale without running any pattern.</summary>
    [Benchmark(Baseline = true)]
    public string SensitiveKey() => redactor.RedactValue("authorization", "Bearer abcdef0123456789");

    /// <summary>Non-sensitive key whose value still matches a pattern: falls through to Redact.</summary>
    [Benchmark]
    public string OrdinaryKeyMatchingValue() => redactor.RedactValue("contact", "jane@acme.com");

    /// <summary>Non-sensitive key with a clean value: full pattern sweep that matches nothing.</summary>
    [Benchmark]
    public string OrdinaryKeyCleanValue() => redactor.RedactValue("city", "Amsterdam");
}
