using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TUnit.Assertions.Exceptions;

namespace TracingAssertions.TUnit.Tests;

/// <summary>
/// Tests for the duration assertions (exact boundaries through fixed start and end times) and for the span
/// relationship assertions with <see cref="ActivityIdFormat.Hierarchical"/> ids, where the W3C ids are default.
/// </summary>
[Category("Smoke")]
[Timeout(10_000)]
internal sealed class SpanDurationAndHierarchicalAssertionsTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static Activity StopAfter(ActivitySource source, double seconds)
    {
        var span = source.StartActivity("op", ActivityKind.Internal, default(ActivityContext), startTime: new DateTimeOffset(T0))!;
        span.SetEndTime(T0.AddSeconds(seconds));
        span.Stop();
        return span;
    }

    private static Activity StartHierarchical(ActivitySource source, string name)
    {
        var span = source.CreateActivity(name, ActivityKind.Internal)!;
        span.SetIdFormat(ActivityIdFormat.Hierarchical);
        return span.Start();
    }

    // ---- durations ----

    [Test]
    public async Task HasDurationLessThan_Boundaries(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.Duration.Max");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.Duration.Max");
        using var span = StopAfter(source, 2);

        await Assert.That(span.Duration).IsEqualTo(TimeSpan.FromSeconds(2));
        await Assert.That(span).HasDurationLessThan(TimeSpan.FromSeconds(2.001));
        await Assert.That(async () => await Assert.That(span).HasDurationLessThan(TimeSpan.FromSeconds(2)))
            .Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(span).HasDurationLessThan(TimeSpan.FromSeconds(1.999)))
            .Throws<AssertionException>();
    }

    [Test]
    public async Task HasDurationAtLeast_Boundaries(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.Duration.Min");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.Duration.Min");
        using var span = StopAfter(source, 2);

        await Assert.That(span).HasDurationAtLeast(TimeSpan.FromSeconds(2));
        await Assert.That(span).HasDurationAtLeast(TimeSpan.Zero);
        await Assert.That(async () => await Assert.That(span).HasDurationAtLeast(TimeSpan.FromSeconds(2.001)))
            .Throws<AssertionException>();
    }

    [Test]
    public async Task HasDuration_RealClockSpan_PassesBothBoundsForAGenerousRange(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.Duration.Real");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.Duration.Real");
        Activity span;
        using (var started = source.StartActivity("op"))
        {
            span = started!;
        }

        // A span that stops at once is far below a minute and not below minus anything: the bounds that hold
        // on any machine.
        await Assert.That(span).HasDurationLessThan(TimeSpan.FromMinutes(1));
        await Assert.That(span).HasDurationAtLeast(TimeSpan.Zero);
    }

    [Test]
    public async Task HasDuration_NullSpanAndNegativeBounds_Throw(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.Duration.Args");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.Duration.Args");
        using var span = StopAfter(source, 1);

        await Assert.That(() => SpanAssertions.HasDurationLessThan(null!, TimeSpan.FromSeconds(1))).Throws<ArgumentNullException>();
        await Assert.That(() => SpanAssertions.HasDurationAtLeast(null!, TimeSpan.FromSeconds(1))).Throws<ArgumentNullException>();
        await Assert.That(() => SpanAssertions.HasDurationLessThan(span, TimeSpan.FromSeconds(-1))).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => SpanAssertions.HasDurationAtLeast(span, TimeSpan.FromSeconds(-1))).Throws<ArgumentOutOfRangeException>();
    }

    // ---- hierarchical ids ----

    [Test]
    public async Task IsChildOf_HierarchicalIds_ChildPassesAndUnrelatedFails(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var noCurrent = new NoCurrentActivity();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.HierAssert.Child");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.HierAssert.Child");
        Activity parent;
        Activity child;
        Activity unrelated;

        using (var parentSpan = StartHierarchical(source, "parent"))
        {
            parent = parentSpan;
            using (var childSpan = StartHierarchical(source, "child"))
            {
                child = childSpan;
            }
        }

        using (var other = StartHierarchical(source, "other"))
        {
            unrelated = other;
        }

        await Assert.That(child).IsChildOf(parent);
        await Assert.That(async () => await Assert.That(unrelated).IsChildOf(parent)).Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(parent).IsChildOf(child)).Throws<AssertionException>();
    }

    [Test]
    public async Task IsChildOf_HierarchicalFailure_NamesTheIdsAndTheRoot(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var noCurrent = new NoCurrentActivity();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.HierAssert.ChildText");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.HierAssert.ChildText");
        using var parent = StartHierarchical(source, "parent");
        using var other = StartHierarchical(source, "other");
        // "other" is a child of "parent" (current when it started): ask for the reverse so the check fails.

        var text = await FailureText.OfAsync(async () => await Assert.That(parent).IsChildOf(other));

        await Assert.That(text).Contains("Expected to be a child of the given parent span");
        await Assert.That(text).Contains("but its parent was no parent in trace ");
        await Assert.That(text).Contains("(expected 'other' (id |");
    }

    [Test]
    public async Task SharesTraceWith_HierarchicalIds_SameRootPassesAndOtherRootFails(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var noCurrent = new NoCurrentActivity();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.HierAssert.Trace");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.HierAssert.Trace");
        Activity root;
        Activity child;
        Activity other;

        using (var rootSpan = StartHierarchical(source, "root"))
        {
            root = rootSpan;
            using (var childSpan = StartHierarchical(source, "child"))
            {
                child = childSpan;
            }
        }

        using (var otherSpan = StartHierarchical(source, "other"))
        {
            other = otherSpan;
        }

        await Assert.That(child).SharesTraceWith(root);
        var text = await FailureText.OfAsync(async () => await Assert.That(other).SharesTraceWith(root));
        await Assert.That(text).Contains("but it was in trace ");
    }

    [Test]
    public async Task IsRoot_HierarchicalIds_RootPassesAndChildFails(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var noCurrent = new NoCurrentActivity();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.HierAssert.Root");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.HierAssert.Root");
        Activity root;
        Activity child;

        using (var rootSpan = StartHierarchical(source, "root"))
        {
            root = rootSpan;
            using (var childSpan = StartHierarchical(source, "child"))
            {
                child = childSpan;
            }
        }

        await Assert.That(root).IsRoot();
        var text = await FailureText.OfAsync(async () => await Assert.That(child).IsRoot());
        await Assert.That(text).Contains("but its parent was 'root' (id |");
    }

    [Test]
    public async Task Relations_HierarchicalAgainstW3C_NeverMatch(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.HierAssert.Mixed");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.HierAssert.Mixed");
        using var w3c = source.StartActivity("w3c");
        using var hierarchical = StartHierarchical(source, "hier");

        await Assert.That(async () => await Assert.That(hierarchical).IsChildOf(w3c!)).Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(w3c!).IsChildOf(hierarchical)).Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(hierarchical).SharesTraceWith(w3c!)).Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(w3c!).SharesTraceWith(hierarchical)).Throws<AssertionException>();
    }

    [Test]
    public async Task W3CIds_ResultsAreUnchanged(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.HierAssert.W3C");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.HierAssert.W3C");
        Activity parent;
        Activity child;
        using (var parentSpan = source.StartActivity("parent"))
        {
            parent = parentSpan!;
            using (var childSpan = source.StartActivity("child"))
            {
                child = childSpan!;
            }
        }

        await Assert.That(child).IsChildOf(parent);
        await Assert.That(child).SharesTraceWith(parent);
        await Assert.That(async () => await Assert.That(child).IsRoot()).Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(parent).IsChildOf(child)).Throws<AssertionException>();
    }

    [Test]
    public async Task SharesTraceWith_HierarchicalSpansThatHaveNotStarted_HaveNoRootAndNeverMatch(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var first = new Activity("first");
        first.SetIdFormat(ActivityIdFormat.Hierarchical);
        using var second = new Activity("second");
        second.SetIdFormat(ActivityIdFormat.Hierarchical);

        // An unstarted activity has no id yet, so no root id: two of them are not in one trace.
        await Assert.That(first.RootId).IsNull();
        var text = await FailureText.OfAsync(async () => await Assert.That(first).SharesTraceWith(second));

        await Assert.That(text).IsEqualTo(
            "Expected to share a trace with the given span" + (char)10 + "but it was in trace none (expected trace none)");
    }
}
