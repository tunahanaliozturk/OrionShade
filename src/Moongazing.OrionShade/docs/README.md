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
builder.Services.AddOrionShade();   // built-in email, IBAN, phone, card, JWT, connection-string rules + common sensitive keys
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
redactor.Redact("pay with 4242 4242 4242 4242")   // "pay with ************4242"
redactor.Redact("mail jane@acme.com")              // "mail [REDACTED]"
redactor.RedactValue("password", "hunter2")        // "[REDACTED]"
redactor.RedactValue("city", "jane@acme.com")      // "[REDACTED]"  (pattern still runs on the value)
```

## Built-in coverage

| Rule | Masks | Example result |
|------|-------|----------------|
| `email` | whole address | `[REDACTED]` |
| `iban` | whole account number | `[REDACTED]` |
| `phone` | international number, keeping last 2 | `**********32` |
| `credit_card` | Luhn-valid card runs, keeping last 4 | `************4242` |
| `jwt` | whole token | `[REDACTED]` |
| `connection_string_secret` | secret value of a `key=value` pair, keeping the key | `Password=[REDACTED]` |

The credit-card rule only masks a digit run that passes the Luhn check, so an order id or reference
number of the same length is left alone.

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

## Logging pipeline

Redact in the `Microsoft.Extensions.Logging` pipeline instead of at each call site, built only on
`Microsoft.Extensions.Logging.Abstractions`. Register your sink providers first and call this last:

```csharp
var redactor = new OrionShadeBuilder().UseDefaults().Build();

builder.Logging.AddOrionShadeRedaction(redactor);   // every category, one rule set
```

Different categories can run different rule sets from one registration (longest matching prefix
wins, with an optional default):

```csharp
builder.Logging.AddOrionShadeRedaction(options => options
    .RedactCategory("Audit.", new OrionShadeBuilder().UseDefaults().Build())
    .RedactCategory("Diag.",  new OrionShadeBuilder().AddRule("ticket", @"TICKET-\d+", Masks.Full("[TICKET]")).Build()));
```

The formatted message is scrubbed before any sink writes it; structured state reaches the inner
logger unchanged. With no redactor configured the integration is inert, so logging is unchanged until
you opt in. A Serilog enricher is planned as a separate package.

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
