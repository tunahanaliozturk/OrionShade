namespace Moongazing.OrionShade.Benchmarks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Moongazing.OrionShade.Redaction;

/// <summary>
/// Measures <see cref="SensitiveKeyset.IsSensitive"/>, the case-insensitive frozen-set lookup that
/// gates every <see cref="Redactor.RedactValue"/> call. Both a hit and a miss are measured, since a
/// miss is the path that then pays for the full pattern sweep.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net90)]
public class SensitiveKeysetBenchmarks
{
    private SensitiveKeyset keyset = null!;

    [GlobalSetup]
    public void Setup() => keyset = SensitiveKeyset.Default;

    /// <summary>Exact-case hit.</summary>
    [Benchmark(Baseline = true)]
    public bool Hit() => keyset.IsSensitive("password");

    /// <summary>Mixed-case hit, exercising the ordinal-ignore-case comparer.</summary>
    [Benchmark]
    public bool HitIgnoreCase() => keyset.IsSensitive("Authorization");

    /// <summary>Miss: the key is not sensitive, so the caller falls through to pattern redaction.</summary>
    [Benchmark]
    public bool Miss() => keyset.IsSensitive("city");
}
