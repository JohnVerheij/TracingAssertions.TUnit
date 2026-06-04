using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;

namespace TracingAssertions;

/// <summary>
/// Disposable, per-test capture of <see cref="Activity"/> spans emitted by an
/// <see cref="ActivitySource"/>. Starts a raw <see cref="ActivityListener"/> sampling at
/// <see cref="ActivitySamplingResult.AllDataAndRecorded"/> and collects every stopped activity
/// from the listened source. Disposing detaches the listener.
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
/// collecting and detaches when disposed. The foundation release (v0.0.1) ships single-source
/// capture and <see cref="Captured"/>; multi-source capture and span-query helpers
/// (find-by-operation-name, parent/child navigation) ship in 0.1.0.
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

    /// <summary>The spans captured so far, in completion (activity-stopped) order. Each access
    /// returns a snapshot.</summary>
    public IReadOnlyList<Activity> Captured => _captured.ToArray();

    /// <inheritdoc/>
    public void Dispose() => _listener.Dispose();
}
