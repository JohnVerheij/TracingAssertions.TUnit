using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TUnit.Assertions.Exceptions;

namespace TracingAssertions.TUnit.Tests;

/// <summary>
/// Tests for the TUnit adapter's span assertions generated over <see cref="Activity"/>:
/// <c>HasOperationName</c>, <c>HasTag</c> (existence and value), <c>HasStatus</c>, <c>IsChildOf</c>,
/// and <c>SharesTraceWith</c>.
/// </summary>
[Category("Smoke")]
[Timeout(10_000)]
internal sealed class SpanAssertionsTests
{
    /// <summary>A fresh, valid parent context with its own random trace id, so a started span becomes
    /// a root in a distinct trace regardless of any ambient <see cref="Activity.Current"/>.</summary>
    private static ActivityContext NewRootContext() =>
        new(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);

    // ---- HasOperationName ----

    [Test]
    public async Task HasOperationName_Matches_Passes(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.OpPass");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.OpPass");
        using var span = source.StartActivity("pick.pipeline");

        await Assert.That(span!).HasOperationName("pick.pipeline");
    }

    [Test]
    public async Task HasOperationName_Mismatch_Fails(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.OpFail");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.OpFail");
        using var span = source.StartActivity("actual.op");

        var exception = await Assert.That(async () =>
        {
            await Assert.That(span!).HasOperationName("expected.op");
        }).Throws<AssertionException>();

        await Assert.That(exception!.Message).Contains("expected.op");
        await Assert.That(exception.Message).Contains("actual.op");
    }

    [Test]
    public async Task HasOperationName_NullSpan_Throws(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Assert.That(() => SpanAssertions.HasOperationName(null!, "op")).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task HasOperationName_NullOperationName_Throws(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.OpNull");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.OpNull");
        using var span = source.StartActivity("op");

        await Assert.That(() => SpanAssertions.HasOperationName(span!, null!)).Throws<ArgumentNullException>();
    }

    // ---- HasTag ----

    [Test]
    public async Task HasTag_Present_Passes(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.TagPresent");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.TagPresent");
        using var span = source.StartActivity("op");
        span!.SetTag("cycle.guid", "abc");

        await Assert.That(span).HasTag("cycle.guid");
    }

    [Test]
    public async Task HasTag_Absent_Fails(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.TagAbsent");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.TagAbsent");
        using var span = source.StartActivity("op");

        var exception = await Assert.That(async () =>
        {
            await Assert.That(span!).HasTag("missing");
        }).Throws<AssertionException>();

        await Assert.That(exception!.Message).Contains("missing");
    }

    [Test]
    public async Task HasTag_NullSpan_Throws(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Assert.That(() => SpanAssertions.HasTag(null!, "key")).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task HasTag_NullKey_Throws(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.TagNullKey");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.TagNullKey");
        using var span = source.StartActivity("op");

        await Assert.That(() => SpanAssertions.HasTag(span!, null!)).Throws<ArgumentNullException>();
    }

    // ---- HasTag (value) ----

    [Test]
    public async Task HasTagValue_Matches_Passes(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.TagValPass");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.TagValPass");
        using var span = source.StartActivity("op");
        span!.SetTag("process.id", 0);

        // Tag stored as int 0; compared by invariant ToString, so the string "0" matches.
        await Assert.That(span).HasTagValue("process.id", "0");
    }

    [Test]
    public async Task HasTagValue_Mismatch_Fails(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.TagValMiss");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.TagValMiss");
        using var span = source.StartActivity("op");
        span!.SetTag("process.id", 1);

        var exception = await Assert.That(async () =>
        {
            await Assert.That(span).HasTagValue("process.id", "0");
        }).Throws<AssertionException>();

        await Assert.That(exception!.Message).Contains("to be \"0\"");
        await Assert.That(exception.Message).Contains("but it was \"1\"");
    }

    [Test]
    public async Task HasTagValue_Absent_Fails(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.TagValAbsent");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.TagValAbsent");
        using var span = source.StartActivity("op");

        var exception = await Assert.That(async () =>
        {
            await Assert.That(span!).HasTagValue("missing", "x");
        }).Throws<AssertionException>();

        await Assert.That(exception!.Message).Contains("but the tag was absent");
    }

    [Test]
    public async Task HasTagValue_NullValue_Throws(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.TagValNull");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.TagValNull");
        using var span = source.StartActivity("op");

        await Assert.That(() => SpanAssertions.HasTagValue(span!, "key", null!)).Throws<ArgumentNullException>();
    }

    // ---- HasStatus ----

    [Test]
    public async Task HasStatus_Matches_Passes(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.StatusPass");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.StatusPass");
        using var span = source.StartActivity("op");
        span!.SetStatus(ActivityStatusCode.Ok);

        await Assert.That(span).HasStatus(ActivityStatusCode.Ok);
    }

    [Test]
    public async Task HasStatus_Mismatch_Fails(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.StatusFail");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.StatusFail");
        using var span = source.StartActivity("op");
        span!.SetStatus(ActivityStatusCode.Error);

        var exception = await Assert.That(async () =>
        {
            await Assert.That(span).HasStatus(ActivityStatusCode.Ok);
        }).Throws<AssertionException>();

        await Assert.That(exception!.Message).Contains("Ok");
        await Assert.That(exception.Message).Contains("Error");
    }

    [Test]
    public async Task HasStatus_NullSpan_Throws(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Assert.That(() => SpanAssertions.HasStatus(null!, ActivityStatusCode.Ok)).Throws<ArgumentNullException>();
    }

    // ---- IsChildOf / SharesTraceWith ----

    [Test]
    public async Task IsChildOf_DirectChild_Passes(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Activity parentSpan;
        Activity childSpan;
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.ChildPass");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.ChildPass");

        using (var parent = source.StartActivity("parent"))
        {
            parentSpan = parent!;
            using (var child = source.StartActivity("child"))
            {
                childSpan = child!;
            }
        }

        await Assert.That(childSpan).IsChildOf(parentSpan);
        await Assert.That(childSpan).SharesTraceWith(parentSpan);
    }

    [Test]
    public async Task IsChildOf_NotChild_Fails(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Activity rootA;
        Activity rootB;
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.ChildFail");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.ChildFail");

        using (var a = source.StartActivity("a", ActivityKind.Internal, NewRootContext()))
        {
            rootA = a!;
        }

        using (var b = source.StartActivity("b", ActivityKind.Internal, NewRootContext()))
        {
            rootB = b!;
        }

        var exception = await Assert.That(async () =>
        {
            await Assert.That(rootB).IsChildOf(rootA);
        }).Throws<AssertionException>();

        await Assert.That(exception!.Message).Contains("child of parent span");
    }

    [Test]
    public async Task SharesTraceWith_DifferentTrace_Fails(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Activity rootA;
        Activity rootB;
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.TraceFail");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.TraceFail");

        using (var a = source.StartActivity("a", ActivityKind.Internal, NewRootContext()))
        {
            rootA = a!;
        }

        using (var b = source.StartActivity("b", ActivityKind.Internal, NewRootContext()))
        {
            rootB = b!;
        }

        var exception = await Assert.That(async () =>
        {
            await Assert.That(rootB).SharesTraceWith(rootA);
        }).Throws<AssertionException>();

        await Assert.That(exception!.Message).Contains("to share trace");
    }

    [Test]
    public async Task IsChildOf_NullArgs_Throw(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.ChildNull");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.ChildNull");
        using var span = source.StartActivity("op");

        await Assert.That(() => SpanAssertions.IsChildOf(null!, span!)).Throws<ArgumentNullException>();
        await Assert.That(() => SpanAssertions.IsChildOf(span!, null!)).Throws<ArgumentNullException>();
        await Assert.That(() => SpanAssertions.SharesTraceWith(null!, span!)).Throws<ArgumentNullException>();
        await Assert.That(() => SpanAssertions.SharesTraceWith(span!, null!)).Throws<ArgumentNullException>();
    }
}
