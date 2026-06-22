namespace Moongazing.OrionShade.Benchmarks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Moongazing.OrionShade.Redaction;

/// <summary>
/// Measures each built-in source-generated regex rule on its own, applying a single rule's pattern
/// and mask to a representative value. Isolating the rules shows the relative cost of the email,
/// card, and JWT patterns that <see cref="Redactor.Redact"/> chains together.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net90)]
public class BuiltInRuleBenchmarks
{
    private const string EmailInput = "please reach jane@acme.com for billing questions";
    private const string CardInput = "charged card 4242 4242 4242 4242 for the order";
    private const string JwtInput = "auth eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.dBjftJeZ4CVPmB92K27uhbUJU1p1r";

    private static string Apply(RedactionRule rule, string input) =>
        rule.Pattern.Replace(input, m => rule.Mask(m.Value));

    [Benchmark(Baseline = true)]
    public string Email() => Apply(BuiltInRules.Email, EmailInput);

    [Benchmark]
    public string CreditCard() => Apply(BuiltInRules.CreditCard, CardInput);

    [Benchmark]
    public string Jwt() => Apply(BuiltInRules.Jwt, JwtInput);
}
