# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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

### Notes

- Still no OpenTelemetry SDK dependency: capture remains a raw `ActivityListener`. AOT-compatible,
  trimmable, no runtime reflection in the assertion path.
- Deferred (no current demand): span events / links / baggage, duration and kind assertions,
  multi-level child-chain matchers, and tag type-aware (non-`ToString`) matching.

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

### Notes

- Both packages are AOT-compatible and trimmable, with no runtime reflection in the assertion path.
- Planned for 0.1.0: multi-source capture, find-by-operation-name and find-by-name-and-tag queries,
  parent/child navigation, and the tag / status / is-child-of / same-trace assertions, plus a
  capture-level `HasSpan` entry point.
