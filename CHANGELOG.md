# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
