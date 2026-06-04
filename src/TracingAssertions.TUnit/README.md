# TracingAssertions.TUnit

TUnit-native fluent assertions for OpenTelemetry distributed tracing (`System.Diagnostics.Activity`
spans) in .NET test projects. Capture spans with the `TracingAssertions` core, then assert on them
through TUnit's `Assert.That(...)` pipeline.

No OpenTelemetry SDK, no exporter pipeline, no NuGet runtime dependency. AOT-compatible, trimmable, no
runtime reflection in the assertion path. Assertion entry points are source-generated via TUnit's
`[GenerateAssertion]`.

> **Foundation release (v0.0.1):** ships the `HasOperationName` span assertion. The full surface (tags,
> status, parent/child, same-trace, and a capture-level `HasSpan`) lands in 0.1.0.

## Install

```bash
dotnet add package TracingAssertions.TUnit
```

The `TracingAssertions` core comes in transitively.

## Use

```csharp
using System.Diagnostics;
using TracingAssertions;

using var capture = SpanCapture.ForSource("MyCompany.MyService");

using (var span = MyActivitySource.StartActivity("pick.pipeline"))
{
    await Assert.That(span).HasOperationName("pick.pipeline");
}
```

## Entry points (v0.0.1)

| Assertion | Receiver | Description |
|---|---|---|
| `HasOperationName(name)` | `Activity` | Asserts the span's `OperationName` equals `name` (ordinal). |

See the [project README](https://github.com/JohnVerheij/TracingAssertions.TUnit) for the full
reference and roadmap.
