# TracingAssertions.TUnit

[![CI](https://github.com/JohnVerheij/TracingAssertions.TUnit/actions/workflows/ci.yml/badge.svg)](https://github.com/JohnVerheij/TracingAssertions.TUnit/actions/workflows/ci.yml)
[![CodeQL](https://github.com/JohnVerheij/TracingAssertions.TUnit/actions/workflows/codeql.yml/badge.svg)](https://github.com/JohnVerheij/TracingAssertions.TUnit/actions/workflows/codeql.yml)
[![OpenSSF Scorecard](https://api.scorecard.dev/projects/github.com/JohnVerheij/TracingAssertions.TUnit/badge)](https://scorecard.dev/viewer/?uri=github.com/JohnVerheij/TracingAssertions.TUnit)
[![OpenSSF Best Practices](https://www.bestpractices.dev/projects/16145/badge)](https://www.bestpractices.dev/projects/16145)
[![codecov](https://codecov.io/gh/JohnVerheij/TracingAssertions.TUnit/branch/main/graph/badge.svg)](https://codecov.io/gh/JohnVerheij/TracingAssertions.TUnit)
[![NuGet](https://img.shields.io/nuget/v/TracingAssertions.TUnit.svg)](https://www.nuget.org/packages/TracingAssertions.TUnit/)
[![Downloads](https://img.shields.io/nuget/dt/TracingAssertions.TUnit.svg)](https://www.nuget.org/packages/TracingAssertions.TUnit/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0%20%7C%2011.0-512BD4.svg)](https://dotnet.microsoft.com/download)

TUnit-native OpenTelemetry distributed-tracing assertions for .NET tests. Fluent entry points over TUnit's `Assert.That(...)` pipeline for asserting on `System.Diagnostics.Activity` spans, with a framework-agnostic core (`TracingAssertions`). AOT-compatible, trimmable, no runtime reflection in the assertion path. No OpenTelemetry SDK: the core has no NuGet dependencies, and the adapter depends only on TUnit and the core. Capture is a raw `ActivityListener` over an `ActivitySource`.

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
- [Finding a span](#finding-a-span)
- [Child spans](#child-spans)
- [Rendering the spans as a tree](#rendering-the-spans-as-a-tree)
- [Durations](#durations)
- [Entry points](#entry-points)
- [Failure diagnostics](#failure-diagnostics)
- [Compared with other ways to test spans](#compared-with-other-ways-to-test-spans)
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
  (`AllDataAndRecorded` by default) over an `ActivitySource` and collects the stopped spans.
- TUnit `Assert.That(span).HasOperationName(...)` assertions, source-generated via
  `[GenerateAssertion]`.
- A span lookup that fails with the captured spans and hands back the span, child-span and duration
  assertions, and a renderer that prints the captured spans as a tree.

The framework-agnostic `TracingAssertions` core ships separately so non-TUnit consumers can reuse the capture.

## Install

```bash
# TUnit consumers install the adapter; the core is pulled transitively:
dotnet add package TracingAssertions.TUnit

# Framework-agnostic consumers (rare in test projects) can pull the core directly:
dotnet add package TracingAssertions
```

**Requirements:** TUnit 1.83.8 or later, .NET 10 or .NET 11. AOT-compatible, trimmable.

## Package layout

| Package | Purpose | Depends on |
|---|---|---|
| [`TracingAssertions`](https://www.nuget.org/packages/TracingAssertions/) | Framework-agnostic core: the `SpanCapture` listener-backed capture type and the `SpanTreeRenderer` | BCL only |
| [`TracingAssertions.TUnit`](https://www.nuget.org/packages/TracingAssertions.TUnit/) | TUnit adapter: fluent `Assert.That(span)` and `Assert.That(capture)` assertions | `TracingAssertions` + `TUnit.Assertions` + `TUnit.Core` |

Install `TracingAssertions.TUnit` for TUnit test projects; `TracingAssertions` comes transitively.
Adapters for other test frameworks (NUnit, xUnit, MSTest) are not planned; the family targets TUnit.

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

// Start the test's own root activity so the assertions see this test's spans only.
using var root = new Activity("test");
root.Start();

using (source.StartActivity("order.pipeline"))
{
    // ... run the code under test; the span stops at the end of this scope ...
}

var span = await Assert.That(capture.ChildrenOf(root)).HasSingleItem();
await Assert.That(span).HasOperationName("order.pipeline");
```

`SpanCapture.ForSource` starts a raw `ActivityListener` that samples `AllDataAndRecorded` by default and
collects every stopped `Activity` from the named source. Create one per test with `using`;
disposing it detaches the listener. Querying `ChildrenOf(root)` instead of the whole capture keeps the
test from passing on another parallel test's span (see [Parallel tests](#parallel-tests)).

## Worked example: one operation, two spans

The code under test handles one request: a `Server` span for the operation, with a `Client` child
span for the database write.

```csharp
using System.Diagnostics;

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
using System.Diagnostics;

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

- **Create the capture before the code runs.** Without any listener, `StartActivity` returns
  `null` and the span is not created; the capture only collects spans that stop while it is attached.
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
  before the call; in an `async` test method the change does not leak out of the test. While it is
  `null`, a span the test starts outside any other span begins a new trace instead of joining the root
  it started earlier or the test's activity, so when more spans follow the call, save the previous value
  and restore it in a `finally` block.
- **Sampling:** a lower result passed to `ForSource(name, ActivitySamplingResult)` takes effect only
  when no other listener records the span, because an activity's sampling is the maximum across all
  listeners. The HTML report listens to every source and records every span in a test's trace. With the
  report on, a result above `PropagationData` (for example `AllData`) applies to spans outside the
  test's trace (`Activity.Current` cleared); `None` and `PropagationData` are raised to
  `PropagationData`, so the span is still created and captured. With the report off and no other
  listener, the chosen result applies everywhere. `ActivitySamplingResult.None` creates no activity, so
  nothing is captured unless another listener samples it.

## Finding a span

`FindByOperationNameAndTag` returns the span or `null`, so a test that uses the result first has to check it
for `null`. `HasSpanWithTag` is the assertion form: it fails with the captured spans when no span matches,
and awaiting it returns the span, so the test goes on with a span that is not `null`.

```csharp
using System.Diagnostics;

using var capture = SpanCapture.ForSource("Shop.Orders");

OrderService.Place(orderId: 42);

var place = await Assert.That(capture).HasSpanWithTag("order.place", "order.id", 42);
await Assert.That(place).HasStatus(ActivityStatusCode.Ok);
```

The tag value is compared by its invariant `ToString` form, as in `HasTagValue`: a `bool` tag matches `true`
and `"True"`, not `"true"`. When nothing matches, the failure shows each captured span with the tag, which
tells a wrong value from a missing span:

```text
Expected to include a span named 'order.place' with tag 'order.id' with the expected value
but none did (the value looked for was '43'); captured spans:
  order.place [Server, Ok] {order.id=42}
    order.persist [Client, Unset]
```

The name is `HasSpanWithTag` and not another `HasSpan` overload on purpose: a string third argument of
`HasSpan` would bind to the caller-expression parameter that TUnit adds.

## Child spans

`HasChildSpan` and `HasChildSpanCount` take the parent span as the receiver and the capture as the first
argument. `HasChildSpan` returns the first matching child when awaited. Both find children with
`ChildrenOf`, so only direct children count.

```csharp
using System.Diagnostics;

var persist = await Assert.That(place).HasChildSpan(capture, "order.persist");
await Assert.That(persist).HasKind(ActivityKind.Client);

await Assert.That(place).HasChildSpanCount(capture, 1);
```

A failure lists the children the parent has and the captured spans as a tree.

## Rendering the spans as a tree

`SpanTreeRenderer.Render` prints the captured spans as an indented tree: the operation name, then the kind
and status, then the tags you select. The text holds no ids and no durations. Siblings, and the spans at the top
level, are ordered by start time, then by operation name (ordinal); spans equal in both keep the order in which
they were captured. The same spans captured in the same order therefore always give the same text, which suits
a snapshot test. The failures of the capture-level assertions and of `HasChildSpan` and `HasChildSpanCount`
print the captured spans as this tree (`HasSpanWithTag` adds the tag it looked for).

```csharp
var options = new SpanTreeOptions { TagKeys = ["order.id"] };
var tree = SpanTreeRenderer.Render(capture, options);

await Assert.That(tree).IsEqualTo(
    "order.place [Server, Ok] {order.id=42}\n" +
    "  order.persist [Client, Unset]");
```

A span whose parent was not captured (a remote parent, a span still running) appears at the top level. A
capture without spans renders as `<no spans>`. Line breaks inside a name or a value are written as `\n` and
`\r`, so one span stays on one line. To keep the tree in a snapshot file, pass it to
`MatchesSnapshot()` from [`SnapshotAssertions.TUnit`](https://www.nuget.org/packages/SnapshotAssertions.TUnit/).

## Durations

```csharp
using System;

await Assert.That(place).HasDurationLessThan(TimeSpan.FromSeconds(2));
```

`HasDurationAtLeast(min)` is the other bound, for a span that must have waited, for example on a retry delay.

A span reads the real clock and has no `TimeProvider`, so a test cannot advance it: choose bounds with room
for a slow machine, and prefer an outcome assertion to a tight bound. A span that has not stopped has no
duration yet and fails both assertions. Negative bounds throw `ArgumentOutOfRangeException`.

## Entry points

Span assertions on `Assert.That(span)` where `span` is a `System.Diagnostics.Activity`:

| Assertion | Description |
|---|---|
| `HasOperationName(name)` | The span's `OperationName` equals `name` (ordinal). |
| `HasTag(key)` | The span carries a tag `key` with a non-null value. |
| `HasTagValue(key, value)` | The span's tag `key` matches `value` (compared by invariant `ToString`). |
| `HasStatus(status)` | The span's `Status` equals the given `ActivityStatusCode`. |
| `HasKind(kind)` *(v0.2.0+)* | The span's `Kind` equals the given `ActivityKind` (for example `Server` for an inbound request, `Client` for an outbound call). |
| `IsChildOf(parent)` | The span is a direct child of `parent` (same trace, `ParentSpanId` equals the parent's `SpanId`). With hierarchical activity IDs the `ParentId` is compared with the parent's `Id`; a hierarchical span is never the child of a W3C span. A span that has not started has no IDs yet and is nobody's child. The failure names the span's actual parent. |
| `IsRoot()` *(v0.2.0+)* | The span has no parent: its `ParentId` is `null` (with W3C IDs that is the same as a default `ParentSpanId`). A span that has not started has no parent yet, so it is a root. Inside a TUnit test, the code's spans usually have the test runner's activity as parent: see [Root spans and the test runner](#root-spans-and-the-test-runner). |
| `HasEvent(name)` *(v0.2.0+)* | The span carries an `ActivityEvent` named `name` (lists the span's event names on failure). |
| `HasExceptionEvent()` *(v0.2.0+)* | The span carries the OpenTelemetry exception event (an `ActivityEvent` named `exception`). |
| `SharesTraceWith(other)` | The span shares `other`'s `TraceId` (with hierarchical IDs, its `RootId`). A span that has not started has no trace and shares one with no span, not even with another unstarted span. |
| `HasDurationLessThan(max)` / `HasDurationAtLeast(min)` | The stopped span's `Duration` is below `max` / at least `min` (real clock; see [Durations](#durations)). |
| `HasChildSpan(capture, name)` | `capture` holds a direct child of the span with that operation name; awaiting it returns the child. |
| `HasChildSpanCount(capture, count)` | `capture` holds exactly that many direct children of the span. |

Capture-level assertions on `Assert.That(capture)` where `capture` is a `SpanCapture`:

| Assertion | Description |
|---|---|
| `HasSpan(operationName)` | The capture contains a span with that operation name (shows the captured spans as a tree on failure). |
| `HasSpanWithTag(operationName, tagKey, tagValue)` | The capture contains a span with that operation name and tag value; awaiting it returns the span (see [Finding a span](#finding-a-span)). |
| `HasNoSpan(operationName)` *(v0.2.0+)* | The capture contains no span with that operation name. |
| `HasSpanCount(int)` *(v0.2.0+)* | The capture collected exactly that many spans. |

The `SpanCapture` core also exposes query helpers for locating spans without an assertion:
`FindByOperationName`, `FindByOperationNameAndTag`, and `ChildrenOf`, and `SpanTreeRenderer.Render` for the tree text. `ForSource` and `ForSources` have overloads (v0.2.0+) that take an `ActivitySamplingResult` to capture under a chosen sampling level (the second parameter of `ForSource`, the first of `ForSources`); see [Root spans and the test runner](#root-spans-and-the-test-runner) for when a lower level takes effect.

## Failure diagnostics

TUnit prints `Expected` and the assertion's expectation, then `but` and what the assertion found. The
expectation names the check; the text after `but` describes what was found, with names and values in
single quotes. A value the expectation cannot show (a tag value, the expected parent) appears once, in
parentheses:

```text
Expected to have status Error
but it was Ok
```

```text
Expected to be a child of the given parent span
but its parent was 'order.place' (span 1fc6d440a2bbf905) in trace 31bcce556ae3466de83bbe11ff2c5145 (expected 'order.cancel' (span 8b0ecb5f493f6354) in trace 31bcce556ae3466de83bbe11ff2c5145)
```

Capture-level failures show the captured spans as a tree (see
[Rendering the spans as a tree](#rendering-the-spans-as-a-tree)):

```text
Expected to include a span named 'order.refund'
but none did; captured spans:
  order.place [Server, Ok]
    order.persist [Client, Unset]
```

TUnit builds the failure text, so its exact format can differ between TUnit versions.

Every span assertion also accepts `.Because(reason)` to attach a domain explanation to the failure,
the same as any other TUnit assertion (it is inherited from the base assertion type):

```csharp
await Assert.That(span).HasTagValue("process.id", workerId).Because("the worker stamps its pid");
```

## Compared with other ways to test spans

Checked on 2026-11-13.

- **The OpenTelemetry in-memory exporter** (`OpenTelemetry.Exporter.InMemory`, added with
  `AddInMemoryExporter` on a `TracerProviderBuilder`) collects what the SDK pipeline exports into a list.
  Use it when the test must prove the SDK setup itself: samplers, processors, resource attributes.
  `SpanCapture` has no SDK and no dependency; it listens to the `ActivitySource` directly, so it sees the
  spans as the code emitted them, and it comes with the queries and the assertions above.
- **A hand-written `ActivityListener`** is what `SpanCapture` is, packaged: the listener's lifetime follows
  a `using`, several sources can be captured at once, and the assertions fail with the captured spans.
- **TUnit.OpenTelemetry** wires an OpenTelemetry provider into the test process, exports TUnit's own test
  spans and correlates the spans of the code under test with the test that ran it. It has no API for
  asserting on spans. It complements this package: it shows the spans of a test run, this one asserts on
  them inside a test. TUnit's HTML report and TUnit.OpenTelemetry are also why a test's spans have the
  test runner's activity as parent (see [Root spans and the test runner](#root-spans-and-the-test-runner)).

## Design notes

### Why a raw `ActivityListener`, not the OpenTelemetry SDK

Capture is a BCL `ActivityListener` over an `ActivitySource`, not an OpenTelemetry `TracerProvider` with an in-memory exporter. That keeps the core `TracingAssertions` package free of NuGet dependencies (`System.Diagnostics.DiagnosticSource` is in the shared framework; the adapter depends only on TUnit and the core) and AOT-safe, and it matches how production code emits spans (a plain `ActivitySource`) without standing up an SDK pipeline in the test.

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
Shipped since: the span lookup that hands back the span (`HasSpanWithTag`), the child-span and duration
assertions, the tree renderer with the tree in every capture-level failure, failure texts that state
the expectation once, and relationships that work with hierarchical activity IDs.

Deferred (no current demand): span links and baggage, multi-level child-chain matchers, and type-aware
(non-`ToString`) tag matching.

Demand-driven; no fixed timeline.

## Family compatibility

The nine assertion-family packages: `LogAssertions.TUnit`, `TimeAssertions.TUnit`,
`SnapshotAssertions.TUnit`, `MathAssertions.TUnit`, `JsonAssertions.TUnit`, `SseAssertions.TUnit`,
`GrpcAssertions.TUnit`, `TracingAssertions.TUnit`, and `MetricsAssertions.TUnit`: release independently and target every .NET
version in Microsoft support, currently .NET 10 and .NET 11 (see the
[TFM policy in CONVENTIONS.md](CONVENTIONS.md#tfm-policy) for the rotation schedule). **Mix versions
freely.** Each package ships under SemVer with `EnablePackageValidation` strict-mode ApiCompat against
its previous baseline, so binary breaks within a version line are caught at pack time.

## Pair with

- **[`LogAssertions.TUnit`](https://www.nuget.org/packages/LogAssertions.TUnit/)**: fluent log assertions over `Microsoft.Extensions.Logging.Testing.FakeLogCollector`.
- **[`TimeAssertions.TUnit`](https://www.nuget.org/packages/TimeAssertions.TUnit/)**: `TimeProvider`-aware time assertions and cross-cutting `.WithinTimeBudget(...)` chain methods. Span duration assertions read the real clock (a span has no `TimeProvider`), so they suit coarse budgets; use it for timers, delays and anything a fake clock can drive.
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
