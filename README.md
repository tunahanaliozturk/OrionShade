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

## Why

Secrets leak through logs. Someone logs a whole request body, or an exception that captured a
header, and now a token is sitting in your log store. OrionShade gives you one place to scrub text
before it is written: a small set of pattern rules for the obvious shapes, and a key list for the
fields that are sensitive no matter what they contain.

## Install

```
dotnet add package OrionShade
```

## Quick start

```csharp
builder.Services.AddOrionShade();   // built-in email, card, JWT rules + common sensitive keys
```

```csharp
public sealed class OrderLogger(IRedactor redactor, ILogger<OrderLogger> logger)
{
    public void LogRequest(string body) =>
        logger.LogInformation("Incoming: {Body}", redactor.Redact(body));

    public void LogField(string name, string value) =>
        logger.LogInformation("{Field} = {Value}", name, redactor.RedactValue(name, value));
}
```

```
redactor.Redact("pay with 4111 1111 1111 1234")   // "pay with ************1234"
redactor.Redact("mail jane@acme.com")              // "mail [REDACTED]"
redactor.RedactValue("password", "hunter2")        // "[REDACTED]"
redactor.RedactValue("city", "jane@acme.com")      // "[REDACTED]"  (pattern still runs on the value)
```

## Built-in coverage

| Rule | Masks | Example result |
|------|-------|----------------|
| `email` | whole address | `[REDACTED]` |
| `credit_card` | digit runs, keeping last 4 | `************1234` |
| `jwt` | whole token | `[REDACTED]` |

Plus a default sensitive-key list: `password`, `secret`, `token`, `authorization`, `apikey`,
`access_token`, `client_secret`, `ssn`, `card_number`, `cvv`, `pin`, and more.

## Customising

```csharp
builder.Services.AddOrionShade(shade => shade
    .UseDefaults()                                       // start from the built-ins
    .AddSensitiveKeys("national_id", "iban")             // mask these field values wholesale
    .AddRule("phone", @"\+?\d[\d ]{7,}\d", Masks.KeepLast(2))  // a custom pattern with a partial mask
    .UseKeyMask(Masks.KeepLast(0)));                     // change how sensitive-key values are masked
```

A `Mask` is just a `Func<string,string>`: use `Masks.Full()`, `Masks.Full("token")`, or
`Masks.KeepLast(n)`, or write your own.

## Telemetry

Subscribe to the `Moongazing.OrionShade` meter: `orionshade.redactions` is tagged `rule` (the
pattern name, or `sensitive_key`), so you can see what is being caught and how often.

## Design

- Multi-targets `net8.0`, `net9.0`, `net10.0`.
- `TreatWarningsAsErrors`, latest analyzers, nullable enabled.
- Built-in patterns are source-generated regexes. The rules are deliberately conservative: they
  catch obvious leaks in logs, they are not a full data-loss-prevention engine.

## License

MIT.
