# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

> *History note:* All version sections were reformatted on 2026-07-05 for one-time
> [CONVENTIONS &sect;CHANGELOG](CONVENTIONS.md) conformance: forbidden sub-headers folded into
> the six Keep a Changelog headers, non-user-facing content removed (internal refactors, test
> counts, coverage numbers, CI and build hygiene, governance churn, roadmap notes), bullets
> kept in past-tense active voice with code-formatted API leads. The nuget.org Release Notes
> tab and the GitHub Release for each shipped version are unchanged. A CI `changelog-lint` gate
> keeps future sections conforming; each is frozen per Rule 7 once shipped.

## [Unreleased]

## [0.2.0] - 2026-06-14: kind, root, event, and count assertions; sampling control

Minor release. Adds span and capture assertions for ASP.NET Core span testing, plus a sampling-control capture overload. Purely additive.

### Added

- **Span assertions:** `Assert.That(span).HasKind(ActivityKind)` (for example `Server` for an inbound request span), `IsRoot()` (no parent), `HasEvent(name)`, and `HasExceptionEvent()` (the OpenTelemetry `"exception"` event). The event assertions list the span's event names on failure.
- **Capture assertions:** `Assert.That(capture).HasNoSpan(operationName)` (the inverse of `HasSpan`, for asserting an operation was never traced) and `HasSpanCount(int)`.
- **`SpanCapture.ForSource(name, ActivitySamplingResult)`** and **`ForSources(ActivitySamplingResult, params string[])`** capture under a chosen sampling result. The existing parameterless forms still force `AllDataAndRecorded`. A lower result (for example `PropagationData`) lets a test exercise code that branches on `Activity.IsAllDataRequested`, with the documented caveat that an activity's effective sampling is the maximum across all active listeners, so this takes effect only when the capture is the sole listener.

## [0.1.2] - 2026-06-06: package description correction

Metadata-only release. No API or behavior change.

### Changed

- README: a short example showing `.Because(reason)` chained on a span assertion. `.Because` is inherited from TUnit's base assertion type and has always worked on the generated span assertions; the note makes that explicit.

### Fixed

- Corrected the NuGet package `<Description>`. It described a foundation release carrying only `HasOperationName` and said the full fluent surface "lands in 0.1.0"; the package has shipped the complete surface (operation name, tag existence and value, status, parent/child, same-trace) since 0.1.0. The description now matches the README.

## [0.1.1] - 2026-06-05: documentation refresh

Documentation-only release. No API or behavior change.

### Changed

- Refreshed the README (plain-ASCII punctuation).

## [0.1.0] - 2026-06-04: span-query surface and the full assertion set

Minor release. Completes the foundation surface with multi-source capture, the span-query helpers, and
the tag / status / parent-child / same-trace assertions plus a capture-level `HasSpan`. Purely
additive; the `0.0.1` ApiCompat baseline is preserved.

### Added

- **Core `TracingAssertions`:**
  - `SpanCapture.ForSources(params string[])` captures from any of several named `ActivitySource`s.
  - `SpanCapture.FindByOperationName(name)` returns the first captured span with that operation name.
  - `SpanCapture.FindByOperationNameAndTag(name, tagKey, tagValue)` returns the first captured span
    with that operation name carrying a matching tag (tag value compared by invariant `ToString`).
  - `SpanCapture.ChildrenOf(parent)` returns the captured direct children of a span (same trace,
    `ParentSpanId` equals the parent's `SpanId`).
- **Adapter `TracingAssertions.TUnit`** (generated via `[GenerateAssertion]`):
  - `Assert.That(span).HasTag(key)` asserts a tag is present.
  - `Assert.That(span).HasTagValue(key, value)` asserts a tag's value (compared by invariant
    `ToString`). Named distinctly from `HasTag(key)` so a two-argument call is never silently bound to
    the tag-existence overload.
  - `Assert.That(span).HasStatus(ActivityStatusCode)` asserts the span status.
  - `Assert.That(span).IsChildOf(parent)` asserts a single-hop parent/child relationship in the same
    trace.
  - `Assert.That(span).SharesTraceWith(other)` asserts two spans share a `TraceId`.
  - `Assert.That(capture).HasSpan(operationName)` asserts the capture contains a span with that
    operation name, listing the captured names on failure.

## [0.0.1] - 2026-06-04: foundation release

First published release. It establishes the repository, the CI and release pipeline, and the two
NuGet package identities (`TracingAssertions` core, `TracingAssertions.TUnit` adapter) with a minimal
but real surface. The fuller span-query surface and the broader fluent assertions land in 0.1.0.

### Added

- **Core `TracingAssertions`:** `SpanCapture.ForSource(name)` starts a raw `ActivityListener`
  (sampling `AllDataAndRecorded`) over a single `ActivitySource` and collects stopped
  `System.Diagnostics.Activity` spans into `Captured`; disposing the capture detaches the listener.
  No OpenTelemetry SDK, no exporter pipeline, and no NuGet runtime dependency
  (`System.Diagnostics.DiagnosticSource` is in the shared framework).
- **Adapter `TracingAssertions.TUnit`:** the `HasOperationName` span assertion, generated via TUnit's
  `[GenerateAssertion]` source generator, usable as `await Assert.That(span).HasOperationName("...")`.
- Both packages are AOT-compatible and trimmable, with no runtime reflection in the assertion path.

[unreleased]: https://github.com/JohnVerheij/TracingAssertions.TUnit/compare/v0.2.0...HEAD
[0.2.0]: https://github.com/JohnVerheij/TracingAssertions.TUnit/compare/v0.1.2...v0.2.0
[0.1.2]: https://github.com/JohnVerheij/TracingAssertions.TUnit/compare/v0.1.1...v0.1.2
[0.1.1]: https://github.com/JohnVerheij/TracingAssertions.TUnit/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/JohnVerheij/TracingAssertions.TUnit/compare/v0.0.1...v0.1.0
[0.0.1]: https://github.com/JohnVerheij/TracingAssertions.TUnit/releases/tag/v0.0.1
