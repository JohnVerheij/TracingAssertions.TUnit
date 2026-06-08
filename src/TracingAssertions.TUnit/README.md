# TracingAssertions.TUnit

[![NuGet](https://img.shields.io/nuget/v/TracingAssertions.TUnit.svg)](https://www.nuget.org/packages/TracingAssertions.TUnit/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)

> **Scope:** Test projects only. Not intended for production code.

TUnit-native OpenTelemetry distributed-tracing (`Activity` / span) assertions for .NET tests. Fluent entry points over TUnit's `Assert.That(...)` pipeline for asserting on captured spans. AOT-compatible, trimmable, no runtime reflection in the assertion path.

## What ships

Span assertions on `Assert.That(span)` where `span` is a `System.Diagnostics.Activity`:

| Entry point | Behavior |
|---|---|
| `HasOperationName(name)` | Asserts the span's `OperationName` equals `name` (ordinal). |
| `HasTag(key)` | Asserts a tag `key` is present (non-null value). |
| `HasTagValue(key, value)` | Asserts the tag `key` matches `value` (compared by invariant `ToString`). |
| `HasStatus(status)` | Asserts the span's `Status` equals the given `ActivityStatusCode`. |
| `IsChildOf(parent)` | Asserts a single-hop parent/child relationship in the same trace. |
| `SharesTraceWith(other)` | Asserts two spans share a `TraceId`. |
| `HasSpan(operationName)` (on `SpanCapture`) | Asserts the capture contains a span with that operation name. |

The framework-agnostic core (`TracingAssertions`) ships `SpanCapture` for collecting spans from one or more `ActivitySource`s via a raw `ActivityListener` (no OpenTelemetry SDK or NuGet runtime dependency), plus the query helpers `FindByOperationName`, `FindByOperationNameAndTag`, and `ChildrenOf`.

## Install

```bash
dotnet add package TracingAssertions.TUnit
```

**Requirements:** TUnit 1.51.0 or later, .NET 10. The framework-agnostic `TracingAssertions` core comes transitively.

## Quick start

```csharp
using System.Diagnostics;
using TracingAssertions;

using var capture = SpanCapture.ForSource("MyCompany.MyService");

using (var span = MyActivitySource.StartActivity("order.created"))
{
    await Assert.That(span).HasOperationName("order.created");
}
```

The full reference is in the [GitHub README](https://github.com/JohnVerheij/TracingAssertions.TUnit#readme).

## License

MIT. No runtime dependencies beyond the BCL.
