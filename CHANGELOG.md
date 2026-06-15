<!-- markdownlint-disable MD024 -->

# Changelog

All notable changes to OrionShade are documented in this file. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.0.0/) and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

[0.1.0]: https://github.com/tunahanaliozturk/OrionShade/releases/tag/v0.1.0
