# TracingAssertions.TUnit

[![NuGet](https://img.shields.io/nuget/v/TracingAssertions.TUnit.svg)](https://www.nuget.org/packages/TracingAssertions.TUnit/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)

> **Scope:** Test projects only. Not intended for production code.

> Part of the **[DotNetAssertions](https://dotnetassertions.dev)** family of assertion extensions for TUnit.

TUnit-native OpenTelemetry distributed-tracing (`Activity` / span) assertions for .NET tests. Fluent entry points over TUnit's `Assert.That(...)` pipeline for asserting on captured spans. AOT-compatible, trimmable, no runtime reflection in the assertion path.

## What ships

Span assertions on `Assert.That(span)` where `span` is a `System.Diagnostics.Activity`:

| Entry point | Behavior |
|---|---|
| `HasOperationName(name)` | Asserts the span's `OperationName` equals `name` (ordinal). |
| `HasTag(key)` | Asserts a tag `key` is present (non-null value). |
| `HasTagValue(key, value)` | Asserts the tag `key` matches `value` (compared by invariant `ToString`). |
| `HasStatus(status)` | Asserts the span's `Status` equals the given `ActivityStatusCode`. |
| `HasKind(kind)` *(v0.2.0+)* | Asserts the span's `Kind` equals the given `ActivityKind` (for example `Server` / `Client`). |
| `IsChildOf(parent)` | Asserts a single-hop parent/child relationship in the same trace. |
| `IsRoot()` *(v0.2.0+)* | Asserts the span has no parent. |
| `HasEvent(name)` / `HasExceptionEvent()` *(v0.2.0+)* | Asserts the span carries an `ActivityEvent` with that name / the OpenTelemetry `exception` event. |
| `SharesTraceWith(other)` | Asserts two spans share a `TraceId`. |
| `HasSpan(operationName)` / `HasNoSpan(operationName)` *(HasNoSpan v0.2.0+)* (on `SpanCapture`) | Asserts the capture does / does not contain a span with that operation name. |
| `HasSpanCount(int)` *(v0.2.0+)* (on `SpanCapture`) | Asserts the capture collected exactly that many spans. |

The framework-agnostic core (`TracingAssertions`) ships `SpanCapture` for collecting spans from one or more `ActivitySource`s via a raw `ActivityListener` (no OpenTelemetry SDK or NuGet runtime dependency), plus the query helpers `FindByOperationName`, `FindByOperationNameAndTag`, and `ChildrenOf`. `ForSource` / `ForSources` take an optional `ActivitySamplingResult` (v0.2.0+) to capture under a chosen sampling level.

## Install

```bash
dotnet add package TracingAssertions.TUnit
```

**Requirements:** TUnit 1.77.0 or later, .NET 10. The framework-agnostic `TracingAssertions` core comes transitively.

## Quick start

```csharp
using System.Diagnostics;
using TracingAssertions;

using var source = new ActivitySource("MyCompany.MyService");
using var capture = SpanCapture.ForSource("MyCompany.MyService");

using (source.StartActivity("order.created"))
{
    // ... the code under test runs here ...
}

await Assert.That(capture).HasSpan("order.created");
```

Create the capture before the code runs (`StartActivity` returns `null` when nothing listens), assert
after the spans stopped, and scope the assertions to a root activity the test starts
(`capture.ChildrenOf(root)`): the listener is process-wide, so tests running in parallel see each
other's spans, and inside a TUnit test the code's spans have the test runner's activity as parent. The
[worked example](https://github.com/JohnVerheij/TracingAssertions.TUnit#worked-example-one-operation-two-spans)
shows the pattern; the [GitHub README](https://github.com/JohnVerheij/TracingAssertions.TUnit#readme)
has the full reference.

## Family

Part of an assertion family for TUnit:

- [LogAssertions.TUnit](https://github.com/JohnVerheij/LogAssertions.TUnit)
- [SnapshotAssertions.TUnit](https://github.com/JohnVerheij/SnapshotAssertions.TUnit)
- [TimeAssertions.TUnit](https://github.com/JohnVerheij/TimeAssertions.TUnit)
- [MathAssertions.TUnit](https://github.com/JohnVerheij/MathAssertions.TUnit)
- [JsonAssertions.TUnit](https://github.com/JohnVerheij/JsonAssertions.TUnit)
- [SseAssertions.TUnit](https://github.com/JohnVerheij/SseAssertions.TUnit)
- [GrpcAssertions.TUnit](https://github.com/JohnVerheij/GrpcAssertions.TUnit)
- [MetricsAssertions.TUnit](https://github.com/JohnVerheij/MetricsAssertions.TUnit)

## License

MIT. No runtime dependencies beyond the BCL.
