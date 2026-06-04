using System;
using System.Diagnostics;
using TUnit.Assertions.Attributes;
using TUnit.Assertions.Core;

namespace TracingAssertions.TUnit;

/// <summary>
/// TUnit-native fluent assertions over <see cref="Activity"/> spans (typically captured by
/// <see cref="TracingAssertions.SpanCapture"/>). Foundation release (v0.0.1): ships
/// <see cref="HasOperationName"/>; the full surface (tag-exists / tag-value / status /
/// is-child-of / same-trace, plus capture-level <c>HasSpan</c>) lands in 0.1.0.
/// </summary>
/// <remarks>
/// Source methods carry the <c>[GenerateAssertion]</c> attribute; TUnit's source generator emits
/// the fluent <c>Assert.That(activity).&lt;Method&gt;()</c> entry point at consumer build time. The
/// generated chain is AOT-clean (no runtime reflection in the assertion path).
/// </remarks>
public static class SpanAssertions
{
    /// <summary>Asserts that <paramref name="span"/> has <see cref="Activity.OperationName"/> equal
    /// to <paramref name="operationName"/> (ordinal).</summary>
    /// <param name="span">The captured span to inspect, as the receiver of the fluent assertion.</param>
    /// <param name="operationName">The operation name the span is expected to carry.</param>
    /// <returns>A passing assertion when the operation name matches; otherwise a failing assertion
    /// naming the expected and observed operation names.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="span"/> or
    /// <paramref name="operationName"/> is <see langword="null"/>.</exception>
    [GenerateAssertion]
    public static AssertionResult HasOperationName(this Activity span, string operationName)
    {
        ArgumentNullException.ThrowIfNull(span);
        ArgumentNullException.ThrowIfNull(operationName);

        return string.Equals(span.OperationName, operationName, StringComparison.Ordinal)
            ? AssertionResult.Passed
            : AssertionResult.Failed(string.Concat(
                "the span to have operation name \"",
                operationName,
                "\"\n  but it was \"",
                span.OperationName,
                "\""));
    }
}
