# TracingAssertions.TUnit

[![CI](https://github.com/JohnVerheij/TracingAssertions.TUnit/actions/workflows/ci.yml/badge.svg)](https://github.com/JohnVerheij/TracingAssertions.TUnit/actions/workflows/ci.yml)
[![CodeQL](https://github.com/JohnVerheij/TracingAssertions.TUnit/actions/workflows/codeql.yml/badge.svg)](https://github.com/JohnVerheij/TracingAssertions.TUnit/actions/workflows/codeql.yml)
[![OpenSSF Scorecard](https://api.scorecard.dev/projects/github.com/JohnVerheij/TracingAssertions.TUnit/badge)](https://scorecard.dev/viewer/?uri=github.com/JohnVerheij/TracingAssertions.TUnit)
[![OpenSSF Best Practices](https://www.bestpractices.dev/projects/16145/badge)](https://www.bestpractices.dev/projects/16145)
[![codecov](https://codecov.io/gh/JohnVerheij/TracingAssertions.TUnit/branch/main/graph/badge.svg)](https://codecov.io/gh/JohnVerheij/TracingAssertions.TUnit)
[![NuGet](https://img.shields.io/nuget/v/TracingAssertions.TUnit.svg)](https://www.nuget.org/packages/TracingAssertions.TUnit/)
[![Downloads](https://img.shields.io/nuget/dt/TracingAssertions.TUnit.svg)](https://www.nuget.org/packages/TracingAssertions.TUnit/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)

TUnit-native OpenTelemetry distributed-tracing assertions for .NET tests. Fluent entry points over TUnit's `Assert.That(...)` pipeline for asserting on `System.Diagnostics.Activity` spans, with a framework-agnostic core (`TracingAssertions`) that a future xUnit, NUnit, or MSTest adapter can reuse. AOT-compatible, trimmable, no runtime reflection in the assertion path. No OpenTelemetry SDK and no NuGet runtime dependency: capture is a raw `ActivityListener` over an `ActivitySource`.

> **Scope:** Test projects only. Not intended for production code.

> Part of the **[DotNetAssertions](https://dotnetassertions.dev)** family of assertion extensions for TUnit.

---

## Table of contents

- [Why this package](#why-this-package)
- [Install](#install)
- [Package layout](#package-layout)
- [Namespaces](#namespaces)
- [Quick start](#quick-start)
- [Worked example: one operation, two spans](#worked-example-one-operation-two-spans)
- [Parallel tests](#parallel-tests)
- [Root spans and the test runner](#root-spans-and-the-test-runner)
- [Entry points](#entry-points)
- [Failure diagnostics](#failure-diagnostics)
- [Design notes](#design-notes)
- [Stability intent (pre-1.0)](#stability-intent-pre-10)
- [Roadmap](#roadmap)
- [Family compatibility](#family-compatibility)
- [Pair with](#pair-with)
- [Contributing](#contributing)
- [License](#license)

## Why this package

OpenTelemetry tracing turns up in test code whenever production emits spans: a test wants to assert
"the pipeline started a span named `order.pipeline`", "it carried `process.id = 0`", "the child span
shares the parent's trace". The BCL gives you the raw machinery (`ActivitySource`, `Activity`,
`ActivityListener`), but asserting on it is manual: wire up a listener, collect stopped activities
into a bag, then hand-roll the find-and-compare against `Assert`. Every project that does this
re-invents the same capture-and-query helper.

`TracingAssertions.TUnit` absorbs that boilerplate into a reusable capture type and a fluent assertion
surface:

- A disposable, per-test `SpanCapture` that starts a raw `ActivityListener`
  (`AllDataAndRecorded`) over an `ActivitySource` and collects the stopped spans.
- TUnit `Assert.That(span).HasOperationName(...)` assertions, source-generated via
  `[GenerateAssertion]`.

The framework-agnostic `TracingAssertions` core ships separately so non-TUnit consumers can reuse the capture.

## Install

```bash
# TUnit consumers install the adapter; the core is pulled transitively:
dotnet add package TracingAssertions.TUnit

# Framework-agnostic consumers (rare in test projects) can pull the core directly:
dotnet add package TracingAssertions
```

**Requirements:** TUnit 1.77.0 or later, .NET 10. AOT-compatible, trimmable.

## Package layout

| Package | Purpose | Depends on |
|---|---|---|
| [`TracingAssertions`](https://www.nuget.org/packages/TracingAssertions/) | Framework-agnostic core: the `SpanCapture` listener-backed capture type | BCL only |
| [`TracingAssertions.TUnit`](https://www.nuget.org/packages/TracingAssertions.TUnit/) | TUnit adapter: fluent `Assert.That(span)` and `Assert.That(capture)` assertions | `TracingAssertions` + `TUnit.Assertions` + `TUnit.Core` |

Install `TracingAssertions.TUnit` for TUnit test projects; `TracingAssertions` comes transitively.
Adapters for other test frameworks (NUnit, xUnit, MSTest) are not shipped; they would reuse the
`TracingAssertions` core. Open a feature request if you need one.

## Namespaces

| Type / member | Namespace | Auto-imported? |
|---|---|---|
| Fluent entry points (`HasOperationName`, `IsChildOf`, `HasSpan`, ...) | `TUnit.Assertions.Extensions` | Yes (TUnit auto-imports) |
| Core type (`SpanCapture`) | `TracingAssertions` | No - needs `using TracingAssertions;` |

A `GlobalUsings.cs` in your test project:

```csharp
global using TracingAssertions;
```

makes the core namespace available everywhere. The fluent entry points are auto-imported via
`TUnit.Assertions.Extensions`, so they need no `using` of their own.

## Quick start

```csharp
using System.Diagnostics;
using TracingAssertions;

// The code under test emits spans on an ActivitySource:
using var source = new ActivitySource("MyCompany.MyService");

// Capture spans from that source for the duration of a test.
using var capture = SpanCapture.ForSource("MyCompany.MyService");

using (source.StartActivity("order.pipeline"))
{
    // ... run the code under test; the span stops at the end of this scope ...
}

await Assert.That(capture).HasSpan("order.pipeline");
await Assert.That(capture.Captured[0]).HasOperationName("order.pipeline");
```

`SpanCapture.ForSource` starts a raw `ActivityListener` that samples `AllDataAndRecorded` and collects
every stopped `Activity` from the named source. Create one per test with `using`;
disposing it detaches the listener.

## Worked example: one operation, two spans

The code under test handles one request: a `Server` span for the operation, with a `Client` child
span for the database write.

```csharp
public static class OrderService
{
    private static readonly ActivitySource Source = new("Shop.Orders");

    public static void Place(int orderId)
    {
        using var place = Source.StartActivity("order.place", ActivityKind.Server);
        place?.SetTag("order.id", orderId);

        using (Source.StartActivity("order.persist", ActivityKind.Client))
        {
            // ... write the order ...
        }

        place?.SetStatus(ActivityStatusCode.Ok);
    }
}
```

The test starts its own root activity, runs the code under it, and walks the tree from there: the
operation is the root's only child, and the database write is the operation's only child.

```csharp
[Test]
public async Task Place_TracesTheOperationWithAChildSpanForTheWrite()
{
    using var capture = SpanCapture.ForSource("Shop.Orders");
    using var root = new Activity("test");
    root.Start();

    OrderService.Place(orderId: 42);

    var place = await Assert.That(capture.ChildrenOf(root)).HasSingleItem();
    await Assert.That(place).HasOperationName("order.place")
        .And.HasKind(ActivityKind.Server)
        .And.HasTagValue("order.id", 42)
        .And.HasStatus(ActivityStatusCode.Ok);

    var persist = await Assert.That(capture.ChildrenOf(place)).HasSingleItem();
    await Assert.That(persist).HasOperationName("order.persist")
        .And.HasKind(ActivityKind.Client);
}
```

Three rules for every tracing test:

- **Create the capture before the code runs.** `ActivitySource.StartActivity` returns `null` when
  nothing listens to the source, so a span the code starts before the capture exists is never created.
- **Assert after the spans stopped.** The capture collects a span when it stops; a span that is still
  open is not in `Captured` yet.
- **Start the test's own root.** `new Activity(...)` needs no listener, and while it is
  `Activity.Current`, the spans the code starts become its children. `ChildrenOf(root)` then returns
  this test's spans only, whatever runs in parallel and whatever activity the test runner has open
  (the next two sections).

## Parallel tests

The `ActivityListener` behind a capture is process-wide: while the capture is active, it also collects
the spans that other tests emit on the same source. Assertions scoped to the test's root
(`ChildrenOf(root)`) are unaffected, and so are lookups by a tag value that only this test uses
(`FindByOperationNameAndTag("order.place", "order.id", orderId)`). Assertions over the whole capture (`HasSpan`, `HasNoSpan`,
`HasSpanCount`, `Captured`) and `FindByOperationName` can see another test's spans: serialize the tests
that emit on the source with a keyed constraint such as `[NotInParallel("Shop.Orders")]`, on every test
that runs the code, not only on the tests that capture. Tests with other keys still run in parallel.

## Root spans and the test runner

Inside a TUnit test, `Activity.Current` is usually not `null`: TUnit's HTML report, on by default,
listens to every source and runs each test inside a test activity, and so does any other listener that
records TUnit's own activity sources, such as an OpenTelemetry tracer provider in the test host. The spans the code starts become children of that activity, so
`IsRoot()` fails for them although the code started no parent. Two consequences:

- **Assert the parent you control:** `IsChildOf(root)`, or `ChildrenOf(root)`, with the test's own
  root as in the worked example. For code that must start a new trace, set `Activity.Current = null`
  before the call; in an `async` test method the change does not leak out of the test.
- **Sampling:** a lower result passed to `ForSource(name, ActivitySamplingResult)` takes effect only
  when no other listener records the span, because an activity's sampling is the maximum across all
  listeners. The HTML report records every span in a test's trace, so the lower result applies only
  to spans outside that trace (`Activity.Current` cleared) or with the report off
  (`TUNIT_DISABLE_HTML_REPORTER=true`).

## Entry points

Span assertions on `Assert.That(span)` where `span` is a `System.Diagnostics.Activity`:

| Assertion | Description |
|---|---|
| `HasOperationName(name)` | The span's `OperationName` equals `name` (ordinal). |
| `HasTag(key)` | The span carries a tag `key` with a non-null value. |
| `HasTagValue(key, value)` | The span's tag `key` matches `value` (compared by invariant `ToString`). |
| `HasStatus(status)` | The span's `Status` equals the given `ActivityStatusCode`. |
| `HasKind(kind)` *(v0.2.0+)* | The span's `Kind` equals the given `ActivityKind` (for example `Server` for an inbound request, `Client` for an outbound call). |
| `IsChildOf(parent)` | The span is a direct child of `parent` (same trace, `ParentSpanId` equals the parent's `SpanId`). |
| `IsRoot()` *(v0.2.0+)* | The span has no parent (its `ParentSpanId` is the default span id). Inside a TUnit test, the code's spans usually have the test runner's activity as parent: see [Root spans and the test runner](#root-spans-and-the-test-runner). |
| `HasEvent(name)` *(v0.2.0+)* | The span carries an `ActivityEvent` named `name` (lists the span's event names on failure). |
| `HasExceptionEvent()` *(v0.2.0+)* | The span carries the OpenTelemetry exception event (an `ActivityEvent` named `exception`). |
| `SharesTraceWith(other)` | The span shares `other`'s `TraceId`. |

Capture-level assertions on `Assert.That(capture)` where `capture` is a `SpanCapture`:

| Assertion | Description |
|---|---|
| `HasSpan(operationName)` | The capture contains a span with that operation name (lists the captured names on failure). |
| `HasNoSpan(operationName)` *(v0.2.0+)* | The capture contains no span with that operation name. |
| `HasSpanCount(int)` *(v0.2.0+)* | The capture collected exactly that many spans. |

The `SpanCapture` core also exposes query helpers for locating spans without an assertion:
`FindByOperationName`, `FindByOperationNameAndTag`, and `ChildrenOf`. `ForSource` / `ForSources` accept an optional `ActivitySamplingResult` (v0.2.0+) to capture under a chosen sampling level; see [Root spans and the test runner](#root-spans-and-the-test-runner) for when a lower level takes effect.

## Failure diagnostics

`HasOperationName` names both the expected and observed operation name on failure:

```
Expected the span to have operation name "order.created"
  but it was "order.updated"
```

Every span assertion also accepts `.Because(reason)` to attach a domain explanation to the failure,
the same as any other TUnit assertion (it is inherited from the base assertion type):

```csharp
await Assert.That(span).HasTagValue("process.id", workerId).Because("the worker stamps its pid");
```

## Design notes

### Why a raw `ActivityListener`, not the OpenTelemetry SDK

Capture is a BCL `ActivityListener` over an `ActivitySource`, not an OpenTelemetry `TracerProvider` with an in-memory exporter. That keeps the package at **zero NuGet runtime dependencies** (`System.Diagnostics.DiagnosticSource` is in the shared framework) and AOT-safe, and it matches how production code emits spans (a plain `ActivitySource`) without standing up an SDK pipeline in the test.

### Why per-test capture, disposed via `using`

`SpanCapture` is a `using`-scoped handle that attaches its listener on creation and detaches on `Dispose`, with a fresh collection per instance. A test sees only the spans that stopped while its capture was active, with no process-wide singleton carrying spans over from earlier tests, and the listener lives exactly as long as the test that needs it. Tests that run at the same time on the same source still see each other's spans; see [Parallel tests](#parallel-tests).

## Stability intent (pre-1.0)

This is a 0.x release and the public API may evolve.

- **Additive changes** (new entry points, new chain methods) ship in any patch without breaking
  ApiCompat.
- **Breaking changes** to existing signatures bump the minor version (0.X.0) and are called out in the
  [CHANGELOG](CHANGELOG.md).
- From 0.1.0, `PackageValidationBaselineVersion` pins to the previous shipped version so ApiCompat
  catches binary breaks at pack time; `CompatibilitySuppressions.xml` records accepted differences.

The 1.0 milestone signals API stability.

## Roadmap

Shipped in **0.1.0**: multi-source capture, the span-query helpers (`FindByOperationName`,
`FindByOperationNameAndTag`, `ChildrenOf`), and the tag / status / parent-child / same-trace
assertions plus capture-level `HasSpan`. Shipped in **0.2.0**: `HasKind`, `IsRoot`, `HasEvent`,
`HasExceptionEvent`, `HasNoSpan`, `HasSpanCount`, and capture under a chosen sampling result.

Deferred (no current demand): span duration assertions, span links and baggage, multi-level
child-chain matchers, type-aware (non-`ToString`) tag matching, and rendering the captured spans on
failure.

Demand-driven; no fixed timeline.

## Family compatibility

The nine assertion-family packages: `LogAssertions.TUnit`, `TimeAssertions.TUnit`,
`SnapshotAssertions.TUnit`, `MathAssertions.TUnit`, `JsonAssertions.TUnit`, `SseAssertions.TUnit`,
`GrpcAssertions.TUnit`, `TracingAssertions.TUnit`, and `MetricsAssertions.TUnit`: release independently and target the same .NET
TFM at any moment (LTS-anchored, multi-target during STS support windows; see the
[TFM policy in CONVENTIONS.md](CONVENTIONS.md#tfm-policy) for the rotation schedule). **Mix versions
freely.** Each package ships under SemVer with `EnablePackageValidation` strict-mode ApiCompat against
its previous baseline, so binary breaks within a version line are caught at pack time.

## Pair with

- **[`LogAssertions.TUnit`](https://www.nuget.org/packages/LogAssertions.TUnit/)**: fluent log assertions over `Microsoft.Extensions.Logging.Testing.FakeLogCollector`.
- **[`TimeAssertions.TUnit`](https://www.nuget.org/packages/TimeAssertions.TUnit/)**: `TimeProvider`-aware time assertions and cross-cutting `.WithinTimeBudget(...)` chain methods. A natural pairing once span-duration assertions arrive.
- **[`SnapshotAssertions.TUnit`](https://www.nuget.org/packages/SnapshotAssertions.TUnit/)**: text-snapshot assertions for API-surface tests and similar deterministic-string scenarios.
- **[`MathAssertions.TUnit`](https://www.nuget.org/packages/MathAssertions.TUnit/)**: tolerance-aware fluent assertions over numeric and geometric types.
- **[`JsonAssertions.TUnit`](https://www.nuget.org/packages/JsonAssertions.TUnit/)**: fluent JSON assertions over `System.Text.Json` and HTTP response bodies.
- **[`SseAssertions.TUnit`](https://www.nuget.org/packages/SseAssertions.TUnit/)**: fluent Server-Sent Events wire-format assertions.
- **[`GrpcAssertions.TUnit`](https://www.nuget.org/packages/GrpcAssertions.TUnit/)**: fluent gRPC outcome assertions plus the `GrpcCallBuilder` test-double helper.
- **[`MetricsAssertions.TUnit`](https://www.nuget.org/packages/MetricsAssertions.TUnit/)**: fluent assertions over `System.Diagnostics.Metrics` instruments (counters, histograms, gauges), built on `MetricCollector`.

## Contributing

Issues and pull requests welcome. Before opening a PR:

- Run `dotnet build` and `dotnet test` locally; the CI pipeline enforces the same quality bar (zero warnings as errors, 90% line / 90% branch coverage minimum).
- Match the existing code style (`.editorconfig` is authoritative; `dotnet format` covers formatting).
- For new assertions, include a test for both the happy path and a representative failure case.

For larger ideas, open a [Discussion](https://github.com/JohnVerheij/TracingAssertions.TUnit/discussions) first to align on direction before investing implementation time.

See [CONTRIBUTING.md](CONTRIBUTING.md) for the full PR review checklist, and [CONVENTIONS.md](CONVENTIONS.md) for the family-wide code conventions shared across the assertion family.

## License

[MIT](LICENSE). Copyright (c) 2026 John Verheij.
