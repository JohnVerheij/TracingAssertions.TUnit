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
/// <see cref="ActivitySamplingResult.AllDataAndRecorded"/> by default (the <c>ForSource</c> and
/// <c>ForSources</c> overloads accept another <see cref="ActivitySamplingResult"/>) and collects every
/// stopped activity from the listened source(s). Disposing detaches the listener.
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
/// <para>
/// The listener is process-wide: while the capture is active, it also collects the spans that other
/// code, including tests running in parallel, emits on the listened sources. To keep such tests
/// independent, serialize the tests that share a source, or start a root <see cref="Activity"/> in the
/// test and query its children with <see cref="ChildrenOf"/>.
/// </para>
/// </remarks>
public sealed class SpanCapture : IDisposable
{
    private readonly ActivityListener _listener;
    private readonly ConcurrentQueue<Activity> _captured = new();

    private SpanCapture(Func<ActivitySource, bool> shouldListenTo, ActivitySamplingResult sampling)
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = shouldListenTo,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => sampling,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => sampling,
            ActivityStopped = activity => _captured.Enqueue(activity),
        };
        ActivitySource.AddActivityListener(_listener);
    }

    /// <summary>Starts capturing spans from every <see cref="ActivitySource"/> whose
    /// <see cref="ActivitySource.Name"/> equals <paramref name="sourceName"/> (ordinal), sampling at
    /// <see cref="ActivitySamplingResult.AllDataAndRecorded"/>.</summary>
    /// <param name="sourceName">The activity-source name to listen to.</param>
    /// <returns>A disposable capture; dispose it (ideally via <see langword="using"/>) to detach the
    /// listener at the end of the test.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sourceName"/> is <see langword="null"/>.</exception>
    public static SpanCapture ForSource(string sourceName)
        => ForSource(sourceName, ActivitySamplingResult.AllDataAndRecorded);

    /// <summary>Starts capturing spans from every <see cref="ActivitySource"/> whose
    /// <see cref="ActivitySource.Name"/> equals <paramref name="sourceName"/> (ordinal), at the given
    /// <paramref name="sampling"/> result.</summary>
    /// <remarks>The default <see cref="ForSource(string)"/> forces
    /// <see cref="ActivitySamplingResult.AllDataAndRecorded"/>, so code that branches on
    /// <see cref="Activity.IsAllDataRequested"/> always takes the recorded path under test. Pass a
    /// lower result (for example <see cref="ActivitySamplingResult.PropagationData"/>) to exercise the
    /// not-fully-sampled branch. Note that an activity's effective sampling is the maximum requested by
    /// any active listener, so this lower result only takes effect when no other listener requests a
    /// higher one; a co-existing recording listener (for example an OpenTelemetry SDK pipeline) still
    /// raises the activity to fully recorded. <see cref="ActivitySamplingResult.None"/> creates no
    /// activity, so nothing is captured unless another listener samples it. With TUnit's HTML report on,
    /// a result below <see cref="ActivitySamplingResult.PropagationData"/> is raised to it; higher
    /// results apply to spans outside the test's trace (the report records spans in a known test trace
    /// anyway).</remarks>
    /// <param name="sourceName">The activity-source name to listen to.</param>
    /// <param name="sampling">The sampling result the listener returns for every activity.
    /// <see cref="ActivitySamplingResult.None"/> creates no activity, so nothing is captured unless
    /// another listener samples it.</param>
    /// <returns>A disposable capture; dispose it (ideally via <see langword="using"/>) to detach the
    /// listener at the end of the test.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sourceName"/> is <see langword="null"/>.</exception>
    public static SpanCapture ForSource(string sourceName, ActivitySamplingResult sampling)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        return new SpanCapture(source => string.Equals(source.Name, sourceName, StringComparison.Ordinal), sampling);
    }

    /// <summary>Starts capturing spans from any <see cref="ActivitySource"/> whose
    /// <see cref="ActivitySource.Name"/> is one of <paramref name="sourceNames"/> (ordinal), sampling
    /// at <see cref="ActivitySamplingResult.AllDataAndRecorded"/>.</summary>
    /// <param name="sourceNames">The activity-source names to listen to. An empty array captures
    /// nothing.</param>
    /// <returns>A disposable capture; dispose it (ideally via <see langword="using"/>) to detach the
    /// listener at the end of the test.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sourceNames"/> is
    /// <see langword="null"/>.</exception>
    public static SpanCapture ForSources(params string[] sourceNames)
        => ForSources(ActivitySamplingResult.AllDataAndRecorded, sourceNames);

    /// <summary>Starts capturing spans from any <see cref="ActivitySource"/> whose
    /// <see cref="ActivitySource.Name"/> is one of <paramref name="sourceNames"/> (ordinal), at the
    /// given <paramref name="sampling"/> result (see <see cref="ForSource(string, ActivitySamplingResult)"/>
    /// for why this matters).</summary>
    /// <param name="sampling">The sampling result the listener returns for every activity.
    /// <see cref="ActivitySamplingResult.None"/> creates no activity, so nothing is captured unless
    /// another listener samples it.</param>
    /// <param name="sourceNames">The activity-source names to listen to. An empty array captures
    /// nothing.</param>
    /// <returns>A disposable capture; dispose it (ideally via <see langword="using"/>) to detach the
    /// listener at the end of the test.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sourceNames"/> is
    /// <see langword="null"/>.</exception>
    public static SpanCapture ForSources(ActivitySamplingResult sampling, params string[] sourceNames)
    {
        ArgumentNullException.ThrowIfNull(sourceNames);
        var set = new HashSet<string>(sourceNames, StringComparer.Ordinal);
        return new SpanCapture(source => set.Contains(source.Name), sampling);
    }

    /// <summary>The spans captured so far, in completion (activity-stopped) order. Each access
    /// returns a snapshot.</summary>
    public IReadOnlyList<Activity> Captured => _captured.ToArray();

    /// <summary>The number of spans captured so far, read without copying the capture the way <see cref="Captured"/>
    /// does; the count assertions use it so that a large capture stays cheap to assert on.</summary>
    internal int CapturedCount => _captured.Count;

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

    /// <summary>Returns the captured spans that are direct children of <paramref name="parent"/>, found by the
    /// relationship rules of the span ids in use (see the remarks).</summary>
    /// <remarks>With W3C activity ids (the .NET default) a child is a span whose
    /// <see cref="Activity.ParentSpanId"/> equals <paramref name="parent"/>'s <see cref="Activity.SpanId"/> and that
    /// shares its <see cref="Activity.TraceId"/>. With <see cref="ActivityIdFormat.Hierarchical"/> ids, where those
    /// are all default, a child is a span whose <see cref="Activity.ParentId"/> equals the parent's
    /// <see cref="Activity.Id"/>. A hierarchical span is never the child of a W3C span or the reverse.</remarks>
    /// <param name="parent">The parent span.</param>
    /// <returns>The direct children, in completion order (possibly empty).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parent"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<Activity> ChildrenOf(Activity parent)
    {
        ArgumentNullException.ThrowIfNull(parent);
        return ChildrenIn(_captured, parent);
    }

    /// <summary>Selects the direct children of <paramref name="parent"/> among <paramref name="spans"/>, by the
    /// relationship rules of <see cref="ChildrenOf(Activity)"/>. A caller that holds one snapshot of the capture
    /// uses this to derive the children and the rest of a message from that same snapshot.</summary>
    /// <param name="spans">The spans to search.</param>
    /// <param name="parent">The parent span.</param>
    /// <returns>The direct children in the order of <paramref name="spans"/>.</returns>
    internal static IReadOnlyList<Activity> ChildrenIn(IEnumerable<Activity> spans, Activity parent)
        => spans.Where(span => SpanRelations.IsChildOf(span, parent)).ToArray();

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
