# TracingAssertions

[![NuGet](https://img.shields.io/nuget/v/TracingAssertions.svg)](https://www.nuget.org/packages/TracingAssertions/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)

> **Scope:** Test projects only. Not intended for production code.

Framework-agnostic core for the TracingAssertions package family. The TUnit-native fluent assertion entry points ship in the adapter package [`TracingAssertions.TUnit`](https://www.nuget.org/packages/TracingAssertions.TUnit/).

> **Most users want [`TracingAssertions.TUnit`](https://www.nuget.org/packages/TracingAssertions.TUnit/), not this package directly.** Install this core directly only when authoring a non-TUnit adapter or when you only need the span-capture primitive.

## What's in this package

- **`SpanCapture`**: a disposable, per-test capture that starts a raw `ActivityListener` (sampling `AllDataAndRecorded`) over one or more `ActivitySource`s matched by name and collects the stopped `System.Diagnostics.Activity` spans. `ForSource(name)` / `ForSources(names)` create one (with overloads taking an `ActivitySamplingResult` since v0.2.0 to capture under a chosen sampling level), `Captured` exposes the collected spans in completion order, and `Dispose()` detaches the listener. Use it with a `using` statement for per-test isolation.
- **Query helpers** on the capture: `FindByOperationName(name)`, `FindByOperationNameAndTag(name, tagKey, tagValue)` (tag compared by invariant `ToString`), and `ChildrenOf(parent)` (direct children in the same trace).

No OpenTelemetry SDK, no exporter pipeline, and no NuGet runtime dependency (`System.Diagnostics.DiagnosticSource` is in the shared framework). AOT-compatible, trimmable, no runtime reflection.

## Install

```bash
dotnet add package TracingAssertions.TUnit
```

The core (`TracingAssertions`) comes transitively; install it directly only when authoring a non-TUnit adapter for the assertion family.

## License

MIT throughout. No runtime dependencies beyond the BCL.
