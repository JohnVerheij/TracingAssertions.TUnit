using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace TracingAssertions.TUnit.Tests;

/// <summary>
/// Pins the complete failure text of every span and capture assertion: TUnit prints <c>Expected</c> followed by the
/// assertion's expectation, then <c>but</c> and the text the assertion returns. Ids are replaced by
/// <c>&lt;span&gt;</c> and <c>&lt;trace&gt;</c>.
/// </summary>
[Category("Smoke")]
[Timeout(10_000)]
internal sealed class SpanFailureMessageTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static ActivityContext NewRootContext() =>
        new(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);

    /// <summary>Starts a span and stops it after exactly <paramref name="seconds"/> of recorded time.</summary>
    private static Activity StopAfter(ActivitySource source, string name, int seconds)
    {
        var span = source.StartActivity(name, ActivityKind.Internal, default(ActivityContext), startTime: new DateTimeOffset(T0))!;
        span.SetEndTime(T0.AddSeconds(seconds));
        span.Stop();
        return span;
    }

    [Test]
    public async Task HasOperationName_Mismatch_Text(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.Msg.OpName");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.Msg.OpName");
        using var span = source.StartActivity("actual.op");

        var text = await FailureText.OfAsync(async () => await Assert.That(span!).HasOperationName("expected.op"));

        await Assert.That(text).IsEqualTo("Expected to have operation name 'expected.op'\nbut it was 'actual.op'");
    }

    [Test]
    public async Task HasTag_Absent_Text(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.Msg.Tag");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.Msg.Tag");
        using var span = source.StartActivity("op");

        var text = await FailureText.OfAsync(async () => await Assert.That(span!).HasTag("missing"));

        await Assert.That(text).IsEqualTo("Expected to have a tag 'missing'\nbut it had no such tag");
    }

    [Test]
    public async Task HasTagValue_Mismatch_Text(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.Msg.TagValue");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.Msg.TagValue");
        using var span = source.StartActivity("op");
        span!.SetTag("process.id", 1);

        var text = await FailureText.OfAsync(async () => await Assert.That(span).HasTagValue("process.id", 0));

        await Assert.That(text).IsEqualTo("Expected to have tag 'process.id' with the expected value\nbut it was '1' (expected '0')");
    }

    [Test]
    public async Task HasTagValue_Absent_Text(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.Msg.TagValueAbsent");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.Msg.TagValueAbsent");
        using var span = source.StartActivity("op");

        var text = await FailureText.OfAsync(async () => await Assert.That(span!).HasTagValue("process.id", "0"));

        await Assert.That(text).IsEqualTo("Expected to have tag 'process.id' with the expected value\nbut the tag was absent (expected '0')");
    }

    [Test]
    public async Task HasStatus_Mismatch_Text(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.Msg.Status");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.Msg.Status");
        using var span = source.StartActivity("op");
        span!.SetStatus(ActivityStatusCode.Ok);

        var text = await FailureText.OfAsync(async () => await Assert.That(span).HasStatus(ActivityStatusCode.Error));

        await Assert.That(text).IsEqualTo("Expected to have status Error\nbut it was Ok");
    }

    [Test]
    public async Task HasKind_Mismatch_Text(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.Msg.Kind");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.Msg.Kind");
        using var span = source.StartActivity("op", ActivityKind.Server);

        var text = await FailureText.OfAsync(async () => await Assert.That(span!).HasKind(ActivityKind.Client));

        await Assert.That(text).IsEqualTo("Expected to have kind Client\nbut it was Server");
    }

    [Test]
    public async Task IsChildOf_RemoteParent_Text(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.Msg.ChildRemote");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.Msg.ChildRemote");
        using var expectedParent = source.StartActivity("expected.parent", ActivityKind.Internal, NewRootContext());
        using var span = source.StartActivity("span", ActivityKind.Internal, NewRootContext());

        var text = await FailureText.OfAsync(async () => await Assert.That(span!).IsChildOf(expectedParent!));

        await Assert.That(text).IsEqualTo(
            "Expected to be a child of the given parent span\n"
            + "but its parent was span <span> in trace <trace> (expected 'expected.parent' (span <span>) in trace <trace>)");
    }

    [Test]
    public async Task IsChildOf_KnownParent_NamesTheParent(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.Msg.ChildKnown");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.Msg.ChildKnown");
        using var expectedParent = source.StartActivity("expected.parent", ActivityKind.Internal, NewRootContext());
        using var actualParent = source.StartActivity("actual.parent", ActivityKind.Internal, NewRootContext());
        using var span = source.StartActivity("span");

        var text = await FailureText.OfAsync(async () => await Assert.That(span!).IsChildOf(expectedParent!));

        await Assert.That(text).IsEqualTo(
            "Expected to be a child of the given parent span\n"
            + "but its parent was 'actual.parent' (span <span>) in trace <trace> (expected 'expected.parent' (span <span>) in trace <trace>)");
    }

    [Test]
    public async Task IsChildOf_RootSpan_SaysNoParent(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.Msg.ChildRoot");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.Msg.ChildRoot");
        var root = StartDetachedRoot(source, "root");
        var other = StartDetachedRoot(source, "other");

        var text = await FailureText.OfAsync(async () => await Assert.That(root).IsChildOf(other));

        other.Dispose();
        root.Dispose();
        await Assert.That(text).IsEqualTo(
            "Expected to be a child of the given parent span\n"
            + "but its parent was no parent in trace <trace> (expected 'other' (span <span>) in trace <trace>)");
    }

    /// <summary>Starts a true root span: inside a TUnit test the current activity is the test's own, so it is
    /// cleared for the start and restored afterwards.</summary>
    private static Activity StartDetachedRoot(ActivitySource source, string name)
    {
        var previous = Activity.Current;
        Activity.Current = null;
        try
        {
            return source.StartActivity(name, ActivityKind.Internal)!;
        }
        finally
        {
            Activity.Current = previous;
        }
    }

    [Test]
    public async Task SharesTraceWith_DifferentTrace_Text(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.Msg.Trace");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.Msg.Trace");
        using var a = source.StartActivity("a", ActivityKind.Internal, NewRootContext());
        using var b = source.StartActivity("b", ActivityKind.Internal, NewRootContext());

        var text = await FailureText.OfAsync(async () => await Assert.That(b!).SharesTraceWith(a!));

        await Assert.That(text).IsEqualTo(
            "Expected to share a trace with the given span\nbut it was in trace <trace> (expected trace <trace>)");
    }

    [Test]
    public async Task IsRoot_WithRemoteParent_Text(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.Msg.Root");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.Msg.Root");
        using var span = source.StartActivity("span", ActivityKind.Internal, NewRootContext());

        var text = await FailureText.OfAsync(async () => await Assert.That(span!).IsRoot());

        await Assert.That(text).IsEqualTo("Expected to be a root span (no parent)\nbut its parent was span <span>");
    }

    [Test]
    public async Task IsRoot_WithKnownParent_NamesTheParent(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.Msg.RootKnown");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.Msg.RootKnown");
        using var parent = source.StartActivity("the.parent", ActivityKind.Internal, NewRootContext());
        using var span = source.StartActivity("span");

        var text = await FailureText.OfAsync(async () => await Assert.That(span!).IsRoot());

        await Assert.That(text).IsEqualTo("Expected to be a root span (no parent)\nbut its parent was 'the.parent' (span <span>)");
    }

    [Test]
    public async Task HasEvent_Absent_Text(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.Msg.Event");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.Msg.Event");
        using var span = source.StartActivity("op");
        span!.AddEvent(new ActivityEvent("cache.hit"));
        span.AddEvent(new ActivityEvent("db.query"));

        var text = await FailureText.OfAsync(async () => await Assert.That(span).HasEvent("cache.miss"));

        await Assert.That(text).IsEqualTo(
            "Expected to have an event named 'cache.miss'\nbut it had events: 'cache.hit', 'db.query'");
    }

    [Test]
    public async Task HasEvent_NoEvents_Text(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.Msg.EventNone");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.Msg.EventNone");
        using var span = source.StartActivity("op");

        var text = await FailureText.OfAsync(async () => await Assert.That(span!).HasEvent("cache.miss"));

        await Assert.That(text).IsEqualTo("Expected to have an event named 'cache.miss'\nbut it had no events");
    }

    [Test]
    public async Task HasExceptionEvent_Absent_Text(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.Msg.ExceptionEvent");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.Msg.ExceptionEvent");
        using var span = source.StartActivity("op");
        span!.AddEvent(new ActivityEvent("retry"));

        var text = await FailureText.OfAsync(async () => await Assert.That(span).HasExceptionEvent());

        await Assert.That(text).IsEqualTo("Expected to have an exception event\nbut it had events: 'retry'");
    }

    [Test]
    public async Task HasDurationLessThan_TooLong_Text(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.Msg.DurationMax");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.Msg.DurationMax");
        using var span = StopAfter(source, "op", 2);

        var text = await FailureText.OfAsync(async () => await Assert.That(span).HasDurationLessThan(TimeSpan.FromSeconds(1)));

        await Assert.That(text).IsEqualTo("Expected to have a duration less than 00:00:01\nbut it ran for 00:00:02");
    }

    [Test]
    public async Task HasDurationAtLeast_TooShort_Text(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.Msg.DurationMin");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.Msg.DurationMin");
        using var span = StopAfter(source, "op", 2);

        var text = await FailureText.OfAsync(async () => await Assert.That(span).HasDurationAtLeast(TimeSpan.FromSeconds(3)));

        await Assert.That(text).IsEqualTo("Expected to have a duration of at least 00:00:03\nbut it ran for 00:00:02");
    }

    [Test]
    public async Task HasDuration_RunningSpan_Text(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.Msg.DurationRunning");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.Msg.DurationRunning");
        using var span = source.StartActivity("op");

        var lessThan = await FailureText.OfAsync(async () => await Assert.That(span!).HasDurationLessThan(TimeSpan.FromHours(1)));
        var atLeast = await FailureText.OfAsync(async () => await Assert.That(span!).HasDurationAtLeast(TimeSpan.Zero));

        await Assert.That(lessThan).IsEqualTo(
            "Expected to have a duration less than 01:00:00\nbut the span had not stopped, so it has no duration yet");
        await Assert.That(atLeast).IsEqualTo(
            "Expected to have a duration of at least 00:00:00\nbut the span had not stopped, so it has no duration yet");
    }

    [Test]
    public async Task HasSpan_Absent_ShowsTheTree(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.Msg.HasSpan");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.Msg.HasSpan");
        using (var root = source.StartActivity("root", ActivityKind.Server))
        {
            using (source.StartActivity("child", ActivityKind.Client))
            {
                // The span stops, and is captured, when the scope closes.
            }
        }

        var text = await FailureText.OfAsync(async () => await Assert.That(capture).HasSpan("order.pipeline"));

        await Assert.That(text).IsEqualTo(
            "Expected to include a span named 'order.pipeline'\n"
            + "but none did; captured spans:\n"
            + "  root [Server, Unset]\n"
            + "    child [Client, Unset]");
    }

    [Test]
    public async Task HasSpan_EmptyCapture_Text(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.Msg.HasSpanEmpty");

        var text = await FailureText.OfAsync(async () => await Assert.That(capture).HasSpan("order.pipeline"));

        await Assert.That(text).IsEqualTo(
            "Expected to include a span named 'order.pipeline'\nbut none did; no spans were captured");
    }

    [Test]
    public async Task HasNoSpan_Present_ShowsTheTree(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.Msg.HasNoSpan");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.Msg.HasNoSpan");
        using (source.StartActivity("order.pipeline"))
        {
            // The span stops, and is captured, when the scope closes.
        }

        var text = await FailureText.OfAsync(async () => await Assert.That(capture).HasNoSpan("order.pipeline"));

        await Assert.That(text).IsEqualTo(
            "Expected to include no span named 'order.pipeline'\n"
            + "but at least one did; captured spans:\n"
            + "  order.pipeline [Internal, Unset]");
    }

    [Test]
    public async Task HasSpanCount_Mismatch_ShowsTheTree(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.Msg.Count");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.Msg.Count");
        using (source.StartActivity("one"))
        {
            // The span stops, and is captured, when the scope closes.
        }

        var text = await FailureText.OfAsync(async () => await Assert.That(capture).HasSpanCount(3));

        await Assert.That(text).IsEqualTo(
            "Expected to contain 3 span(s)\nbut it contained 1; captured spans:\n  one [Internal, Unset]");
    }

    [Test]
    public async Task HasSpanWithTag_NoMatch_ShowsTheTreeWithTheTag(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.Msg.Lookup");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.Msg.Lookup");
        using (var root = source.StartActivity("cycle"))
        {
            root!.SetTag("cycle.guid", "abc");
            using (var child = source.StartActivity("cycle.step"))
            {
                child!.SetTag("cycle.guid", 7);
            }
        }

        var text = await FailureText.OfAsync(async () => await Assert.That(capture).HasSpanWithTag("cycle", "cycle.guid", "xyz"));

        await Assert.That(text).IsEqualTo(
            "Expected to include a span named 'cycle' with tag 'cycle.guid' with the expected value\n"
            + "but none did (the value looked for was 'xyz'); captured spans:\n"
            + "  cycle [Internal, Unset] {cycle.guid=abc}\n"
            + "    cycle.step [Internal, Unset] {cycle.guid=7}");
    }

    [Test]
    public async Task HasSpanWithTag_EmptyCapture_Text(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.Msg.LookupEmpty");

        var text = await FailureText.OfAsync(async () => await Assert.That(capture).HasSpanWithTag("cycle", "k", 1.5));

        await Assert.That(text).IsEqualTo(
            "Expected to include a span named 'cycle' with tag 'k' with the expected value\n"
            + "but none did (the value looked for was '1.5'); no spans were captured");
    }

    [Test]
    public async Task HasChildSpan_Missing_Text(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.Msg.HasChild");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.Msg.HasChild");
        Activity root;
        using (var rootSpan = source.StartActivity("root"))
        {
            root = rootSpan!;
            using (source.StartActivity("a"))
            {
                // The span stops, and is captured, when the scope closes.
            }

            using (source.StartActivity("b"))
            {
                // The span stops, and is captured, when the scope closes.
            }
        }

        var text = await FailureText.OfAsync(async () => await Assert.That(root).HasChildSpan(capture, "c"));

        await Assert.That(text).IsEqualTo(
            "Expected to have a child span named 'c'\n"
            + "but it had child spans 'a', 'b'; captured spans:\n"
            + "  root [Internal, Unset]\n"
            + "    a [Internal, Unset]\n"
            + "    b [Internal, Unset]");
    }

    [Test]
    public async Task HasChildSpanCount_Mismatch_Text(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.Msg.ChildCount");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.Msg.ChildCount");
        Activity root;
        using (var rootSpan = source.StartActivity("root"))
        {
            root = rootSpan!;
        }

        var text = await FailureText.OfAsync(async () => await Assert.That(root).HasChildSpanCount(capture, 2));

        await Assert.That(text).IsEqualTo(
            "Expected to have 2 child span(s)\nbut it had no child spans; captured spans:\n  root [Internal, Unset]");
    }
}
