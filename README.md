# TracingAssertions

[![CI](https://github.com/JohnVerheij/TracingAssertions.TUnit/actions/workflows/ci.yml/badge.svg)](https://github.com/JohnVerheij/TracingAssertions.TUnit/actions/workflows/ci.yml)
[![NuGet TracingAssertions](https://img.shields.io/nuget/v/TracingAssertions?label=TracingAssertions)](https://www.nuget.org/packages/TracingAssertions)
[![NuGet TracingAssertions.TUnit](https://img.shields.io/nuget/v/TracingAssertions.TUnit?label=TracingAssertions.TUnit)](https://www.nuget.org/packages/TracingAssertions.TUnit)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

Fluent assertions for OpenTelemetry distributed tracing (`System.Diagnostics.Activity` spans) in .NET
test projects. Capture the spans your code emits with a raw `ActivityListener`, then assert on them
through TUnit's `Assert.That(...)` pipeline.

No OpenTelemetry SDK, no exporter pipeline, **no NuGet runtime dependency**
(`System.Diagnostics.DiagnosticSource` is in the shared framework). AOT-compatible, trimmable, and no
runtime reflection in the assertion path.

> **Foundation release (v0.0.1).** This release establishes the packages and ships a minimal surface:
> single-source `SpanCapture` and the `HasOperationName` assertion. The fuller span-query surface and
> the tag / status / parent-child / same-trace assertions land in 0.1.0 (see [Roadmap](#roadmap)).

## Packages

| Package | What it is |
|---|---|
| [`TracingAssertions`](https://www.nuget.org/packages/TracingAssertions) | Framework-agnostic core: the `SpanCapture` listener-backed capture type. No test-framework dependency. |
| [`TracingAssertions.TUnit`](https://www.nuget.org/packages/TracingAssertions.TUnit) | TUnit adapter: fluent `Assert.That(span)` assertions, source-generated via `[GenerateAssertion]`. References the core. |

## Install

```bash
dotnet add package TracingAssertions.TUnit
```

The adapter brings the `TracingAssertions` core in transitively.

## Quick start

```csharp
using System.Diagnostics;
using TracingAssertions;

// Capture spans from one ActivitySource for the duration of a test.
using var capture = SpanCapture.ForSource("MyCompany.MyService");

using (var span = MyActivitySource.StartActivity("pick.pipeline"))
{
    // ... run the code under test; the span stops at the end of this scope ...
}

// Assert on a captured span (TUnit adapter).
await Assert.That(capture.Captured[0]).HasOperationName("pick.pipeline");
```

`SpanCapture.ForSource` starts a raw `ActivityListener` that samples `AllDataAndRecorded` and collects
every stopped `Activity` from the named source. Create one per test with `using` for isolation;
disposing it detaches the listener.

## Roadmap

Planned for **0.1.0**:

- Multi-source capture (`SpanCapture.ForSources(...)`).
- Span queries on the capture: find-by-operation-name, find-by-name-and-tag, and parent/child navigation.
- Assertions: tag-exists, tag-value, status, is-child-of, same-trace, and a capture-level `HasSpan`.

Deferred (no current demand): span events / links / baggage, duration and kind assertions, multi-level
child-chain matchers.

## Design principles

- **Zero runtime dependencies.** Capture is a BCL `ActivityListener` over an `ActivitySource`; nothing
  from the OpenTelemetry SDK is required at runtime.
- **AOT-safe and trimmable.** No reflection in the assertion path.
- **Core + adapter.** The capture lives in the framework-agnostic core; the fluent assertions live in
  the TUnit adapter, so other test-framework adapters are possible if there is demand.

## License

[MIT](LICENSE).
