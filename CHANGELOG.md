<!-- markdownlint-disable MD024 -->

# Changelog

All notable changes to OrionShade are documented in this file. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.0.0/) and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.2.1] - 2026-06-20

### Performance

Allocation cuts on the redaction hot path. Output is byte-identical and every existing test passes
unchanged.

- `Redactor.Redact`: probe each pattern with `Regex.IsMatch` before building the replacement
  evaluator. On text that matches nothing (the common case for most logged values) this skips
  allocating the per-rule `MatchEvaluator` closure entirely. Measured on the clean built-in path:
  allocations drop from 480 to 160 bytes per call (about 67 percent less). `Regex.Replace` already
  returns the same string on a no-match input, so the result is identical.
- `Redactor.RedactJson`: decode the redacted document straight from the `MemoryStream` backing
  buffer with `GetBuffer()` instead of copying it out with `ToArray()`, removing one full-length
  array allocation and copy per call.

## [0.2.0] - 2026-06-19

### Added

Structured redaction and two more built-in rules.

- `IRedactor.RedactJson` / `Redactor.RedactJson`: redacts a JSON document with `System.Text.Json`,
  preserving structure while masking string values. Recurses nested objects and arrays; each string
  is redacted in the context of the property that owns it (a sensitive key masks its value
  wholesale, any other string runs through the pattern rules). Non-string leaves are preserved and
  input that is not valid JSON falls back to free-text redaction.
- `BuiltInRules.Iban`: source-generated rule that masks IBAN account numbers entirely.
- `BuiltInRules.Phone`: source-generated rule that masks phone numbers, keeping the last two digits.
  Both rules are part of `BuiltInRules.All`, so they apply wherever the defaults are used.

## [0.1.0] - 2026-06-15

### Added

Initial release. Sensitive-data redaction.

- `IRedactor` / `Redactor`: applies pattern rules to free text and masks values whose key names are
  sensitive.
- `BuiltInRules`: source-generated email, credit-card (keep-last-4), and JWT rules.
- `SensitiveKeyset`: case-insensitive key matching with a sensible default credential/PII list.
- `Masks`: `Full`, `Full(token)`, and `KeepLast(n)` strategies (a mask is a `Func<string,string>`).
- `RedactionRule`: a named regex plus a mask.
- `ShadeDiagnostics`: `Moongazing.OrionShade` meter with a rule-tagged redaction counter.
- `AddOrionShade()` DI extension with a rule/key builder; defaults applied when unconfigured.

### Tests

17 tests across the masks, the redactor (email, card, JWT, clean text, sensitive keys, custom
rule), the keyset, and registration.

[0.2.1]: https://github.com/tunahanaliozturk/OrionShade/releases/tag/v0.2.1
[0.2.0]: https://github.com/tunahanaliozturk/OrionShade/releases/tag/v0.2.0
[0.1.0]: https://github.com/tunahanaliozturk/OrionShade/releases/tag/v0.1.0
