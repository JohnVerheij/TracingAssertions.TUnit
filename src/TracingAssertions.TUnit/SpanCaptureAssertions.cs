using System;
using System.Diagnostics;
using System.Text;
using TracingAssertions;
using TUnit.Assertions.Attributes;
using TUnit.Assertions.Core;

namespace TracingAssertions.TUnit;

/// <summary>
/// TUnit-native fluent assertions over a <see cref="SpanCapture"/>: capture-level checks that do not
/// require the caller to first locate a span.
/// </summary>
/// <remarks>
/// Source methods carry the <c>[GenerateAssertion]</c> attribute; TUnit's source generator emits the
/// fluent <c>Assert.That(capture).&lt;Method&gt;()</c> entry point at consumer build time.
/// </remarks>
public static class SpanCaptureAssertions
{
    /// <summary>Asserts that <paramref name="capture"/> contains at least one span whose
    /// <see cref="Activity.OperationName"/> equals <paramref name="operationName"/> (ordinal).</summary>
    /// <param name="capture">The span capture, as the receiver of the fluent assertion.</param>
    /// <param name="operationName">The operation name expected to appear among the captured spans.</param>
    /// <returns>A passing assertion when a matching span was captured; otherwise a failing assertion
    /// listing the captured operation names.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="capture"/> or
    /// <paramref name="operationName"/> is <see langword="null"/>.</exception>
    [GenerateAssertion]
    public static AssertionResult HasSpan(this SpanCapture capture, string operationName)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(operationName);

        if (capture.FindByOperationName(operationName) is not null)
        {
            return AssertionResult.Passed;
        }

        var captured = capture.Captured;
        var sb = new StringBuilder();
        sb.Append("the captured spans to include one with operation name \"")
          .Append(operationName)
          .Append("\"\n  but none did (captured: ");
        if (captured.Count is 0)
        {
            sb.Append("<none>");
        }
        else
        {
            for (var i = 0; i < captured.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }

                sb.Append('"').Append(captured[i].OperationName).Append('"');
            }
        }

        sb.Append(')');
        return AssertionResult.Failed(sb.ToString());
    }

    /// <summary>Asserts that <paramref name="capture"/> contains no span whose
    /// <see cref="Activity.OperationName"/> equals <paramref name="operationName"/> (ordinal): the
    /// inverse of <see cref="HasSpan"/>, for verifying an operation was never traced.</summary>
    /// <param name="capture">The span capture, as the receiver of the fluent assertion.</param>
    /// <param name="operationName">The operation name expected to be absent.</param>
    /// <returns>A passing assertion when no matching span was captured; otherwise a failing
    /// assertion.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="capture"/> or
    /// <paramref name="operationName"/> is <see langword="null"/>.</exception>
    [GenerateAssertion]
    public static AssertionResult HasNoSpan(this SpanCapture capture, string operationName)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(operationName);

        return capture.FindByOperationName(operationName) is null
            ? AssertionResult.Passed
            : AssertionResult.Failed(string.Concat(
                "the captured spans to include none with operation name \"", operationName,
                "\"\n  but at least one did"));
    }

    /// <summary>Asserts that <paramref name="capture"/> captured exactly <paramref name="expected"/>
    /// spans.</summary>
    /// <param name="capture">The span capture, as the receiver of the fluent assertion.</param>
    /// <param name="expected">The expected number of captured spans. Must be non-negative.</param>
    /// <returns>A passing assertion when the captured count matches; otherwise a failing assertion
    /// naming the expected and observed counts.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="capture"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="expected"/> is negative.</exception>
    [GenerateAssertion]
    public static AssertionResult HasSpanCount(this SpanCapture capture, int expected)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentOutOfRangeException.ThrowIfNegative(expected);

        var actual = capture.Captured.Count;
        return actual == expected
            ? AssertionResult.Passed
            : AssertionResult.Failed(string.Concat(
                "the capture to contain ", expected.ToString(System.Globalization.CultureInfo.InvariantCulture),
                " span(s)\n  but it contained ", actual.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }
}
