# Benchmarks

Micro-benchmarks for OrionShade's in-memory redaction hot paths, built with
[BenchmarkDotNet](https://benchmarkdotnet.org/). Everything here runs against the public API with no
database or external service: pattern redaction, key-based value masking, the mask strategies, the
built-in regex rules, and the sensitive-key lookup.

The project lives in `benchmarks/Moongazing.OrionShade.Benchmarks` and references the library
directly.

## Benchmark classes

| Class | What it measures |
|-------|------------------|
| `RedactBenchmarks` | `Redactor.Redact` over free text: a clean line (no matches), a line with several matches, and a longer log line. The rules still scan text that matches nothing, so the clean case is meaningful. |
| `RedactValueBenchmarks` | `Redactor.RedactValue` on both branches: a sensitive key (short-circuit to the key mask) versus an ordinary key that falls through to the full pattern sweep, with both a matching and a clean value. |
| `MaskBenchmarks` | The mask strategies in isolation: `Masks.Full()` (returns a constant) versus `Masks.KeepLast(4)` (allocates a new string per call). |
| `BuiltInRuleBenchmarks` | Each built-in source-generated regex rule on its own (email, credit card, JWT), to compare the relative cost of the patterns that `Redact` chains. |
| `SensitiveKeysetBenchmarks` | `SensitiveKeyset.IsSensitive`, the case-insensitive frozen-set lookup: an exact-case hit, a mixed-case hit, and a miss. |

Each class uses `[MemoryDiagnoser]` and runs on .NET 8 and .NET 9 via `[SimpleJob]`.

## Running

From the repository root:

```
dotnet run -c Release --project benchmarks/Moongazing.OrionShade.Benchmarks
```

BenchmarkDotNet's switcher will prompt for which benchmarks to run. To run everything
non-interactively:

```
dotnet run -c Release --project benchmarks/Moongazing.OrionShade.Benchmarks -- --filter "*"
```

Run a single class, for example just the free-text redaction benchmarks:

```
dotnet run -c Release --project benchmarks/Moongazing.OrionShade.Benchmarks -- --filter "*RedactBenchmarks*"
```

Running on both runtimes requires the .NET 8 and .NET 9 runtimes to be installed. Results are
written to `BenchmarkDotNet.Artifacts` under the working directory.

No results are committed to the repository: numbers are hardware-dependent and meant to be produced
on the machine you care about.
