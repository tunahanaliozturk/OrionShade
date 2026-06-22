<!-- markdownlint-disable MD024 -->

# Changelog

All notable changes to OrionShade are documented in this file. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.0.0/) and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.3.0] - 2026-06-22

### Added

Two more built-in rules and a Luhn refinement of the card rule. All are part of `BuiltInRules.All`,
so they apply wherever the defaults are used, and each stays individually addressable.

- `BuiltInRules.ConnectionStringSecret`: source-generated rule that masks the secret value of a
  connection-string credential pair (`Password=`, `Pwd=`, `AccountKey=`, `SharedAccessKey=`,
  `Secret=`, matched case-insensitively) while leaving the key and the rest of the connection string
  readable. The value runs to the next `;` delimiter or the end of the text, so a base64 account key
  is masked whole including its `=` padding. It is ordered first in `All` so a secret value is masked
  as a unit before any inner pattern could partially rewrite it.

### Changed

- `BuiltInRules.CreditCard` now masks a candidate digit run only when it is a valid Luhn (mod 10)
  sequence. A 13-to-16 digit run that fails the checksum (an order id, a reference number) is left in
  the clear instead of being masked, cutting false positives. A run that is not masked is not counted
  in telemetry. This narrows what the default card rule masks: a digit run that was masked before but
  is not a valid card number is no longer masked. The rule keeps its `credit_card` name, its position,
  and its keep-last-four behaviour for genuine cards.
- `Redactor.Redact` records a redaction only when a rule's mask actually changes the matched text. A
  value-gated rule (the Luhn card check) can match a candidate with its pattern yet decline to mask
  it; the counter now reflects what was masked rather than what was examined. Output is unchanged for
  every rule whose mask always transforms a match (every built-in mask before this release).

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

[0.3.0]: https://github.com/tunahanaliozturk/OrionShade/releases/tag/v0.3.0
[0.2.1]: https://github.com/tunahanaliozturk/OrionShade/releases/tag/v0.2.1
[0.2.0]: https://github.com/tunahanaliozturk/OrionShade/releases/tag/v0.2.0
[0.1.0]: https://github.com/tunahanaliozturk/OrionShade/releases/tag/v0.1.0
