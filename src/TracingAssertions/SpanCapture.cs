using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace TracingAssertions;

/// <summary>
/// Disposable, per-test capture of <see cref="Activity"/> spans emitted by one or more
/// <see cref="ActivitySource"/>s. Starts a raw <see cref="ActivityListener"/> sampling at
/// <see cref="ActivitySamplingResult.AllDataAndRecorded"/> and collects every stopped activity
/// from the listened source(s). Disposing detaches the listener.
/// </summary>
/// <remarks>
/// <para>
/// No OpenTelemetry SDK or exporter pipeline is involved: capture is a BCL
/// <see cref="ActivityListener"/> over an <see cref="ActivitySource"/> matched by name, so the type
/// is AOT-safe and carries no NuGet runtime dependency (<c>System.Diagnostics.DiagnosticSource</c>
/// is in the shared framework).
/// </para>
/// <para>
/// Create one per test with a <see langword="using"/> statement for isolation; the listener stops
/// collecting and detaches when disposed. The query helpers (<see cref="FindByOperationName"/>,
/// <see cref="FindByOperationNameAndTag"/>, <see cref="ChildrenOf"/>) read the captured set without
/// advancing any clock.
/// </para>
/// </remarks>
public sealed class SpanCapture : IDisposable
{
    private readonly ActivityListener _listener;
    private readonly ConcurrentQueue<Activity> _captured = new();

    private SpanCapture(Func<ActivitySource, bool> shouldListenTo)
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = shouldListenTo,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = static (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => _captured.Enqueue(activity),
        };
        ActivitySource.AddActivityListener(_listener);
    }

    /// <summary>Starts capturing spans from the single <see cref="ActivitySource"/> whose
    /// <see cref="ActivitySource.Name"/> equals <paramref name="sourceName"/> (ordinal).</summary>
    /// <param name="sourceName">The activity-source name to listen to.</param>
    /// <returns>A disposable capture; dispose it (ideally via <see langword="using"/>) to detach the
    /// listener at the end of the test.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sourceName"/> is <see langword="null"/>.</exception>
    public static SpanCapture ForSource(string sourceName)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        return new SpanCapture(source => string.Equals(source.Name, sourceName, StringComparison.Ordinal));
    }

    /// <summary>Starts capturing spans from any <see cref="ActivitySource"/> whose
    /// <see cref="ActivitySource.Name"/> is one of <paramref name="sourceNames"/> (ordinal).</summary>
    /// <param name="sourceNames">The activity-source names to listen to. An empty array captures
    /// nothing.</param>
    /// <returns>A disposable capture; dispose it (ideally via <see langword="using"/>) to detach the
    /// listener at the end of the test.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sourceNames"/> is
    /// <see langword="null"/>.</exception>
    public static SpanCapture ForSources(params string[] sourceNames)
    {
        ArgumentNullException.ThrowIfNull(sourceNames);
        var set = new HashSet<string>(sourceNames, StringComparer.Ordinal);
        return new SpanCapture(source => set.Contains(source.Name));
    }

    /// <summary>The spans captured so far, in completion (activity-stopped) order. Each access
    /// returns a snapshot.</summary>
    public IReadOnlyList<Activity> Captured => _captured.ToArray();

    /// <summary>Returns the first captured span whose <see cref="Activity.OperationName"/> equals
    /// <paramref name="operationName"/> (ordinal), or <see langword="null"/> if none matches.</summary>
    /// <param name="operationName">The operation name to look for.</param>
    /// <returns>The matching span, or <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="operationName"/> is
    /// <see langword="null"/>.</exception>
    public Activity? FindByOperationName(string operationName)
    {
        ArgumentNullException.ThrowIfNull(operationName);
        return _captured.FirstOrDefault(
            span => string.Equals(span.OperationName, operationName, StringComparison.Ordinal));
    }

    /// <summary>Returns the first captured span whose <see cref="Activity.OperationName"/> equals
    /// <paramref name="operationName"/> and that carries a tag <paramref name="tagKey"/> whose value
    /// matches <paramref name="tagValue"/>, or <see langword="null"/> if none matches. Tag values are
    /// compared by their invariant <see cref="object.ToString"/> form, matching how spans carry
    /// heterogeneously-typed tag values.</summary>
    /// <param name="operationName">The operation name to look for.</param>
    /// <param name="tagKey">The tag key to inspect.</param>
    /// <param name="tagValue">The expected tag value (compared by invariant <c>ToString</c>).</param>
    /// <returns>The matching span, or <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="operationName"/>,
    /// <paramref name="tagKey"/>, or <paramref name="tagValue"/> is <see langword="null"/>.</exception>
    public Activity? FindByOperationNameAndTag(string operationName, string tagKey, object tagValue)
    {
        ArgumentNullException.ThrowIfNull(operationName);
        ArgumentNullException.ThrowIfNull(tagKey);
        ArgumentNullException.ThrowIfNull(tagValue);
        return _captured.FirstOrDefault(
            span => string.Equals(span.OperationName, operationName, StringComparison.Ordinal)
                && TagValueEquals(span.GetTagItem(tagKey), tagValue));
    }

    /// <summary>Returns the captured spans that are direct children of <paramref name="parent"/>: a
    /// span whose <see cref="Activity.ParentSpanId"/> equals <paramref name="parent"/>'s
    /// <see cref="Activity.SpanId"/> and that shares its <see cref="Activity.TraceId"/>.</summary>
    /// <param name="parent">The parent span.</param>
    /// <returns>The direct children, in completion order (possibly empty).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parent"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<Activity> ChildrenOf(Activity parent)
    {
        ArgumentNullException.ThrowIfNull(parent);
        var children = new List<Activity>();
        foreach (var span in _captured)
        {
            if (span.ParentSpanId == parent.SpanId && span.TraceId == parent.TraceId)
            {
                children.Add(span);
            }
        }

        return children;
    }

    /// <summary>Compares a span's tag value (as returned by <see cref="Activity.GetTagItem"/>) against
    /// an expected value by their invariant <see cref="object.ToString"/> form. A
    /// <see langword="null"/> actual (absent tag) never matches.</summary>
    internal static bool TagValueEquals(object? actual, object expected)
    {
        if (actual is null)
        {
            return false;
        }

        return string.Equals(
            Convert.ToString(actual, CultureInfo.InvariantCulture),
            Convert.ToString(expected, CultureInfo.InvariantCulture),
            StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public void Dispose() => _listener.Dispose();
}
