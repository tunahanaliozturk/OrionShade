# OrionShade Roadmap

Where OrionShade might go, and how you can help shape it.

The current release is **0.2.1**: rule-based redaction for the logging path, with built-in pattern
rules, a sensitive-key set, JSON-aware redaction, and a low-allocation hot path.

Everything below the "Recently shipped" section is a list of **ideas under consideration, not
promises**. The version milestones are a rough ordering, not committed dates, and items move, merge,
or drop as real-world use teaches us what actually matters. If something listed below would help you,
open an issue and say so. Demonstrated demand is what moves an idea from "interesting" to "next".

For what exists today, see [FEATURES.md](FEATURES.md).

---

## Guiding principles

These constrain everything below. An idea that conflicts with one of these is unlikely to ship.

- **Stay on the logging hot path.** OrionShade runs where strings are about to be written. New
  features must keep cost predictable and allocations low. If a capability needs heavy analysis, it
  belongs behind an explicit opt-in, not in the default `Redact` sweep.
- **Conservative by default.** Built-in rules favour precision over recall: a missed redaction is
  bad, but a tool that mangles every log line is worse and gets turned off. Aggressive matching
  ships opt-in, never as a silent default.
- **Small and dependency-light.** The core has one dependency. Integrations and heavier backends, if
  they happen, live in separate add-on packages rather than weighing down the core.
- **Never log the secret to find the secret.** Telemetry counts and tags; it never records the
  values being redacted. Any future diagnostics keep that line.

---

## Recently shipped

- **JSON-aware redaction (0.2.0).** `RedactJson` walks a JSON document with `System.Text.Json`,
  redacting each string leaf in the context of the property that owns it: a sensitive key masks its
  value wholesale, any other string runs through the pattern rules. Structure is preserved, non-string
  leaves are untouched, and input that is not valid JSON falls back to free-text redaction.
- **IBAN and phone rules (0.2.0).** Two source-generated built-in rules. IBAN masks the account
  number whole by its country-code prefix; phone masks an international `+`-prefixed number keeping
  the last two digits. Both are in `BuiltInRules.All`, and both are matched before the credit-card
  rule so a compact international number is not partly consumed as a card.
- **No-match fast path (0.2.1).** `Redact` probes each pattern with `Regex.IsMatch` before building
  the replacement evaluator, so text that matches nothing skips the per-rule closure entirely.
  Output is byte-identical; measured allocations on the clean built-in path drop from 480 to 160
  bytes per call. `RedactJson` now decodes from the stream's backing buffer rather than copying it
  out, removing one array allocation per call.
- **Self-deriving meter version (0.2.1).** The diagnostics meter reads its version from the assembly
  informational version, so the meter version can no longer drift from the package version.

---

## Ideas under consideration

### Next (0.3.x) - more built-in rules

The built-in set today is email, IBAN, phone, credit card, and JWT. Each candidate below ships
opt-in and needs a precision story (a real false-positive analysis) before it lands.

- **Credit-card refinement.** Add an optional Luhn check so the card rule rejects digit runs that
  cannot be card numbers, cutting false positives on order ids and reference numbers.
- **National identifiers.** Region-specific rules in the spirit of the existing `ssn` key (for
  example US SSN, UK NINO) grouped so a consumer opts into a region rather than the whole world.
- **Auth headers and connection strings.** Generic `Bearer`/`Basic` authorization header values, and
  the secret-bearing parts of common connection strings (`Password=`, `Pwd=`, `AccountKey=`),
  masking the value while leaving the surrounding text readable.
- **IP addresses.** IPv4 and IPv6, opt-in, for teams that treat client addresses as personal data.

### Sensitive keys (0.3.x)

- **Key-pattern matching.** Let a sensitive "key" be a prefix, suffix, or pattern (for example
  anything ending in `_token`) instead of only an exact name, so a new `*_secret` field is covered
  without editing the list.
- **Per-rule and per-key control.** Take the defaults but drop or swap a single rule or key without
  rebuilding the whole list by hand. The builder today is add-only; this needs a remove/replace path.
- **Curated key packs.** Optional larger key sets for specific domains (finance, health) layered on
  top of the conservative default.

### Integrations (0.4.x)

- **`Microsoft.Extensions.Logging` enricher.** An `ILogger`-side hook that runs redaction over the
  message and its arguments as part of the logging pipeline rather than at each call site. Ships as a
  separate `OrionShade.Extensions.Logging` package to keep the core dependency-light.
- **Serilog enricher / destructuring policy.** The equivalent for Serilog, scrubbing properties as
  they are bound, in its own add-on package.
- **Configurable rule sets per sink.** Allow a named redactor configuration so a verbose debug sink
  and an audited sink can run different rule and key sets from the same registration.

### Redaction surface (later)

- **Format-preserving masking.** A mask option that keeps the shape of the value (length, separators,
  digit/letter classes) while removing the content, for cases where downstream tooling expects a
  well-formed-looking value. One-way only, in line with the out-of-scope note below.
- **Streaming and large-payload redaction.** A path that redacts a large JSON or text payload from a
  stream or `ReadOnlySequence<byte>` without first materialising the whole string, for request and
  response logging of big bodies.

### Diagnostics (later)

- **Richer telemetry tags.** Optionally tag the redaction counter with more dimensions (for example
  rule category) while keeping the redacted values out of telemetry entirely.

---

## What is intentionally out of scope

- **Full data-loss-prevention.** OrionShade is a redaction helper for the logging path, not a DLP
  engine. Exhaustive content classification is a different tool.
- **Reversible/tokenized masking.** Masks are one-way by design. Format-preserving encryption or
  reversible tokenization is a separate concern and not planned for the core. The format-preserving
  *masking* idea above keeps a value's shape only; it does not make a mask reversible.
- **Storing or shipping redacted values anywhere.** The library transforms strings in place and
  counts what it did; it does not persist, forward, or sample the data it sees.

---

## Contributing to the roadmap

Open an issue describing the leak you are trying to stop and how you would want OrionShade to help.
Concrete scenarios are far more useful than feature names, and they are what pull an idea up this
list. Pull requests are welcome too; please read [CONTRIBUTING.md](../CONTRIBUTING.md) first.
