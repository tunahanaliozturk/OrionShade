# OrionShade Features

A complete reference for what OrionShade ships today. Every entry below maps to public API in the
`OrionShade` package (root namespace `Moongazing.OrionShade`). For the broader context, see the
[README](../README.md); for where the library may go next, see [ROADMAP.md](ROADMAP.md).

---

## Table of contents

1. [The redactor](#1-the-redactor)
2. [Pattern rules](#2-pattern-rules)
3. [Built-in rules](#3-built-in-rules)
4. [The sensitive key set](#4-the-sensitive-key-set)
5. [Mask strategies](#5-mask-strategies)
6. [Custom rules](#6-custom-rules)
7. [Dependency injection](#7-dependency-injection)
8. [Telemetry](#8-telemetry)
9. [Targets and build settings](#9-targets-and-build-settings)

---

## 1. The redactor

`IRedactor` is the single surface you inject. It has two methods.

```csharp
public interface IRedactor
{
    string Redact(string input);
    string RedactValue(string key, string value);
}
```

- `Redact(input)` applies every configured pattern rule to free text, masking each match, and
  returns the input unchanged when nothing matches. An empty string is returned as-is.
- `RedactValue(key, value)` masks the whole value when `key` is in the sensitive key set; otherwise
  it runs the same pattern sweep as `Redact` on the value.

Both methods throw `ArgumentNullException` on null arguments.

The default implementation, `Redactor`, is constructed from an ordered rule list, a
`SensitiveKeyset`, a key mask (`Func<string, string>`), and a `ShadeDiagnostics` instance. You
normally let DI build it, but the constructor is public if you want to wire one up by hand.

---

## 2. Pattern rules

A `RedactionRule` is a named regex plus a mask:

```csharp
public sealed class RedactionRule
{
    public RedactionRule(string name, Regex pattern, Func<string, string> mask);
    public string Name { get; }
    public Regex Pattern { get; }
    public Func<string, string> Mask { get; }
}
```

`Redact` runs each rule in the order it was added, replacing every match with `Mask(match.Value)`.
The `Name` doubles as the telemetry tag, so it shows up on the redaction counter when the rule
fires.

---

## 3. Built-in rules

`BuiltInRules` exposes three source-generated rules and an `All` collection. The patterns are
compiled at build time via `[GeneratedRegex]`.

| Rule | Property | Pattern target | Mask |
|------|----------|----------------|------|
| Email | `BuiltInRules.Email` | Email addresses | `Masks.Full()` (whole match) |
| Credit card | `BuiltInRules.CreditCard` | Card-like digit runs (4-4-4-1..4, optional spaces/dashes) | `Masks.KeepLast(4)` |
| JWT | `BuiltInRules.Jwt` | `eyJ...`-style three-part tokens | `Masks.Full()` (whole match) |

`BuiltInRules.All` returns `[Email, CreditCard, Jwt]`, the same set `UseDefaults()` installs.

The rules are intentionally conservative. They are tuned to catch obvious leaks on a logging hot
path with predictable cost, not to be an exhaustive PII detector.

---

## 4. The sensitive key set

`SensitiveKeyset` decides whether a value should be masked because of the field it belongs to,
rather than because of a pattern it matches. Lookups are case-insensitive through a `FrozenSet`.

```csharp
public sealed class SensitiveKeyset
{
    public SensitiveKeyset(IEnumerable<string> keys);
    public static SensitiveKeyset Default { get; }
    public bool IsSensitive(string key);
}
```

`SensitiveKeyset.Default` (also what `UseDefaults()` installs) contains:

`password`, `passwd`, `pwd`, `secret`, `token`, `authorization`, `auth`, `apikey`, `api_key`,
`access_token`, `refresh_token`, `client_secret`, `ssn`, `creditcard`, `credit_card`, `cardnumber`,
`card_number`, `cvv`, `pin`.

Add your own through the builder's `AddSensitiveKeys(...)`.

---

## 5. Mask strategies

A mask is a `Func<string, string>`: it receives the matched text and returns the replacement. The
built-ins live on `Masks`.

| Member | Behaviour |
|--------|-----------|
| `Masks.DefaultToken` | The constant `"[REDACTED]"`. |
| `Masks.Full()` | Replace the whole value with `DefaultToken`. |
| `Masks.Full(token)` | Replace the whole value with a fixed token. |
| `Masks.KeepLast(visible, maskChar = '*')` | Keep the last `visible` characters, mask the rest. |

`KeepLast` is safe with short input: when the value is no longer than `visible`, it is masked in
full, so a short secret is never left in the clear. A negative `visible` throws
`ArgumentOutOfRangeException`.

Because a mask is a plain delegate, you can supply any custom strategy where a `Func<string, string>`
is accepted.

---

## 6. Custom rules

`OrionShadeBuilder` lets you compose the rule list, the sensitive keys, and the key mask.

```csharp
public sealed class OrionShadeBuilder
{
    public OrionShadeBuilder UseDefaults();
    public OrionShadeBuilder AddRule(string name, Regex pattern, Func<string, string>? mask = null);
    public OrionShadeBuilder AddRule(string name, string pattern, Func<string, string>? mask = null);
    public OrionShadeBuilder AddSensitiveKeys(params string[] keys);
    public OrionShadeBuilder UseKeyMask(Func<string, string> mask);
}
```

- The `string` overload of `AddRule` compiles the pattern with `RegexOptions.IgnoreCase` and
  `RegexOptions.CultureInvariant`; an empty pattern throws.
- When no `mask` is given, the rule uses `Masks.Full()`.
- `UseDefaults()` is additive: call it first, then layer your own rules and keys on top. Skip it to
  run only what you add.

---

## 7. Dependency injection

A single extension method registers everything.

```csharp
public static IServiceCollection AddOrionShade(
    this IServiceCollection services,
    Action<OrionShadeBuilder>? configure = null);
```

- With no `configure` delegate, the built-in defaults are applied (`UseDefaults()`).
- With a delegate, exactly what you declare is active.
- `IRedactor` and `ShadeDiagnostics` are registered as singletons via `TryAdd`, so the call is safe
  to repeat and an earlier `IRedactor` registration of your own takes precedence.

---

## 8. Telemetry

`ShadeDiagnostics` owns a `System.Diagnostics.Metrics.Meter` named `Moongazing.OrionShade`
(`ShadeDiagnostics.MeterName`).

| Instrument | Type | Tag | Meaning |
|------------|------|-----|---------|
| `orionshade.redactions` | `Counter<long>` | `rule` | One increment per redaction; tag is the rule name, or `sensitive_key` for a key-based mask. |

The type is registered as a singleton and is `IDisposable` (disposing releases the meter). Subscribe
to the meter name from any OpenTelemetry metrics pipeline to observe redaction activity without ever
capturing the redacted values.

---

## 9. Targets and build settings

- Multi-targets `net8.0`, `net9.0`, and `net10.0`.
- Nullable reference types enabled; implicit usings enabled.
- `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`, latest-recommended analysis level.
- XML documentation generated for the public API.
- One runtime dependency: `Microsoft.Extensions.DependencyInjection.Abstractions`.
