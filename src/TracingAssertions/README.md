# TracingAssertions

[![NuGet](https://img.shields.io/nuget/v/TracingAssertions.svg)](https://www.nuget.org/packages/TracingAssertions/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0%20%7C%2011.0-512BD4.svg)](https://dotnet.microsoft.com/download)

> **Scope:** Test projects only. Not intended for production code.

> Part of the **[DotNetAssertions](https://dotnetassertions.dev)** family. This is the framework-agnostic core; the TUnit assertions live in the matching `.TUnit` package.

Framework-agnostic core for the TracingAssertions package family. The TUnit-native fluent assertion entry points ship in the adapter package [`TracingAssertions.TUnit`](https://www.nuget.org/packages/TracingAssertions.TUnit/).

> **Most users want [`TracingAssertions.TUnit`](https://www.nuget.org/packages/TracingAssertions.TUnit/), not this package directly.** Install this core directly only when authoring a non-TUnit adapter or when you only need the span-capture primitive.

## What's in this package

- **`SpanCapture`**: a disposable, per-test capture that starts a raw `ActivityListener` (sampling `AllDataAndRecorded` by default) over one or more `ActivitySource`s matched by name and collects the stopped `System.Diagnostics.Activity` spans. `ForSource(name)` / `ForSources(names)` create one (with overloads taking an `ActivitySamplingResult` since v0.2.0 to capture under a chosen sampling level), `Captured` exposes the collected spans in completion order, and `Dispose()` detaches the listener. Use it with a `using` statement so the listener lives exactly as long as the test. The listener is process-wide: tests that emit on the same source at the same time see each other's spans, so scope assertions to a root activity the test starts with `ChildrenOf(root)` (see the [worked example](https://github.com/JohnVerheij/TracingAssertions.TUnit#worked-example-one-operation-two-spans)).
- **Query helpers** on the capture: `FindByOperationName(name)`, `FindByOperationNameAndTag(name, tagKey, tagValue)` (tag compared by invariant `ToString`), and `ChildrenOf(parent)` (direct children in the same trace; this compares W3C activity IDs, the .NET default, and the `ParentId` and `Id` of spans with hierarchical IDs).
- **`SpanTreeRenderer`**: `Render(capture, options)` prints the captured spans as an indented tree (operation name, kind, status and the tags chosen in `SpanTreeOptions.TagKeys`), without ids or durations. Siblings are ordered by start time, then operation name, and spans equal in both keep their capture order.

No OpenTelemetry SDK, no exporter pipeline, and no NuGet runtime dependency (`System.Diagnostics.DiagnosticSource` is in the shared framework). AOT-compatible, trimmable, no runtime reflection.

## Install

```bash
dotnet add package TracingAssertions
```

TUnit users install the adapter [`TracingAssertions.TUnit`](https://www.nuget.org/packages/TracingAssertions.TUnit/) instead; it brings this core transitively.

## License

MIT throughout. No NuGet dependencies.
