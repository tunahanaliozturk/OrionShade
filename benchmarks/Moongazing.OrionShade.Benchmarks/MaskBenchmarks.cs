namespace Moongazing.OrionShade.Benchmarks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Moongazing.OrionShade.Redaction;

/// <summary>
/// Measures the built-in mask strategies in isolation. <see cref="Masks.Full()"/> returns a
/// constant, while <see cref="Masks.KeepLast(int, char)"/> allocates a new string per call. These
/// run once per regex match, so their per-call cost compounds across a redaction.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net90)]
public class MaskBenchmarks
{
    private Func<string, string> full = null!;
    private Func<string, string> keepLast4 = null!;

    private const string CardNumber = "4242424242424242";

    [GlobalSetup]
    public void Setup()
    {
        full = Masks.Full();
        keepLast4 = Masks.KeepLast(4);
    }

    [Benchmark(Baseline = true)]
    public string Full() => full(CardNumber);

    [Benchmark]
    public string KeepLast4() => keepLast4(CardNumber);
}
