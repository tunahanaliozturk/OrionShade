<p align="center">
  <img src="docs/logo.png" alt="OrionShade" width="150" />
</p>

# OrionShade

[![CI/CD](https://github.com/tunahanaliozturk/OrionShade/actions/workflows/ci-cd.yml/badge.svg)](https://github.com/tunahanaliozturk/OrionShade/actions/workflows/ci-cd.yml)
[![NuGet](https://img.shields.io/nuget/v/OrionShade.svg)](https://www.nuget.org/packages/OrionShade/)

Sensitive-data redaction for .NET. Mask secrets and PII before they reach your logs, your traces,
or a support ticket, both by the shape of the data (an email, a card number, a JWT) and by the
field it lives in (a `password`, an `authorization` header, an `ssn`).

Part of the **Orion** family. Usable entirely on its own.

---

## Why

Secrets leak through logs. Someone logs a whole request body, or an exception that captured a
header, and now a token is sitting in your log store. OrionShade gives you one place to scrub text
before it is written: a small set of pattern rules for the obvious shapes, and a key list for the
fields that are sensitive no matter what they contain.

It does not try to be a full data-loss-prevention engine. The built-in rules are deliberately
conservative, aimed at catching obvious leaks on the logging hot path with predictable cost.

---

## How it works

Redaction happens two ways, and `RedactValue` decides between them by the key it is given.

- **By pattern.** `Redact` runs every configured `RedactionRule` over free text, replacing each
  regex match with a mask. The built-in rules cover email, card-like digit runs, and JWTs.
- **By key name.** `RedactValue(key, value)` first checks the `SensitiveKeyset`. If the key is
  sensitive (`password`, `authorization`, `ssn`, ...) the whole value is masked with the key mask.
  Otherwise the value falls through to the same pattern sweep as `Redact`.

```
                          ┌──────────────────────────┐
   Redact(text) ─────────▶│  pattern rules (in order)│──▶ masked text
                          │  email · card · jwt · ... │
                          └──────────────────────────┘
                                      ▲
                                      │ key not sensitive
   RedactValue(key, value) ──▶ key sensitive? ──┐
                                      │ yes      │ no
                                      ▼          └──▶ Redact(value)
                                 key mask ──▶ "[REDACTED]"
```

Every match increments the `orionshade.redactions` counter, tagged with the rule that fired, so you
can see what is being caught and how often.

---

## Features

- **Two redaction modes.** Pattern matching inside free text, and whole-value masking by sensitive
  key name, exposed through a single `IRedactor`.
- **Source-generated built-in rules.** Email, credit card (keeps the last four digits), and JWT,
  compiled at build time via `[GeneratedRegex]`, not at runtime.
- **A frozen sensitive-key set.** A sensible default list of credential and PII field names matched
  case-insensitively through a `FrozenSet`, extensible with your own keys.
- **Pluggable mask strategies.** A mask is just a `Func<string, string>`. Ship with `Masks.Full()`,
  `Masks.Full(token)`, and `Masks.KeepLast(n)`, or write your own.
- **Custom pattern rules.** Add your own named rules from a regex string or a compiled `Regex`, each
  with its own mask.
- **First-class DI.** One `AddOrionShade()` call registers the redactor and diagnostics; defaults
  are applied when you pass no configuration.
- **Built-in telemetry.** A `System.Diagnostics.Metrics` meter with a rule-tagged redaction counter,
  ready for any OpenTelemetry exporter.
- **Multi-targeted.** `net8.0`, `net9.0`, and `net10.0`, nullable enabled, warnings as errors.

See [docs/FEATURES.md](docs/FEATURES.md) for the full surface, and
[docs/ROADMAP.md](docs/ROADMAP.md) for where it may go next.

---

## Install

```bash
dotnet add package OrionShade
```

---

## Quick start

Register OrionShade with the built-in defaults (email, card, and JWT patterns plus the common
sensitive keys):

```csharp
builder.Services.AddOrionShade();   // built-in email, card, JWT rules + common sensitive keys
```

Inject `IRedactor` and scrub before you log:

```csharp
using Moongazing.OrionShade;

public sealed class OrderLogger(IRedactor redactor, ILogger<OrderLogger> logger)
{
    public void LogRequest(string body) =>
        logger.LogInformation("Incoming: {Body}", redactor.Redact(body));

    public void LogField(string name, string value) =>
        logger.LogInformation("{Field} = {Value}", name, redactor.RedactValue(name, value));
}
```

What the two methods do:

```csharp
redactor.Redact("pay with 4111 1111 1111 1234");  // "pay with ************1234"
redactor.Redact("mail jane@acme.com");             // "mail [REDACTED]"
redactor.RedactValue("password", "hunter2");       // "[REDACTED]"  (key is sensitive)
redactor.RedactValue("city", "jane@acme.com");     // "[REDACTED]"  (pattern still runs on the value)
redactor.Redact("the quick brown fox");            // unchanged when nothing matches
```

---

## Usage

### Pattern rules

`Redact` applies every configured rule to free text in order and masks each match. With the
defaults, that means email, credit card, and JWT:

```csharp
redactor.Redact("contact jane@acme.com about card 4111 1111 1111 1234");
// "contact [REDACTED] about card ************1234"
```

The credit-card rule keeps the last four digits; email and JWT are masked whole.

### The sensitive key set

`RedactValue` masks a value whenever its key is in the sensitive key set, regardless of what the
value contains. Matching is case-insensitive:

```csharp
redactor.RedactValue("Authorization", "Bearer abc.def");  // "[REDACTED]"
redactor.RedactValue("password", "hunter2");              // "[REDACTED]"
redactor.RedactValue("username", "jane");                 // "jane" (not a sensitive key)
```

The default set covers `password`, `passwd`, `pwd`, `secret`, `token`, `authorization`, `auth`,
`apikey`, `api_key`, `access_token`, `refresh_token`, `client_secret`, `ssn`, `creditcard`,
`credit_card`, `cardnumber`, `card_number`, `cvv`, and `pin`.

### Custom rules and keys

Configure the builder instead of taking the defaults. Start from the built-ins with `UseDefaults()`,
then layer your own:

```csharp
using Moongazing.OrionShade;
using Moongazing.OrionShade.Redaction;

builder.Services.AddOrionShade(shade => shade
    .UseDefaults()                                            // start from the built-ins
    .AddSensitiveKeys("national_id", "iban")                  // mask these field values wholesale
    .AddRule("phone", @"\+?\d[\d ]{7,}\d", Masks.KeepLast(2)) // a custom pattern with a partial mask
    .UseKeyMask(Masks.Full("***")));                          // change how sensitive-key values are masked
```

If you do not call `UseDefaults()`, only the rules and keys you add are active. `AddRule` accepts
either a pattern string (compiled case-insensitive and culture-invariant) or a pre-built `Regex`:

```csharp
using System.Text.RegularExpressions;

builder.Services.AddOrionShade(shade => shade
    .AddRule("digits", new Regex(@"\d+"), Masks.Full("#"))
    .AddSensitiveKeys("national_id"));
// Redact("order 12345") => "order #";  an email passes through (defaults not opted in)
```

### Mask strategies

A mask is a `Func<string, string>` that takes the matched text and returns its replacement. The
built-ins live on `Masks`:

```csharp
Masks.Full();              // => "[REDACTED]"  (Masks.DefaultToken)
Masks.Full("***");         // => "***"
Masks.KeepLast(4);         // "4111111111111111" => "************1111"
Masks.KeepLast(4)("1234"); // => "****"  (a value no longer than the visible count is fully masked)
Masks.KeepLast(2, '#');    // mask with '#' instead of '*'
```

`KeepLast` never leaves a short secret in the clear: if the value is no longer than the number of
visible characters requested, it is masked entirely. Anything custom works too:

```csharp
Func<string, string> initials = v => v.Length == 0 ? v : $"{v[0]}…";
builder.Services.AddOrionShade(shade => shade.UseKeyMask(initials));
```

---

## Configuration

`AddOrionShade` is the single entry point. With no argument it applies the defaults; with a
configuration delegate you declare exactly what runs.

| `OrionShadeBuilder` member | Effect |
|----------------------------|--------|
| `UseDefaults()` | Add the built-in rules (email, card, JWT) and the default sensitive keys. |
| `AddRule(name, Regex, mask?)` | Add a pattern rule from a compiled regex. Mask defaults to `Masks.Full()`. |
| `AddRule(name, string, mask?)` | Add a pattern rule from a regex string (compiled `IgnoreCase` + `CultureInvariant`). |
| `AddSensitiveKeys(params string[])` | Mark key names whose values are masked wholesale. |
| `UseKeyMask(Func<string, string>)` | Set the mask used for sensitive-key values. Defaults to `Masks.Full()`. |

The redactor and `ShadeDiagnostics` are registered as singletons via `TryAdd`, so registering twice
is safe and your own `IRedactor` registration wins if it is added first.

---

## Telemetry

OrionShade exposes a `System.Diagnostics.Metrics.Meter` named `Moongazing.OrionShade`
(`ShadeDiagnostics.MeterName`). It publishes one counter:

| Instrument | Type | Tag | Meaning |
|------------|------|-----|---------|
| `orionshade.redactions` | `Counter<long>` | `rule` | One per redaction. The tag is the pattern name (for example `email`, `credit_card`, `jwt`) or `sensitive_key` for a key-based mask. |

Wire it into OpenTelemetry by subscribing to the meter:

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics.AddMeter("Moongazing.OrionShade"));
```

The counter lets you watch which kinds of sensitive data are being caught and how often, without
ever recording the values themselves.

---

## Testing

The library ships with an xUnit suite covering the redactor branches, the mask strategies, the
sensitive-key matching, and DI registration:

```bash
dotnet test
```

A BenchmarkDotNet suite measures the redaction hot paths (free-text redaction, key-based masking,
the mask strategies, each built-in rule, and the keyset lookup). See [benchmarks.md](benchmarks.md)
for the benchmark classes and how to run them. No results are committed; numbers are
hardware-dependent and meant to be produced on the machine you care about.

---

## Design

- Multi-targets `net8.0`, `net9.0`, `net10.0`.
- `TreatWarningsAsErrors`, latest analyzers, nullable enabled, XML docs generated.
- Built-in patterns are source-generated regexes. The rules are deliberately conservative: they
  catch obvious leaks in logs, they are not a full data-loss-prevention engine.
- The sensitive key set is a `FrozenSet` for O(1) case-insensitive lookups.

---

## Versioning

OrionShade follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html). Notable changes are
recorded in [CHANGELOG.md](CHANGELOG.md). The current release is `0.1.0`; while the package is
pre-1.0, minor versions may still adjust the public surface.

---

## Contributing

Issues and pull requests are welcome. Please read [CONTRIBUTING.md](CONTRIBUTING.md) and the
[Code of Conduct](CODE_OF_CONDUCT.md) before opening one.

---

## More from the Orion family

OrionShade is one of a set of standalone .NET libraries:

- [OrionGuard](https://github.com/tunahanaliozturk/OrionGuard) — guard clauses and validation.
- [OrionAudit](https://github.com/tunahanaliozturk/OrionAudit) — automatic EF Core change-audit trail.
- [OrionKey](https://github.com/tunahanaliozturk/OrionKey) — source-generated strongly-typed IDs.

---

## License

Licensed under the [MIT License](LICENSE).

## Author

**Tunahan Ali Ozturk** — [GitHub](https://github.com/tunahanaliozturk)
