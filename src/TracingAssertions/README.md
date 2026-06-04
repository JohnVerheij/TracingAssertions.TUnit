# TracingAssertions

Framework-agnostic core for OpenTelemetry distributed-tracing (`System.Diagnostics.Activity`)
assertions in .NET test projects. This package ships the capture primitive; the fluent assertion
entry points live in test-framework adapter packages (`TracingAssertions.TUnit`).

No OpenTelemetry SDK, no exporter pipeline, no NuGet runtime dependency
(`System.Diagnostics.DiagnosticSource` is in the shared framework). AOT-compatible, trimmable, no
runtime reflection.

> **Foundation release (v0.0.1):** ships single-source `SpanCapture` and `Captured`. Multi-source
> capture and span-query helpers (find-by-operation-name, parent/child navigation) land in 0.1.0.

## What it provides

```csharp
using System.Diagnostics;
using TracingAssertions;

// Disposable, per-test capture: starts a raw ActivityListener (AllDataAndRecorded) over one source.
using var capture = SpanCapture.ForSource("MyCompany.MyService");

using (MyActivitySource.StartActivity("op.work"))
{
    // ... run the code under test ...
}

IReadOnlyList<Activity> spans = capture.Captured;   // stopped spans, in completion order
```

Create one capture per test with `using` for isolation; disposing it detaches the listener.

## Adapters

`TracingAssertions.TUnit` provides the fluent `Assert.That(span)` assertions for TUnit. The core has
no test-framework dependency, so adapters for other frameworks are possible if there is demand.

See the [project README](https://github.com/JohnVerheij/TracingAssertions.TUnit) for the full
reference and roadmap.
