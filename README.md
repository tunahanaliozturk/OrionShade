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
  regex match with a mask. The built-in rules cover email, IBAN, phone, Luhn-valid card numbers,
  JWTs, and connection-string secrets.
- **By key name.** `RedactValue(key, value)` first checks the `SensitiveKeyset`. If the key is
  sensitive (`password`, `authorization`, `ssn`, ...) the whole value is masked with the key mask.
  Otherwise the value falls through to the same pattern sweep as `Redact`.
- **By JSON structure.** `RedactJson(json)` parses a JSON document and redacts it in place,
  recursing nested objects and arrays. Each string value is redacted in the context of the property
  that owns it, so a sensitive key masks its value wholesale and any other string passes through the
  pattern rules. Numbers, booleans, and nulls are left untouched, and the structure is preserved.
  Input that is not valid JSON falls back to free-text `Redact`.

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

- **Three redaction modes.** Pattern matching inside free text, whole-value masking by sensitive
  key name, and structure-preserving redaction of a JSON document, all exposed through a single
  `IRedactor`.
- **Source-generated built-in rules.** Email, IBAN, phone (keeps the last two digits), credit card
  (keeps the last four digits, masked only when the run passes the Luhn check), JWT, and
  connection-string secrets (`Password=`, `Pwd=`, `AccountKey=`, `SharedAccessKey=`, `Secret=`,
  masking the value while keeping the key), compiled at build time via `[GeneratedRegex]`, not at
  runtime. Phone is matched before credit card so a `+`-prefixed international number redacts as a
  phone rather than being partially consumed as a card.
- **JSON-aware redaction.** `RedactJson` walks a JSON document with `System.Text.Json`, redacting
  each string leaf in the context of the property that owns it and recursing nested objects and
  arrays. Structure is preserved; non-string values are untouched; invalid JSON falls back to
  free-text redaction.
- **A frozen sensitive-key set.** A sensible default list of credential and PII field names matched
  case-insensitively through a `FrozenSet`, extensible with your own keys.
- **Pluggable mask strategies.** A mask is just a `Func<string, string>`. Ship with `Masks.Full()`,
  `Masks.Full(token)`, and `Masks.KeepLast(n)`, or write your own.
- **Custom pattern rules.** Add your own named rules from a regex string or a compiled `Regex`, each
  with its own mask.
- **First-class DI.** One `AddOrionShade()` call registers the redactor and diagnostics; defaults
  are applied when you pass no configuration.
- **Logging pipeline integration.** `ILoggingBuilder.AddOrionShadeRedaction(...)` redacts the
  formatted message of every log entry before it reaches a sink, built only on
  `Microsoft.Extensions.Logging.Abstractions`. Per-category rule sets let different loggers run
  different rules from the same registration.
- **Serilog integration.** The `OrionShade.Serilog` add-on package redacts Serilog log events before
  any sink sees them: `WriteTo.OrionShadeRedaction(...)` scrubs the message, properties, and exception
  through a sink wrapper, and `Enrich.WithOrionShadeRedaction(...)` scrubs string properties.
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

Register OrionShade with the built-in defaults (email, IBAN, phone, card, JWT, and connection-string
patterns plus the common sensitive keys):

```csharp
builder.Services.AddOrionShade();   // built-in email, IBAN, phone, card, JWT, connection-string rules + common sensitive keys
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
redactor.Redact("pay with 4242 4242 4242 4242");  // "pay with ************4242"
redactor.Redact("mail jane@acme.com");             // "mail [REDACTED]"
redactor.RedactValue("password", "hunter2");       // "[REDACTED]"  (key is sensitive)
redactor.RedactValue("city", "jane@acme.com");     // "[REDACTED]"  (pattern still runs on the value)
redactor.Redact("the quick brown fox");            // unchanged when nothing matches
redactor.Redact("order 1234 5678 9012 3456");      // unchanged: fails the Luhn check, not a card
```

---

## Usage

### Pattern rules

`Redact` applies every configured rule to free text in order and masks each match. With the
defaults, that means email, IBAN, phone, credit card, JWT, and connection-string secrets:

```csharp
redactor.Redact("contact jane@acme.com about card 4242 4242 4242 4242");
// "contact [REDACTED] about card ************4242"

redactor.Redact("ConnectionString: Server=db;Password=P@ssw0rd!;Database=app");
// "ConnectionString: Server=db;Password=[REDACTED];Database=app"
```

The credit-card rule keeps the last four digits and the phone rule keeps the last two; email, IBAN,
and JWT are masked whole. The credit-card rule masks a digit run only when it passes the Luhn check,
so an order id or reference number of the same length is left alone. The connection-string rule
masks the value of a `Password=`, `Pwd=`, `AccountKey=`, `SharedAccessKey=`, or `Secret=` pair while
keeping the key and the rest of the string readable. The IBAN and phone rules run before the
credit-card rule, so a `+`-prefixed international number is claimed by the phone rule (keeping its
last two digits) instead of being partially consumed as a card, and an IBAN is masked whole by its
country-code prefix.

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

### JSON documents

`RedactJson` redacts a JSON document while keeping its shape. It recurses nested objects and arrays
and redacts each string value in the context of the property that owns it: a sensitive key masks its
value wholesale, and any other string passes through the same pattern rules as `Redact`. Numbers,
booleans, and nulls are left as they are.

```csharp
var json = """
    {
      "user": "jane.doe@acme.com",
      "password": "hunter2",
      "attempts": 3,
      "contacts": ["+1 415 555 0100", "team@acme.com"],
      "payment": { "iban": "DE89 3704 0044 0532 0130 00" }
    }
    """;

// RedactJson returns a new string; the input is never mutated, so use the returned value.
var safe = redactor.RedactJson(json);
// "user" and the contact email pass through the pattern rules; "password" and "iban" are masked
// whole by key name; the "+"-prefixed phone is caught by the phone rule; "attempts" stays 3.
```

When the input is not valid JSON, `RedactJson` treats the whole string as free text and runs it
through `Redact` instead, so it is always safe to call on a value that may or may not be JSON.

### Logging pipeline integration

Rather than calling the redactor at each log site, fold it into the `Microsoft.Extensions.Logging`
pipeline so every log entry is scrubbed before it reaches a sink. The integration lives in the core
package and is built only on `Microsoft.Extensions.Logging.Abstractions`, so it adds no concrete sink
dependency. Register your sink providers first and call `AddOrionShadeRedaction` last, because it
decorates the `ILoggerProvider` registrations present at that point:

```csharp
using Moongazing.OrionShade;
using Moongazing.OrionShade.Logging;

var redactor = new OrionShadeBuilder().UseDefaults().Build();

builder.Logging.AddOrionShadeRedaction(redactor);   // one rule set for every category
```

The decorator substitutes the formatter so the rendered message is redacted, while the structured
state and scopes reach the inner logger unchanged. The integration is additive and opt-in: a pipeline
that never calls `AddOrionShadeRedaction`, or one configured with no redactor, logs exactly as
before.

Different categories can run different rule and key sets from a single registration. A category is
matched to a redactor by the longest registered prefix it starts with, falling back to an optional
`DefaultRedactor`; a category that matches nothing is logged unchanged:

```csharp
var audited = new OrionShadeBuilder().UseDefaults().Build();
var diagnostics = new OrionShadeBuilder()
    .AddRule("ticket", @"TICKET-\d+", Masks.Full("[TICKET]"))
    .Build();

builder.Logging.AddOrionShadeRedaction(options => options
    .DefaultRedactor = audited);                 // applied to any category no prefix claims

builder.Logging.AddOrionShadeRedaction(options => options
    .RedactCategory("Audit.", audited)           // audited categories mask emails, cards, ...
    .RedactCategory("Diag.", diagnostics));      // diagnostics categories only mask the ticket id
```

`OrionShadeBuilder.Build()` produces a standalone `IRedactor` from a builder configuration for use
here; pass the shared registered `ShadeDiagnostics` to `Build(diagnostics)` to keep all redaction on
one meter.

### Serilog

For Serilog, the `OrionShade.Serilog` package redacts log events before they reach any sink. It
depends on Serilog and reuses the core redactor, so it ships separately from the core. The complete
seam is a sink wrapper: it rebuilds each event, scrubbing the rendered message (both literal template
text and property-bound values), the string property values, and the exception text:

```csharp
using Moongazing.OrionShade;
using Moongazing.OrionShade.Serilog;

var redactor = new OrionShadeBuilder().UseDefaults().Build();

Log.Logger = new LoggerConfiguration()
    .WriteTo.OrionShadeRedaction(redactor, w => w.Console())   // wraps the Console sink
    .CreateLogger();
```

For the common structured-logging case, where PII reaches the log through a property, a lighter
enricher seam scrubs string property values in place (and so the rendered message wherever a property
carried the secret). An enricher cannot reach a literal in the template or the exception, so use the
sink wrapper above when those matter:

```csharp
Log.Logger = new LoggerConfiguration()
    .Enrich.WithOrionShadeRedaction(redactor)
    .WriteTo.Console()
    .CreateLogger();
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
| `UseDefaults()` | Add the built-in rules (email, IBAN, phone, card, JWT, connection-string secrets) and the default sensitive keys. |
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
| `orionshade.redactions` | `Counter<long>` | `rule` | One per redaction. The tag is the pattern name (for example `email`, `iban`, `phone`, `credit_card`, `jwt`, `connection_string_secret`) or `sensitive_key` for a key-based mask. |

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
recorded in [CHANGELOG.md](CHANGELOG.md). The current release is `0.4.0`; while the package is
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
