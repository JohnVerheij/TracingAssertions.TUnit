using System;
using System.Diagnostics;
using System.Linq;
using TUnit.Assertions.Attributes;
using TUnit.Assertions.Core;

namespace TracingAssertions.TUnit;

/// <summary>
/// TUnit-native fluent assertions over <see cref="Activity"/> spans (typically captured by
/// <see cref="TracingAssertions.SpanCapture"/>): operation name, tag existence and value, status,
/// kind, root, events (<c>HasKind</c>, <c>IsRoot</c>, <c>HasEvent</c>, <c>HasExceptionEvent</c>), duration
/// (<c>HasDurationLessThan</c>, <c>HasDurationAtLeast</c>), child spans (<c>HasChildSpan</c>,
/// <c>HasChildSpanCount</c>), and single-hop parent/child and same-trace relationships.
/// </summary>
/// <remarks>
/// Source methods carry the <c>[GenerateAssertion]</c> attribute; TUnit's source generator emits
/// the fluent <c>Assert.That(activity).&lt;Method&gt;()</c> entry point at consumer build time. The
/// generated chain is AOT-clean (no runtime reflection in the assertion path). Tag values are
/// compared by their invariant <see cref="object.ToString"/> form, matching how spans carry
/// heterogeneously-typed tag values. Each assertion states its expectation through
/// <c>ExpectationMessage</c>, and the failure text after <c>but</c> describes only what was found, with
/// names and values in single quotes.
/// </remarks>
public static class SpanAssertions
{
    /// <summary>The failure text for a span that has not stopped: an activity that is still running reports a zero
    /// duration, which would make a bound check pass or fail for the wrong reason.</summary>
    private const string NotStopped = "the span had not stopped, so it has no duration yet";

    /// <summary>Asserts that <paramref name="span"/> has <see cref="Activity.OperationName"/> equal
    /// to <paramref name="operationName"/> (ordinal).</summary>
    /// <param name="span">The captured span, as the receiver of the fluent assertion.</param>
    /// <param name="operationName">The operation name the span is expected to carry.</param>
    /// <returns>A passing assertion when the operation name matches; otherwise a failing assertion
    /// naming the observed operation name.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="span"/> or
    /// <paramref name="operationName"/> is <see langword="null"/>.</exception>
    [GenerateAssertion(ExpectationMessage = "to have operation name '{operationName}'")]
    public static AssertionResult HasOperationName(this Activity span, string operationName)
    {
        ArgumentNullException.ThrowIfNull(span);
        ArgumentNullException.ThrowIfNull(operationName);

        return string.Equals(span.OperationName, operationName, StringComparison.Ordinal)
            ? AssertionResult.Passed
            : AssertionResult.Failed("it was " + SpanDescriptions.Quote(span.OperationName));
    }

    /// <summary>Asserts that <paramref name="span"/> carries a tag <paramref name="key"/> with a
    /// non-null value.</summary>
    /// <param name="span">The captured span.</param>
    /// <param name="key">The tag key expected to be present.</param>
    /// <returns>A passing assertion when the tag is present; otherwise a failing assertion.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="span"/> or <paramref name="key"/> is
    /// <see langword="null"/>.</exception>
    [GenerateAssertion(ExpectationMessage = "to have a tag '{key}'")]
    public static AssertionResult HasTag(this Activity span, string key)
    {
        ArgumentNullException.ThrowIfNull(span);
        ArgumentNullException.ThrowIfNull(key);

        return span.GetTagItem(key) is not null
            ? AssertionResult.Passed
            : AssertionResult.Failed("it had no such tag");
    }

    /// <summary>Asserts that <paramref name="span"/> carries a tag <paramref name="key"/> whose value
    /// matches <paramref name="value"/> (compared by invariant <see cref="object.ToString"/>).</summary>
    /// <param name="span">The captured span.</param>
    /// <param name="key">The tag key to inspect.</param>
    /// <param name="value">The expected tag value.</param>
    /// <returns>A passing assertion when the tag value matches; otherwise a failing assertion naming
    /// the observed and the expected value, or reporting the tag as absent.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="span"/>, <paramref name="key"/>, or
    /// <paramref name="value"/> is <see langword="null"/>.</exception>
    [GenerateAssertion(ExpectationMessage = "to have tag '{key}' with the expected value")]
    public static AssertionResult HasTagValue(this Activity span, string key, object value)
    {
        ArgumentNullException.ThrowIfNull(span);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        var actual = span.GetTagItem(key);
        if (actual is null)
        {
            return AssertionResult.Failed(string.Concat(
                "the tag was absent (expected ", SpanDescriptions.QuoteValue(value), ")"));
        }

        return SpanCapture.TagValueEquals(actual, value)
            ? AssertionResult.Passed
            : AssertionResult.Failed(string.Concat(
                "it was ", SpanDescriptions.QuoteValue(actual), " (expected ", SpanDescriptions.QuoteValue(value), ")"));
    }

    /// <summary>Asserts that <paramref name="span"/> has <see cref="Activity.Status"/> equal to
    /// <paramref name="status"/>.</summary>
    /// <param name="span">The captured span.</param>
    /// <param name="status">The expected status code.</param>
    /// <returns>A passing assertion when the status matches; otherwise a failing assertion naming the
    /// observed status.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="span"/> is <see langword="null"/>.</exception>
    [GenerateAssertion(ExpectationMessage = "to have status {status}")]
    public static AssertionResult HasStatus(this Activity span, ActivityStatusCode status)
    {
        ArgumentNullException.ThrowIfNull(span);

        return span.Status == status
            ? AssertionResult.Passed
            : AssertionResult.Failed("it was " + span.Status.ToString());
    }

    /// <summary>Asserts that <paramref name="span"/> is a direct child of <paramref name="parent"/>: it was started
    /// while the parent was current, or with the parent's context.</summary>
    /// <remarks>With W3C activity ids (the .NET default) the span's <see cref="Activity.ParentSpanId"/> must equal
    /// <paramref name="parent"/>'s <see cref="Activity.SpanId"/> and the trace ids must match. With
    /// <see cref="ActivityIdFormat.Hierarchical"/> ids the span's <see cref="Activity.ParentId"/> must equal the
    /// parent's <see cref="Activity.Id"/>. A hierarchical span is never the child of a W3C span or the
    /// reverse. A span that has not started has no ids yet and is nobody's child.</remarks>
    /// <param name="span">The captured span.</param>
    /// <param name="parent">The expected parent span.</param>
    /// <returns>A passing assertion when the parent/trace relationship holds; otherwise a failing
    /// assertion naming the observed parent and the expected one.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="span"/> or <paramref name="parent"/>
    /// is <see langword="null"/>.</exception>
    [GenerateAssertion(ExpectationMessage = "to be a child of the given parent span")]
    public static AssertionResult IsChildOf(this Activity span, Activity parent)
    {
        ArgumentNullException.ThrowIfNull(span);
        ArgumentNullException.ThrowIfNull(parent);

        return SpanRelations.IsChildOf(span, parent)
            ? AssertionResult.Passed
            : AssertionResult.Failed(string.Concat(
                "its parent was ", SpanDescriptions.ParentOf(span),
                " in trace ", SpanDescriptions.TraceOf(span),
                " (expected ", SpanDescriptions.Span(parent),
                " in trace ", SpanDescriptions.TraceOf(parent), ")"));
    }

    /// <summary>Asserts that <paramref name="span"/> belongs to the same trace as <paramref name="other"/>
    /// (context propagation across a boundary).</summary>
    /// <remarks>With W3C activity ids (the .NET default) the <see cref="Activity.TraceId"/> values must be equal;
    /// with <see cref="ActivityIdFormat.Hierarchical"/> ids the <see cref="Activity.RootId"/> values must be
    /// equal. A hierarchical span never shares a trace with a W3C span. A span that has not started has no ids yet
    /// and shares a trace with no span, not even with another one that has not started.</remarks>
    /// <param name="span">The captured span.</param>
    /// <param name="other">The span expected to be in the same trace.</param>
    /// <returns>A passing assertion when the traces match; otherwise a failing assertion naming the
    /// observed and the expected trace.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="span"/> or <paramref name="other"/> is
    /// <see langword="null"/>.</exception>
    [GenerateAssertion(ExpectationMessage = "to share a trace with the given span")]
    public static AssertionResult SharesTraceWith(this Activity span, Activity other)
    {
        ArgumentNullException.ThrowIfNull(span);
        ArgumentNullException.ThrowIfNull(other);

        return SpanRelations.SharesTrace(span, other)
            ? AssertionResult.Passed
            : AssertionResult.Failed(string.Concat(
                "it was in trace ", SpanDescriptions.TraceOf(span),
                " (expected trace ", SpanDescriptions.TraceOf(other), ")"));
    }

    /// <summary>Asserts that <paramref name="span"/> has <see cref="Activity.Kind"/> equal to
    /// <paramref name="kind"/> (for example <see cref="ActivityKind.Server"/> for an inbound request
    /// span or <see cref="ActivityKind.Client"/> for an outbound call).</summary>
    /// <param name="span">The captured span.</param>
    /// <param name="kind">The expected activity kind.</param>
    /// <returns>A passing assertion when the kind matches; otherwise a failing assertion naming the
    /// observed kind.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="span"/> is <see langword="null"/>.</exception>
    [GenerateAssertion(ExpectationMessage = "to have kind {kind}")]
    public static AssertionResult HasKind(this Activity span, ActivityKind kind)
    {
        ArgumentNullException.ThrowIfNull(span);

        return span.Kind == kind
            ? AssertionResult.Passed
            : AssertionResult.Failed("it was " + span.Kind.ToString());
    }

    /// <summary>Asserts that <paramref name="span"/> is a root span: it has no parent. A span created under a
    /// propagated remote parent carries that parent's span id and is not a root.</summary>
    /// <remarks>The span's <see cref="Activity.ParentId"/> must be <see langword="null"/>: with W3C activity ids (the
    /// .NET default) that is the same as a default (all-zero) <see cref="Activity.ParentSpanId"/>, and a span with
    /// a parent id that is not a W3C id has a parent although its parent span id is default. A span that has not
    /// started has no parent yet, so it is a root.</remarks>
    /// <param name="span">The captured span.</param>
    /// <returns>A passing assertion when the span is a root; otherwise a failing assertion naming the
    /// observed parent.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="span"/> is <see langword="null"/>.</exception>
    [GenerateAssertion(ExpectationMessage = "to be a root span (no parent)")]
    public static AssertionResult IsRoot(this Activity span)
    {
        ArgumentNullException.ThrowIfNull(span);

        return SpanRelations.IsRoot(span)
            ? AssertionResult.Passed
            : AssertionResult.Failed("its parent was " + SpanDescriptions.ParentOf(span));
    }

    /// <summary>Asserts that <paramref name="span"/> carries an <see cref="ActivityEvent"/> named
    /// <paramref name="name"/> (ordinal).</summary>
    /// <param name="span">The captured span.</param>
    /// <param name="name">The event name expected to be present.</param>
    /// <returns>A passing assertion when an event with that name is present; otherwise a failing
    /// assertion listing the event names the span carries.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="span"/> or <paramref name="name"/> is
    /// <see langword="null"/>.</exception>
    [GenerateAssertion(ExpectationMessage = "to have an event named '{name}'")]
    public static AssertionResult HasEvent(this Activity span, string name)
    {
        ArgumentNullException.ThrowIfNull(span);
        ArgumentNullException.ThrowIfNull(name);

        return span.Events.Any(spanEvent => string.Equals(spanEvent.Name, name, StringComparison.Ordinal))
            ? AssertionResult.Passed
            : AssertionResult.Failed("it had " + DescribeEventNames(span));
    }

    /// <summary>Asserts that <paramref name="span"/> carries the OpenTelemetry exception event (an
    /// <see cref="ActivityEvent"/> named <c>"exception"</c>, as recorded by
    /// <see cref="Activity.AddException(Exception, in System.Diagnostics.TagList, System.DateTimeOffset)"/>
    /// or an OpenTelemetry instrumentation).</summary>
    /// <param name="span">The captured span.</param>
    /// <returns>A passing assertion when an exception event is present; otherwise a failing assertion
    /// listing the event names the span carries.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="span"/> is <see langword="null"/>.</exception>
    [GenerateAssertion(ExpectationMessage = "to have an exception event")]
    public static AssertionResult HasExceptionEvent(this Activity span)
    {
        ArgumentNullException.ThrowIfNull(span);

        return span.Events.Any(static spanEvent => string.Equals(spanEvent.Name, "exception", StringComparison.Ordinal))
            ? AssertionResult.Passed
            : AssertionResult.Failed("it had " + DescribeEventNames(span));
    }

    /// <summary>Asserts that <paramref name="span"/> ran for less than <paramref name="max"/>, measured by its
    /// <see cref="Activity.Duration"/>.</summary>
    /// <remarks>A span reads the real clock: it has no <see cref="TimeProvider"/>, so a test cannot advance it.
    /// Choose a bound with room for a slow machine. A span that has not stopped has no duration yet and
    /// fails.</remarks>
    /// <param name="span">The captured span.</param>
    /// <param name="max">The exclusive upper bound. Must not be negative.</param>
    /// <returns>A passing assertion when the span stopped and its duration is below the bound; otherwise a failing
    /// assertion naming the observed duration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="span"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="max"/> is negative.</exception>
    [GenerateAssertion(ExpectationMessage = "to have a duration less than {max}")]
    public static AssertionResult HasDurationLessThan(this Activity span, TimeSpan max)
    {
        ArgumentNullException.ThrowIfNull(span);
        ArgumentOutOfRangeException.ThrowIfLessThan(max, TimeSpan.Zero);

        if (!span.IsStopped)
        {
            return AssertionResult.Failed(NotStopped);
        }

        return span.Duration < max
            ? AssertionResult.Passed
            : AssertionResult.Failed("it ran for " + span.Duration.ToString());
    }

    /// <summary>Asserts that <paramref name="span"/> ran for at least <paramref name="min"/>, measured by its
    /// <see cref="Activity.Duration"/>.</summary>
    /// <remarks>A span reads the real clock: it has no <see cref="TimeProvider"/>, so a test cannot advance it.
    /// A span that has not stopped has no duration yet and fails.</remarks>
    /// <param name="span">The captured span.</param>
    /// <param name="min">The inclusive lower bound. Must not be negative.</param>
    /// <returns>A passing assertion when the span stopped and its duration is at least the bound; otherwise a
    /// failing assertion naming the observed duration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="span"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="min"/> is negative.</exception>
    [GenerateAssertion(ExpectationMessage = "to have a duration of at least {min}")]
    public static AssertionResult HasDurationAtLeast(this Activity span, TimeSpan min)
    {
        ArgumentNullException.ThrowIfNull(span);
        ArgumentOutOfRangeException.ThrowIfLessThan(min, TimeSpan.Zero);

        if (!span.IsStopped)
        {
            return AssertionResult.Failed(NotStopped);
        }

        return span.Duration >= min
            ? AssertionResult.Passed
            : AssertionResult.Failed("it ran for " + span.Duration.ToString());
    }

    /// <summary>Asserts that <paramref name="capture"/> holds a direct child of <paramref name="parent"/> whose
    /// operation name equals <paramref name="operationName"/> (ordinal). Awaiting the assertion returns the first
    /// such child.</summary>
    /// <remarks>Children are found with <see cref="SpanCapture.ChildrenOf(Activity)"/>, so the same relationship
    /// rules apply (W3C and hierarchical ids). Only spans that have stopped are in the capture.</remarks>
    /// <param name="parent">The parent span, as the receiver of the fluent assertion.</param>
    /// <param name="capture">The capture that holds the child spans.</param>
    /// <param name="operationName">The operation name the child is expected to carry.</param>
    /// <returns>The first matching child when there is one; otherwise a failing assertion listing the
    /// parent's children and the captured spans.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parent"/>, <paramref name="capture"/> or
    /// <paramref name="operationName"/> is <see langword="null"/>.</exception>
    [GenerateAssertion(ExpectationMessage = "to have a child span named '{operationName}'")]
    public static AssertionResult<Activity> HasChildSpan(this Activity parent, SpanCapture capture, string operationName)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(operationName);

        // One snapshot: the children and the tree in a failure come from the same spans.
        var spans = capture.Captured;
        var children = SpanCapture.ChildrenIn(spans, parent);
        var match = children.FirstOrDefault(
            child => string.Equals(child.OperationName, operationName, StringComparison.Ordinal));
        return match is not null
            ? AssertionResult<Activity>.Passed(match)
            : AssertionResult.Failed(string.Concat(
                "it had ", SpanDescriptions.Children(children), "; ", SpanTreeRenderer.Describe(spans)));
    }

    /// <summary>Asserts that <paramref name="capture"/> holds exactly <paramref name="expected"/> direct children
    /// of <paramref name="parent"/>.</summary>
    /// <remarks>Children are found with <see cref="SpanCapture.ChildrenOf(Activity)"/>. Only spans that have
    /// stopped are in the capture.</remarks>
    /// <param name="parent">The parent span, as the receiver of the fluent assertion.</param>
    /// <param name="capture">The capture that holds the child spans.</param>
    /// <param name="expected">The expected number of children. Must not be negative.</param>
    /// <returns>A passing assertion when the count matches; otherwise a failing assertion naming the observed
    /// children and the captured spans.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parent"/> or <paramref name="capture"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="expected"/> is negative.</exception>
    [GenerateAssertion(ExpectationMessage = "to have {expected} child span(s)")]
    public static AssertionResult HasChildSpanCount(this Activity parent, SpanCapture capture, int expected)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentOutOfRangeException.ThrowIfNegative(expected);

        // One snapshot: the children and the tree in a failure come from the same spans.
        var spans = capture.Captured;
        var children = SpanCapture.ChildrenIn(spans, parent);
        return children.Count == expected
            ? AssertionResult.Passed
            : AssertionResult.Failed(string.Concat(
                "it had ", SpanDescriptions.Children(children), "; ", SpanTreeRenderer.Describe(spans)));
    }

    /// <summary>Renders the event names a span carries for a failure message: <c>no events</c> when
    /// empty, otherwise <c>events:</c> and a comma-separated quoted list.</summary>
    /// <param name="span">The span whose events are listed.</param>
    /// <returns>The description.</returns>
    private static string DescribeEventNames(Activity span)
    {
        var names = span.Events.Select(static spanEvent => spanEvent.Name).ToList();
        return names.Count is 0 ? "no events" : "events: " + SpanDescriptions.Names(names, string.Empty);
    }
}
