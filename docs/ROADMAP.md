# OrionShade Roadmap

Where OrionShade might go, and how you can help shape it.

This is a list of **ideas under consideration, not promises**. Nothing here has a committed date,
and items move, merge, or drop as real-world use teaches us what actually matters. If something
listed below would help you, open an issue and say so. Demonstrated demand is what moves an idea
from "interesting" to "next".

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

## Ideas under consideration

### Redaction surface

- **More built-in patterns.** Candidates include IPv4/IPv6 addresses, phone numbers, IBANs, AWS-style
  access keys, and generic `Bearer`/`Basic` auth headers. Each would ship opt-in so the default set
  stays conservative, and each needs a precision story before it lands.
- **Per-rule enable/disable.** A way to take the defaults but drop or swap a single rule, without
  rebuilding the whole list by hand.
- **Span-friendly redaction.** Investigate a path that avoids allocating when nothing matches, and
  reduces intermediate strings when several rules run in sequence.
- **Structured-value masking.** Helpers for redacting a value that is itself JSON or a query string,
  walking keys rather than treating the whole blob as free text.

### Sensitive keys

- **Key-pattern matching.** Allow a sensitive "key" to be a prefix or pattern (for example,
  anything ending in `_token`) instead of only an exact name.
- **Curated key packs.** Optional larger key sets for specific domains (finance, health) that a
  consumer can opt into on top of the conservative default.

### Integrations

- **Logging-framework adapters.** Thin glue for the common sinks so redaction can run as part of the
  logging pipeline rather than at each call site. Would live in separate add-on packages to keep the
  core dependency-light.
- **Redacting `ILogger` wrapper.** An `IRedactor`-backed wrapper that scrubs message templates and
  arguments automatically.

### Diagnostics

- **Richer telemetry tags.** Optionally tag the redaction counter with more dimensions (for example
  rule category) while keeping values out of telemetry entirely.

---

## What is intentionally out of scope

- **Full data-loss-prevention.** OrionShade is a redaction helper for the logging path, not a DLP
  engine. Exhaustive content classification is a different tool.
- **Reversible/tokenized masking.** Masks are one-way by design. Format-preserving encryption or
  reversible tokenization is a separate concern and not planned for the core.
- **Storing or shipping redacted values anywhere.** The library transforms strings in place and
  counts what it did; it does not persist, forward, or sample the data it sees.

---

## Contributing to the roadmap

Open an issue describing the leak you are trying to stop and how you would want OrionShade to help.
Concrete scenarios are far more useful than feature names, and they are what pull an idea up this
list. Pull requests are welcome too; please read [CONTRIBUTING.md](../CONTRIBUTING.md) first.
