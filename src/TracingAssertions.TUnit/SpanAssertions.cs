using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using TUnit.Assertions.Attributes;
using TUnit.Assertions.Core;

namespace TracingAssertions.TUnit;

/// <summary>
/// TUnit-native fluent assertions over <see cref="Activity"/> spans (typically captured by
/// <see cref="TracingAssertions.SpanCapture"/>): operation name, tag existence and value, status,
/// and single-hop parent/child and same-trace relationships.
/// </summary>
/// <remarks>
/// Source methods carry the <c>[GenerateAssertion]</c> attribute; TUnit's source generator emits
/// the fluent <c>Assert.That(activity).&lt;Method&gt;()</c> entry point at consumer build time. The
/// generated chain is AOT-clean (no runtime reflection in the assertion path). Tag values are
/// compared by their invariant <see cref="object.ToString"/> form, matching how spans carry
/// heterogeneously-typed tag values.
/// </remarks>
public static class SpanAssertions
{
    /// <summary>Asserts that <paramref name="span"/> has <see cref="Activity.OperationName"/> equal
    /// to <paramref name="operationName"/> (ordinal).</summary>
    /// <param name="span">The captured span, as the receiver of the fluent assertion.</param>
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

    /// <summary>Asserts that <paramref name="span"/> carries a tag <paramref name="key"/> with a
    /// non-null value.</summary>
    /// <param name="span">The captured span.</param>
    /// <param name="key">The tag key expected to be present.</param>
    /// <returns>A passing assertion when the tag is present; otherwise a failing assertion.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="span"/> or <paramref name="key"/> is
    /// <see langword="null"/>.</exception>
    [GenerateAssertion]
    public static AssertionResult HasTag(this Activity span, string key)
    {
        ArgumentNullException.ThrowIfNull(span);
        ArgumentNullException.ThrowIfNull(key);

        return span.GetTagItem(key) is not null
            ? AssertionResult.Passed
            : AssertionResult.Failed(string.Concat(
                "the span to have a tag \"", key, "\"\n  but it had no such tag"));
    }

    /// <summary>Asserts that <paramref name="span"/> carries a tag <paramref name="key"/> whose value
    /// matches <paramref name="value"/> (compared by invariant <see cref="object.ToString"/>).</summary>
    /// <param name="span">The captured span.</param>
    /// <param name="key">The tag key to inspect.</param>
    /// <param name="value">The expected tag value.</param>
    /// <returns>A passing assertion when the tag value matches; otherwise a failing assertion naming
    /// the expected and observed values, or reporting the tag as absent.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="span"/>, <paramref name="key"/>, or
    /// <paramref name="value"/> is <see langword="null"/>.</exception>
    [GenerateAssertion]
    public static AssertionResult HasTagValue(this Activity span, string key, object value)
    {
        ArgumentNullException.ThrowIfNull(span);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        var valueText = Convert.ToString(value, CultureInfo.InvariantCulture);
        var actual = span.GetTagItem(key);
        if (actual is null)
        {
            return AssertionResult.Failed(string.Concat(
                "the span tag \"", key, "\" to be \"", valueText, "\"\n  but the tag was absent"));
        }

        var actualText = Convert.ToString(actual, CultureInfo.InvariantCulture);
        return string.Equals(actualText, valueText, StringComparison.Ordinal)
            ? AssertionResult.Passed
            : AssertionResult.Failed(string.Concat(
                "the span tag \"", key, "\" to be \"", valueText, "\"\n  but it was \"", actualText, "\""));
    }

    /// <summary>Asserts that <paramref name="span"/> has <see cref="Activity.Status"/> equal to
    /// <paramref name="status"/>.</summary>
    /// <param name="span">The captured span.</param>
    /// <param name="status">The expected status code.</param>
    /// <returns>A passing assertion when the status matches; otherwise a failing assertion naming the
    /// expected and observed status.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="span"/> is <see langword="null"/>.</exception>
    [GenerateAssertion]
    public static AssertionResult HasStatus(this Activity span, ActivityStatusCode status)
    {
        ArgumentNullException.ThrowIfNull(span);

        return span.Status == status
            ? AssertionResult.Passed
            : AssertionResult.Failed(string.Concat(
                "the span to have status ", status.ToString(), "\n  but it was ", span.Status.ToString()));
    }

    /// <summary>Asserts that <paramref name="span"/> is a direct child of <paramref name="parent"/>:
    /// its <see cref="Activity.ParentSpanId"/> equals <paramref name="parent"/>'s
    /// <see cref="Activity.SpanId"/> and it shares the same <see cref="Activity.TraceId"/>.</summary>
    /// <param name="span">The captured span.</param>
    /// <param name="parent">The expected parent span.</param>
    /// <returns>A passing assertion when the parent/trace relationship holds; otherwise a failing
    /// assertion naming the expected and observed parent span and trace.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="span"/> or <paramref name="parent"/>
    /// is <see langword="null"/>.</exception>
    [GenerateAssertion]
    public static AssertionResult IsChildOf(this Activity span, Activity parent)
    {
        ArgumentNullException.ThrowIfNull(span);
        ArgumentNullException.ThrowIfNull(parent);

        return span.ParentSpanId == parent.SpanId && span.TraceId == parent.TraceId
            ? AssertionResult.Passed
            : AssertionResult.Failed(string.Concat(
                "the span to be a child of parent span ", parent.SpanId.ToString(),
                " in trace ", parent.TraceId.ToString(),
                "\n  but its parent span was ", span.ParentSpanId.ToString(),
                " in trace ", span.TraceId.ToString()));
    }

    /// <summary>Asserts that <paramref name="span"/> shares the same <see cref="Activity.TraceId"/>
    /// as <paramref name="other"/> (context propagation across a boundary).</summary>
    /// <param name="span">The captured span.</param>
    /// <param name="other">The span expected to be in the same trace.</param>
    /// <returns>A passing assertion when the traces match; otherwise a failing assertion naming the
    /// expected and observed trace.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="span"/> or <paramref name="other"/> is
    /// <see langword="null"/>.</exception>
    [GenerateAssertion]
    public static AssertionResult SharesTraceWith(this Activity span, Activity other)
    {
        ArgumentNullException.ThrowIfNull(span);
        ArgumentNullException.ThrowIfNull(other);

        return span.TraceId == other.TraceId
            ? AssertionResult.Passed
            : AssertionResult.Failed(string.Concat(
                "the span to share trace ", other.TraceId.ToString(),
                "\n  but it was in trace ", span.TraceId.ToString()));
    }

    /// <summary>Asserts that <paramref name="span"/> has <see cref="Activity.Kind"/> equal to
    /// <paramref name="kind"/> (for example <see cref="ActivityKind.Server"/> for an inbound request
    /// span or <see cref="ActivityKind.Client"/> for an outbound call).</summary>
    /// <param name="span">The captured span.</param>
    /// <param name="kind">The expected activity kind.</param>
    /// <returns>A passing assertion when the kind matches; otherwise a failing assertion naming the
    /// expected and observed kind.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="span"/> is <see langword="null"/>.</exception>
    [GenerateAssertion]
    public static AssertionResult HasKind(this Activity span, ActivityKind kind)
    {
        ArgumentNullException.ThrowIfNull(span);

        return span.Kind == kind
            ? AssertionResult.Passed
            : AssertionResult.Failed(string.Concat(
                "the span to have kind ", kind.ToString(), "\n  but it was ", span.Kind.ToString()));
    }

    /// <summary>Asserts that <paramref name="span"/> is a root span: it has no parent, so its
    /// <see cref="Activity.ParentSpanId"/> is the default (all-zero) span id. A span created under a
    /// propagated remote parent carries that parent's span id and is not a root.</summary>
    /// <param name="span">The captured span.</param>
    /// <returns>A passing assertion when the span is a root; otherwise a failing assertion naming the
    /// observed parent span.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="span"/> is <see langword="null"/>.</exception>
    [GenerateAssertion]
    public static AssertionResult IsRoot(this Activity span)
    {
        ArgumentNullException.ThrowIfNull(span);

        return span.ParentSpanId == default
            ? AssertionResult.Passed
            : AssertionResult.Failed(string.Concat(
                "the span to be a root (no parent)\n  but its parent span was ", span.ParentSpanId.ToString()));
    }

    /// <summary>Asserts that <paramref name="span"/> carries an <see cref="ActivityEvent"/> named
    /// <paramref name="name"/> (ordinal).</summary>
    /// <param name="span">The captured span.</param>
    /// <param name="name">The event name expected to be present.</param>
    /// <returns>A passing assertion when an event with that name is present; otherwise a failing
    /// assertion listing the event names the span carries.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="span"/> or <paramref name="name"/> is
    /// <see langword="null"/>.</exception>
    [GenerateAssertion]
    public static AssertionResult HasEvent(this Activity span, string name)
    {
        ArgumentNullException.ThrowIfNull(span);
        ArgumentNullException.ThrowIfNull(name);

        return span.Events.Any(spanEvent => string.Equals(spanEvent.Name, name, StringComparison.Ordinal))
            ? AssertionResult.Passed
            : AssertionResult.Failed(string.Concat(
                "the span to have an event named \"", name, "\"\n  but it had ", DescribeEventNames(span)));
    }

    /// <summary>Asserts that <paramref name="span"/> carries the OpenTelemetry exception event (an
    /// <see cref="ActivityEvent"/> named <c>"exception"</c>, as recorded by
    /// <see cref="Activity.AddException(Exception, in System.Diagnostics.TagList, System.DateTimeOffset)"/>
    /// or an OpenTelemetry instrumentation).</summary>
    /// <param name="span">The captured span.</param>
    /// <returns>A passing assertion when an exception event is present; otherwise a failing assertion
    /// listing the event names the span carries.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="span"/> is <see langword="null"/>.</exception>
    [GenerateAssertion]
    public static AssertionResult HasExceptionEvent(this Activity span)
    {
        ArgumentNullException.ThrowIfNull(span);

        return span.Events.Any(static spanEvent => string.Equals(spanEvent.Name, "exception", StringComparison.Ordinal))
            ? AssertionResult.Passed
            : AssertionResult.Failed(string.Concat(
                "the span to have an exception event\n  but it had ", DescribeEventNames(span)));
    }

    /// <summary>Renders the event names a span carries for a failure message: <c>no events</c> when
    /// empty, otherwise a comma-separated quoted list.</summary>
    private static string DescribeEventNames(Activity span)
    {
        var sb = new System.Text.StringBuilder();
        var any = false;
        foreach (var spanEvent in span.Events)
        {
            sb.Append(any ? ", " : "events: ").Append('"').Append(spanEvent.Name).Append('"');
            any = true;
        }

        return any ? sb.ToString() : "no events";
    }
}
