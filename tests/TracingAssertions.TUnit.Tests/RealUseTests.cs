using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TUnit.Assertions.Exceptions;

namespace TracingAssertions.TUnit.Tests;

/// <summary>
/// The real-use pass: the package exercised where tests call it (parallel tests on one source, other threads, spans
/// that never stop, sampling results, nested and overlapping captures, retries, large captures, odd names) with the
/// assertion chain the way a consumer writes it.
/// </summary>
[Category("Smoke")]
[Timeout(30_000)]
internal sealed class RealUseTests
{
    private const string SharedSource = "TracingAssertions.TUnit.Tests.RealUse.Shared";

    /// <summary>One source every parallel test below emits on; the capture of each test sees the others' spans.</summary>
    private static readonly ActivitySource Shared = new(SharedSource);

    private static ActivityContext NewRootContext() =>
        new(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None);

    // ---- parallel tests on one source ----

    [Test]
    [Repeat(8)]
    public async Task ParallelTests_ScopedToATestOwnedRoot_SeeOnlyTheirOwnChildren(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var capture = SpanCapture.ForSource(SharedSource);
        Activity root;
        var mine = 3 + (Environment.CurrentManagedThreadId % 3);
        using (var rootSpan = Shared.StartActivity("work.root", ActivityKind.Internal, default(ActivityContext)))
        {
            root = rootSpan!;
            for (var i = 0; i < mine; i++)
            {
                using (Shared.StartActivity("work.child"))
                {
                    await Task.Yield();
                }
            }
        }

        // Scoped to the root the test owns, the count is exact whatever the other tests emit.
        await Assert.That(root).HasChildSpanCount(capture, mine);
        await Assert.That(capture.ChildrenOf(root).All(span => string.Equals(span.OperationName, "work.child", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task SpansStartedOnOtherThreadsAndAfterAwait_AreCaptured(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.RealUse.Threads");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.RealUse.Threads");
        Activity root;
        using (var rootSpan = source.StartActivity("root"))
        {
            root = rootSpan!;
            await Task.Run(() =>
            {
                using (source.StartActivity("on.pool.thread"))
                {
                    // The span stops, and is captured, when the scope closes.
                }
            }, cancellationToken);
            await Task.Delay(10, cancellationToken);
            using (source.StartActivity("after.await"))
            {
                // The span stops, and is captured, when the scope closes.
            }
        }

        // Activity.Current flows through Task.Run and await, so both are children of the root.
        await Assert.That(root).HasChildSpan(capture, "on.pool.thread");
        await Assert.That(root).HasChildSpan(capture, "after.await");
    }

    [Test]
    public async Task ManySpansFromManyThreads_AreAllCaptured(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.RealUse.Many");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.RealUse.Many");

        await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 500; i++)
            {
                using (source.StartActivity("burst", ActivityKind.Internal, default(ActivityContext)))
                {
                    // The span stops, and is captured, when the scope closes.
                }
            }
        }, cancellationToken)));

        await Assert.That(capture).HasSpanCount(8000);
    }

    // ---- spans that do not complete ----

    [Test]
    public async Task ARunningSpan_IsNotCaptured_AndHasNoDuration(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.RealUse.Running");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.RealUse.Running");
        using var running = source.StartActivity("still.running");

        await Assert.That(capture).HasSpanCount(0);
        await Assert.That(capture).HasNoSpan("still.running");
        await Assert.That(async () => await Assert.That(running!).HasDurationLessThan(TimeSpan.FromHours(1)))
            .Throws<AssertionException>();

        running!.Stop();

        await Assert.That(capture).HasSpan("still.running");
        await Assert.That(running).HasDurationLessThan(TimeSpan.FromHours(1));
    }

    // ---- sampling ----

    [Test]
    public async Task SamplingNone_CreatesNoSpanOfItsOwn(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var control = new ActivitySource("TracingAssertions.TUnit.Tests.RealUse.SamplingNoneControl");
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.RealUse.SamplingNone");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.RealUse.SamplingNone", ActivitySamplingResult.None);

        // The control source has no capture: it only shows whether another listener (TUnit's own, while its HTML
        // report is on) samples spans in this test, and so would create one whatever the capture asks for.
        var otherListenerSamples = control.StartActivity("op");
        otherListenerSamples?.Dispose();
        var span = source.StartActivity("op");
        span?.Dispose();

        // The capture adds nothing: a span exists only if another listener created it, and then the capture sees
        // exactly that one span; with no other listener, no span exists and nothing is captured.
        await Assert.That(span is null).IsEqualTo(otherListenerSamples is null);
        await Assert.That(capture).HasSpanCount(otherListenerSamples is null ? 0 : 1);
    }

    [Test]
    public async Task SamplingPropagationData_CapturesTheSpanWithoutRecordedData(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.RealUse.SamplingPropagation");
        using var capture = SpanCapture.ForSource(
            "TracingAssertions.TUnit.Tests.RealUse.SamplingPropagation", ActivitySamplingResult.PropagationData);

        Activity span;
        using (var started = source.StartActivity("op", ActivityKind.Internal, NewRootContext()))
        {
            span = started!;
        }

        await Assert.That(capture).HasSpan("op");
        await Assert.That(span.IsAllDataRequested).IsFalse();
    }

    // ---- nested and overlapping captures ----

    [Test]
    public async Task NestedCapturesOfOneSource_BothSeeTheSpans_AndInnerDisposeLeavesTheOuter(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.RealUse.Nested");
        using var outer = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.RealUse.Nested");
        var inner = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.RealUse.Nested");

        using (source.StartActivity("one"))
        {
            // The span stops, and is captured, when the scope closes.
        }

        inner.Dispose();
        using (source.StartActivity("two"))
        {
            // The span stops, and is captured, when the scope closes.
        }

        await Assert.That(outer).HasSpanCount(2);
        await Assert.That(inner).HasSpanCount(1);
    }

    [Test]
    public async Task DisposedCapture_StopsCollecting_AndDoubleDisposeIsHarmless(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.RealUse.Dispose");
        var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.RealUse.Dispose");
        var inFlight = source.StartActivity("in.flight");

        capture.Dispose();
        capture.Dispose();
        inFlight!.Stop();

        // A span still running when the capture is disposed is not captured when it stops later.
        await Assert.That(capture).HasSpanCount(0);
    }

    // ---- retries ----

    [Test]
    [Retry(2)]
    public async Task Retry_EachAttemptStartsWithAFreshCapture(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var attempt = TestContext.Current!.Execution.CurrentRetryAttempt;
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.RealUse.Retry");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.RealUse.Retry");
        using (source.StartActivity("attempt"))
        {
            // The span stops, and is captured, when the scope closes.
        }

        // The first attempt's span is gone with its capture: every attempt sees exactly its own span.
        await Assert.That(capture).HasSpanCount(1);
        if (attempt == 0)
        {
            throw new InvalidOperationException("first attempt fails on purpose, to force a retry");
        }

        await Assert.That(attempt).IsEqualTo(1);
    }

    // ---- large captures ----

    // The allocation counter below is process-wide, so no other test may allocate while it is read: this one runs alone.
    [Test]
    [NotInParallel]
    public async Task LargeCapture_CountAssertionsDoNotCopyTheCapture(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.RealUse.Large");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.RealUse.Large");
        for (var i = 0; i < 20_000; i++)
        {
            using (source.StartActivity("span", ActivityKind.Internal, default(ActivityContext)))
            {
                // The span stops, and is captured, when the scope closes.
            }
        }

        // Warm up, so one-time allocations of the first call do not count.
        await Assert.That(capture).HasSpanCount(20_000);

        // An awaited assertion can resume on another thread, so a per-thread counter would compare two threads: the
        // process-wide one is read instead (precise, so it includes the allocations of every thread).
        var before = GC.GetTotalAllocatedBytes(precise: true);
        for (var i = 0; i < 200; i++)
        {
            await Assert.That(capture).HasSpanCount(20_000);
        }

        var allocated = GC.GetTotalAllocatedBytes(precise: true) - before;

        // 200 count assertions over 20,000 spans must not copy the capture each time: a copy is 160,000 bytes, so
        // 200 copies are 32 MB (measured: 32.2 MB), where the assertions themselves need well under 1 KB each
        // (measured: 0.14 MB for all 200). The bound sits between the two, with a margin of 28 and 8.
        await Assert.That(allocated).IsLessThan(4_000_000);
    }

    // ---- failure texts while spans are still being captured ----

    [Test]
    public async Task HasSpanCount_FailureTextDuringACapture_CountsTheSpansItListsAndNoOthers(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.RealUse.FailureCount");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.RealUse.FailureCount");
        var producer = Task.Run(() =>
        {
            for (var i = 0; i < 20_000; i++)
            {
                using (source.StartActivity("burst", ActivityKind.Internal, default(ActivityContext)))
                {
                    // The span stops, and is captured, when the scope closes.
                }
            }
        }, cancellationToken);

        // The assertion is called directly: the loop must be fast enough to run while the producer is still adding.
        var mismatches = 0;
        do
        {
            var message = SpanCaptureAssertions.HasSpanCount(capture, int.MaxValue).Message.ReplaceLineEndings("\n");
            var lines = message.Split('\n');
            var counted = int.Parse(
                message["it contained ".Length..message.IndexOf(';', StringComparison.Ordinal)], CultureInfo.InvariantCulture);
            var listed = message.Contains("captured spans:", StringComparison.Ordinal) ? lines.Length - 1 : 0;
            mismatches += counted == listed ? 0 : 1;
        }
        while (!producer.IsCompleted);

        await producer;

        // Whatever the producer added meanwhile, the count in the text is the number of spans the tree lists.
        await Assert.That(mismatches).IsEqualTo(0);
    }

    [Test]
    public async Task HasChildSpanCount_FailureTextDuringACapture_ListsTheChildrenOfTheSameSnapshotAsTheTree(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.RealUse.FailureChildren");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.RealUse.FailureChildren");
        using var parent = source.StartActivity("parent", ActivityKind.Internal, default(ActivityContext))!;
        var parentContext = parent.Context;
        var producer = Task.Run(() =>
        {
            for (var i = 0; i < 20_000; i++)
            {
                using (source.StartActivity("child", ActivityKind.Internal, parentContext))
                {
                    // The span stops, and is captured, when the scope closes.
                }
            }
        }, cancellationToken);

        var mismatches = 0;
        do
        {
            var message = SpanAssertions.HasChildSpanCount(parent, capture, int.MaxValue).Message.ReplaceLineEndings("\n");
            var lines = message.Split('\n');
            var named = lines[0].Split("'child'", StringSplitOptions.None).Length - 1;
            var listed = message.Contains("captured spans:", StringComparison.Ordinal) ? lines.Length - 1 : 0;

            // The parent is still running, so it is not captured: every captured span is a child and is a line of the tree.
            mismatches += named == listed ? 0 : 1;
        }
        while (!producer.IsCompleted);

        await producer;

        await Assert.That(mismatches).IsEqualTo(0);
    }

    // ---- odd names and values ----

    [Test]
    public async Task NamesAndValuesWithLineBreaks_StayOnOneLineInTheTree(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.RealUse.LineBreaks");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.RealUse.LineBreaks");
        using (var span = source.StartActivity("first\nsecond [Server, Ok]\r\nthird"))
        {
            span!.SetTag("k", "a\nb");
        }

        var text = SpanTreeRenderer.Render(capture, new SpanTreeOptions { TagKeys = ["k"] });

        // One span is one line: a line break inside a name or value cannot forge another span line.
        await Assert.That(text.Split('\n').Length).IsEqualTo(1);
        await Assert.That(text).IsEqualTo("first\\nsecond [Server, Ok]\\r\\nthird [Internal, Unset] {k=a\\nb}");
    }

    [Test]
    public async Task EmptyOperationName_IsCapturedAndRendered(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.RealUse.EmptyName");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.RealUse.EmptyName");
        using (source.StartActivity(string.Empty))
        {
            // The span stops, and is captured, when the scope closes.
        }

        await Assert.That(capture).HasSpan(string.Empty);
        await Assert.That(SpanTreeRenderer.Render(capture)).IsEqualTo(" [Internal, Unset]");
    }
}
