using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
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
    /// showing the captured spans as a tree.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="capture"/> or
    /// <paramref name="operationName"/> is <see langword="null"/>.</exception>
    [GenerateAssertion(ExpectationMessage = "to include a span named '{operationName}'")]
    public static AssertionResult HasSpan(this SpanCapture capture, string operationName)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(operationName);

        // One snapshot: the search and the tree in a failure come from the same spans.
        var spans = capture.Captured;
        return SpanCapture.FindIn(spans, operationName) is not null
            ? AssertionResult.Passed
            : AssertionResult.Failed("none did; " + SpanTreeRenderer.Describe(spans));
    }

    /// <summary>Asserts that <paramref name="capture"/> contains a span whose
    /// <see cref="Activity.OperationName"/> equals <paramref name="operationName"/> (ordinal) and that carries a
    /// tag <paramref name="tagKey"/> whose value matches <paramref name="tagValue"/> (compared by invariant
    /// <see cref="object.ToString"/>). Awaiting the assertion returns the first such span, so the test can go on
    /// with it without a null check.</summary>
    /// <remarks>The overload set of <c>HasSpan</c> has no tag form on purpose: a string third argument would bind
    /// to the caller-expression parameter. The failure shows the captured spans as a tree with the tag
    /// <paramref name="tagKey"/> on each span.</remarks>
    /// <param name="capture">The span capture, as the receiver of the fluent assertion.</param>
    /// <param name="operationName">The operation name of the span to find.</param>
    /// <param name="tagKey">The tag key the span must carry.</param>
    /// <param name="tagValue">The tag value the span must carry for <paramref name="tagKey"/>.</param>
    /// <returns>The first matching span when there is one; otherwise a failing assertion showing the captured
    /// spans.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="capture"/>, <paramref name="operationName"/>,
    /// <paramref name="tagKey"/> or <paramref name="tagValue"/> is <see langword="null"/>.</exception>
    [GenerateAssertion(ExpectationMessage = "to include a span named '{operationName}' with tag '{tagKey}' with the expected value")]
    public static AssertionResult<Activity> HasSpanWithTag(
        this SpanCapture capture, string operationName, string tagKey, object tagValue)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(operationName);
        ArgumentNullException.ThrowIfNull(tagKey);
        ArgumentNullException.ThrowIfNull(tagValue);

        // One snapshot: the search and the tree in a failure come from the same spans.
        var spans = capture.Captured;
        var span = SpanCapture.FindIn(spans, operationName, tagKey, tagValue);
        if (span is not null)
        {
            return AssertionResult<Activity>.Passed(span);
        }

        return AssertionResult.Failed(string.Concat(
            "none did (the value looked for was ",
            SpanDescriptions.QuoteValue(tagValue),
            "); ",
            SpanTreeRenderer.Describe(spans, new SpanTreeOptions { TagKeys = [tagKey] })));
    }

    /// <summary>Asserts that <paramref name="capture"/> contains no span whose
    /// <see cref="Activity.OperationName"/> equals <paramref name="operationName"/> (ordinal): the
    /// inverse of <see cref="HasSpan"/>, for verifying an operation was never traced.</summary>
    /// <param name="capture">The span capture, as the receiver of the fluent assertion.</param>
    /// <param name="operationName">The operation name expected to be absent.</param>
    /// <returns>A passing assertion when no matching span was captured; otherwise a failing
    /// assertion showing the captured spans as a tree.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="capture"/> or
    /// <paramref name="operationName"/> is <see langword="null"/>.</exception>
    [GenerateAssertion(ExpectationMessage = "to include no span named '{operationName}'")]
    public static AssertionResult HasNoSpan(this SpanCapture capture, string operationName)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(operationName);

        // One snapshot: the search and the tree in a failure come from the same spans.
        var spans = capture.Captured;
        return SpanCapture.FindIn(spans, operationName) is null
            ? AssertionResult.Passed
            : AssertionResult.Failed("at least one did; " + SpanTreeRenderer.Describe(spans));
    }

    /// <summary>Asserts that <paramref name="capture"/> captured exactly <paramref name="expected"/>
    /// spans.</summary>
    /// <param name="capture">The span capture, as the receiver of the fluent assertion.</param>
    /// <param name="expected">The expected number of captured spans. Must be non-negative.</param>
    /// <returns>A passing assertion when the captured count matches; otherwise a failing assertion
    /// naming the observed count and showing the captured spans as a tree.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="capture"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="expected"/> is negative.</exception>
    [GenerateAssertion(ExpectationMessage = "to contain {expected} span(s)")]
    public static AssertionResult HasSpanCount(this SpanCapture capture, int expected)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentOutOfRangeException.ThrowIfNegative(expected);

        if (capture.CapturedCount == expected)
        {
            return AssertionResult.Passed;
        }

        // One snapshot for the whole message, so a span captured meanwhile cannot make the count and the tree disagree.
        var spans = capture.Captured;
        return AssertionResult.Failed(string.Concat(
            "it contained ", spans.Count.ToString(CultureInfo.InvariantCulture), "; ", SpanTreeRenderer.Describe(spans)));
    }
}
