# TracingAssertions.TUnit

[![NuGet](https://img.shields.io/nuget/v/TracingAssertions.TUnit.svg)](https://www.nuget.org/packages/TracingAssertions.TUnit/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)

> **Scope:** Test projects only. Not intended for production code.

TUnit-native OpenTelemetry distributed-tracing (`Activity` / span) assertions for .NET tests. Fluent entry points over TUnit's `Assert.That(...)` pipeline for asserting on captured spans. AOT-compatible, trimmable, no runtime reflection in the assertion path.

> **Foundation release (v0.0.1):** ships the `HasOperationName` span assertion. The full surface (tags, status, parent/child, same-trace, and a capture-level `HasSpan`) lands in 0.1.0.

## What ships

Assertions on `Assert.That(span)` where `span` is a `System.Diagnostics.Activity`:

| Entry point | Behaviour |
|---|---|
| `HasOperationName(name)` | Asserts the span's `OperationName` equals `name` (ordinal). |

The framework-agnostic core (`TracingAssertions`) ships `SpanCapture` for collecting spans from an `ActivitySource` via a raw `ActivityListener`, with no OpenTelemetry SDK or NuGet runtime dependency.

## Install

```bash
dotnet add package TracingAssertions.TUnit
```

**Requirements:** TUnit 1.49.0 or later, .NET 10. The framework-agnostic `TracingAssertions` core comes transitively.

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
