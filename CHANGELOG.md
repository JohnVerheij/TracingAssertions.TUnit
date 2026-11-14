# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

> *History note:* All version sections were reformatted on 2026-07-05 for one-time
> [CONVENTIONS &sect;CHANGELOG](CONVENTIONS.md) conformance: forbidden sub-headers folded into
> the six Keep a Changelog headers, non-user-facing content removed (internal refactors, test
> counts, coverage numbers, CI and build hygiene, governance churn, roadmap notes), bullets
> kept in past-tense active voice with code-formatted API leads. The nuget.org Release Notes
> tab and the GitHub Release for each shipped version are unchanged. A CI `family-lint` gate
> keeps future sections conforming; each is frozen per Rule 7 once shipped.

## [Unreleased]

### Added

- **`Assert.That(capture).HasSpanWithTag(operationName, tagKey, tagValue)`** asserts that the capture holds a span with that operation name and tag value, and awaiting it returns the span, so a test goes on with a span that is not `null` instead of looking it up and checking for `null`. The tag value is compared by its invariant `ToString` form, as in `HasTagValue`. When no span matches, the failure shows every captured span with the tag.
- **`Assert.That(parent).HasChildSpan(capture, operationName)`** (awaiting it returns the first matching child) and **`HasChildSpanCount(capture, count)`** assert on the direct children of a span.
- **`Assert.That(span).HasDurationLessThan(max)`** and **`HasDurationAtLeast(min)`** assert on `Activity.Duration`. A span reads the real clock, so bounds need room for a slow machine. A span that has not stopped fails both.
- **`SpanTreeRenderer.Render(capture, options)`** prints the captured spans as an indented tree: operation name, kind, status and the tags chosen with `SpanTreeOptions.TagKeys`, without ids or durations. Siblings are ordered by start time, then operation name, and spans equal in both keep their capture order, so the same capture gives the same text and it suits a snapshot test.
- **.NET 11:** both packages target `net10.0` and `net11.0`.

### Changed

- **Failure texts of every span and capture assertion** now follow one rule: TUnit prints the expectation once (`Expected to have status Error`), and the text after `but` describes what was found (`but it was Ok`), with names and values in single quotes; a value the expectation cannot show appears once, in parentheses. Before, each text started with the expectation again, so TUnit printed it twice. A test that matches on the old wording needs updating.
- **`HasSpan`, `HasNoSpan` and `HasSpanCount`** show the captured spans as a tree when they fail, where `HasSpan` listed operation names and the other two did not list spans.
- **`IsChildOf`** names the span's actual parent (by operation name when known) and the expected one when it fails, not only span ids.
- **Minimum TUnit version** is now 1.83.8.

### Fixed

- **`IsChildOf`, `IsRoot`, `SharesTraceWith` and `SpanCapture.ChildrenOf` with `ActivityIdFormat.Hierarchical` ids.** They compared W3C ids only, which are all default with hierarchical ids, so unrelated spans passed and every span was a root. They now compare `ParentId`, `Id` and `RootId` for hierarchical spans. A hierarchical span is never related to a W3C span.
- **`IsChildOf` and `SharesTraceWith` with spans that have not started.** An unstarted span has no ids yet, so every id is default, and two such spans passed both assertions because default equals default. An unstarted span is now nobody's child and shares a trace with no span. `IsRoot` still passes for it, as it has no parent yet.
- **`IsRoot` with a W3C span whose parent id is not a W3C id** (for example a hierarchical id given to `Activity.SetParentId(string)`). Its parent span id stays default, so it passed as a root although it has a parent. It now fails, and the failure shows the parent id.

## [0.2.1] - 2026-11-05: worked example, parallel-test guidance

Patch release. No API or behavior change.

### Changed

- **README:** a worked example for one operation traced as a parent span with a child span, scoped to a root activity the test starts (`ChildrenOf(root)`), with the three rules every tracing test follows; a section on parallel tests (the listener is process-wide, so whole-capture assertions see other tests' spans); a section on the test runner's own activity (inside a TUnit test, the code's spans have TUnit's test activity as parent while its HTML report (or another listener on TUnit's activity source) is on, so `IsRoot()` fails for them, and a lower sampling result applies only outside the test's trace or with the report off); a roadmap that lists what 0.2.0 shipped; and a quick start that declares its `ActivitySource`.
- **`SpanCapture` XML docs:** the same parallel-test note.
- **README quick starts** (root and package READMEs) now scope their assertion to a root activity the test starts (`ChildrenOf(root)`) instead of the whole capture, so it cannot pass on another parallel test's span.
- **`IsChildOf`**, **`IsRoot`**, **`SharesTraceWith`**, and **`SpanCapture.ChildrenOf`** documentation now says these relationships are compared by W3C activity IDs (the .NET default) and are not meaningful with hierarchical IDs, in the READMEs and the XML docs.
- **README failure diagnostics** now describes in words what a failed span assertion reports, instead of a verbatim failure text that no longer matches TUnit's output.
- **`ActivitySamplingResult.None`** is now described as creating no activity, so nothing is captured unless another listener samples it (README, package READMEs, and `SpanCapture` XML docs).
- **`SpanCapture.ForSource`** and **`SpanCapture.ForSources`** documentation now describes the sampling-result variants as overloads, with the result as the second parameter of `ForSource` and the first of `ForSources`.
- **README sampling guidance** now says that with TUnit's HTML report on, a result below `PropagationData` is raised to `PropagationData`, while higher results apply to spans outside the test's trace (the report records spans in a known test trace anyway).
- **README rule on creating the capture first** now says that without any listener `StartActivity` returns `null` and the span is not created, and that the capture only collects spans that stop while it is attached.
- **Dependency statement** now says the core `TracingAssertions` has no NuGet dependencies and the adapter depends only on TUnit and the core, in the READMEs and the `TracingAssertions.TUnit` package description.
- **`TracingAssertions`** package README now shows `dotnet add package TracingAssertions` as the install command, with a note that TUnit users install the adapter.
- **`SpanCapture`** documentation now says sampling defaults to `AllDataAndRecorded` and that the overloads accept another result, and `ForSource` now says it matches every source with that name.
- **`SpanAssertions`** summary now lists the kind, root, and event assertions.
- **README** now says adapters for other test frameworks (NUnit, xUnit, MSTest) are not planned.
- **`PackageProjectUrl`** of both packages now points to `https://tracingassertions.dev`.
- **NuGet descriptions** of both packages list the current assertion surface; the core's no longer calls itself a foundation release.
- **`TUnit.Assertions`** and **`TUnit.Core`** minimum versions raised from 1.54.0 to 1.80.0.

## [0.2.0] - 2026-06-14: kind, root, event, and count assertions; sampling control

Minor release. Adds span and capture assertions for ASP.NET Core span testing, plus a sampling-control capture overload. Purely additive.

### Added

- **Span assertions:** `Assert.That(span).HasKind(ActivityKind)` (for example `Server` for an inbound request span), `IsRoot()` (no parent), `HasEvent(name)`, and `HasExceptionEvent()` (the OpenTelemetry `"exception"` event). The event assertions list the span's event names on failure.
- **Capture assertions:** `Assert.That(capture).HasNoSpan(operationName)` (the inverse of `HasSpan`, for asserting an operation was never traced) and `HasSpanCount(int)`.
- **`SpanCapture.ForSource(name, ActivitySamplingResult)`** and **`ForSources(ActivitySamplingResult, params string[])`** capture under a chosen sampling result. The existing parameterless forms still force `AllDataAndRecorded`. A lower result (for example `PropagationData`) lets a test exercise code that branches on `Activity.IsAllDataRequested`, with the documented caveat that an activity's effective sampling is the maximum across all active listeners, so this takes effect only when the capture is the sole listener.

## [0.1.2] - 2026-06-06: package description correction

Metadata-only release. No API or behavior change.

### Changed

- `.Because(reason)`: added a README example showing it chained on a span assertion. Inherited from TUnit's base assertion type and has always worked on the generated span assertions; the note makes that explicit.

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

[unreleased]: https://github.com/JohnVerheij/TracingAssertions.TUnit/compare/v0.2.1...HEAD
[0.2.1]: https://github.com/JohnVerheij/TracingAssertions.TUnit/compare/v0.2.0...v0.2.1
[0.2.0]: https://github.com/JohnVerheij/TracingAssertions.TUnit/compare/v0.1.2...v0.2.0
[0.1.2]: https://github.com/JohnVerheij/TracingAssertions.TUnit/compare/v0.1.1...v0.1.2
[0.1.1]: https://github.com/JohnVerheij/TracingAssertions.TUnit/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/JohnVerheij/TracingAssertions.TUnit/compare/v0.0.1...v0.1.0
[0.0.1]: https://github.com/JohnVerheij/TracingAssertions.TUnit/releases/tag/v0.0.1
