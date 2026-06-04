using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace TracingAssertions.Tests;

/// <summary>
/// Tests for the framework-agnostic <see cref="SpanCapture"/>: single- and multi-source capture via
/// a raw <see cref="ActivityListener"/>, the query helpers (find-by-name, find-by-name-and-tag,
/// children-of), and listener detach on dispose.
/// </summary>
[Category("Smoke")]
[Timeout(10_000)]
internal sealed class SpanCaptureTests
{
    // ---- capture ----

    [Test]
    public async Task ForSource_CapturesStoppedSpanFromMatchingSource(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.Tests.Capture");
        using var capture = SpanCapture.ForSource("TracingAssertions.Tests.Capture");

        using (source.StartActivity("op.one"))
        {
            // The span stops (and is captured) when this using scope closes.
        }

        await Assert.That(capture.Captured).IsNotEmpty();
        await Assert.That(capture.Captured[0].OperationName).IsEqualTo("op.one");
    }

    [Test]
    public async Task ForSource_IgnoresSpansFromOtherSources(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var other = new ActivitySource("TracingAssertions.Tests.Other");
        using var capture = SpanCapture.ForSource("TracingAssertions.Tests.Listened");

        using (other.StartActivity("op.other"))
        {
            // A span from a non-listened source must not be captured.
        }

        await Assert.That(capture.Captured).IsEmpty();
    }

    [Test]
    public async Task ForSources_CapturesFromAnyNamedSource(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var a = new ActivitySource("TracingAssertions.Tests.MultiA");
        using var b = new ActivitySource("TracingAssertions.Tests.MultiB");
        using var capture = SpanCapture.ForSources("TracingAssertions.Tests.MultiA", "TracingAssertions.Tests.MultiB");

        using (a.StartActivity("op.a"))
        {
            // captured: source A is listened.
        }

        using (b.StartActivity("op.b"))
        {
            // captured: source B is listened.
        }

        await Assert.That(capture.Captured).Count().IsEqualTo(2);
    }

    [Test]
    public async Task ForSources_IgnoresUnlistedSource(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var listened = new ActivitySource("TracingAssertions.Tests.MultiListened");
        using var unlisted = new ActivitySource("TracingAssertions.Tests.MultiUnlisted");
        using var capture = SpanCapture.ForSources("TracingAssertions.Tests.MultiListened");

        using (unlisted.StartActivity("op.unlisted"))
        {
            // not captured.
        }

        await Assert.That(capture.Captured).IsEmpty();
    }

    [Test]
    public async Task ForSources_NullNames_Throws(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Assert.That(() => SpanCapture.ForSources(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task ForSource_NullName_Throws(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Assert.That(() => SpanCapture.ForSource(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Dispose_DetachesListener(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.Tests.Detach");
        var capture = SpanCapture.ForSource("TracingAssertions.Tests.Detach");
        capture.Dispose();

        using (source.StartActivity("op.after-dispose"))
        {
            // After dispose the listener is detached, so this span is not captured.
        }

        await Assert.That(capture.Captured).IsEmpty();
    }

    // ---- FindByOperationName ----

    [Test]
    public async Task FindByOperationName_Found_ReturnsSpan(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.Tests.Find");
        using var capture = SpanCapture.ForSource("TracingAssertions.Tests.Find");

        using (source.StartActivity("op.find"))
        {
            // The span is captured when the using scope closes.
        }

        var found = capture.FindByOperationName("op.find");
        await Assert.That(found).IsNotNull();
        await Assert.That(found!.OperationName).IsEqualTo("op.find");
    }

    [Test]
    public async Task FindByOperationName_NotFound_ReturnsNull(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var capture = SpanCapture.ForSource("TracingAssertions.Tests.FindEmpty");
        await Assert.That(capture.FindByOperationName("nope")).IsNull();
    }

    [Test]
    public async Task FindByOperationName_NullName_Throws(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var capture = SpanCapture.ForSource("TracingAssertions.Tests.FindNull");
        await Assert.That(() => capture.FindByOperationName(null!)).Throws<ArgumentNullException>();
    }

    // ---- FindByOperationNameAndTag ----

    [Test]
    public async Task FindByOperationNameAndTag_Found_ReturnsSpan(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.Tests.FindTag");
        using var capture = SpanCapture.ForSource("TracingAssertions.Tests.FindTag");

        using (var span = source.StartActivity("op.tagged"))
        {
            span?.SetTag("process.id", 0);
        }

        // Tag compared by invariant ToString, so the int 0 matches the string "0".
        var found = capture.FindByOperationNameAndTag("op.tagged", "process.id", "0");
        await Assert.That(found).IsNotNull();
    }

    [Test]
    public async Task FindByOperationNameAndTag_TagValueMismatch_ReturnsNull(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.Tests.FindTagMiss");
        using var capture = SpanCapture.ForSource("TracingAssertions.Tests.FindTagMiss");

        using (var span = source.StartActivity("op.tagged"))
        {
            span?.SetTag("process.id", 1);
        }

        await Assert.That(capture.FindByOperationNameAndTag("op.tagged", "process.id", "0")).IsNull();
    }

    [Test]
    public async Task FindByOperationNameAndTag_TagAbsent_ReturnsNull(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.Tests.FindTagAbsent");
        using var capture = SpanCapture.ForSource("TracingAssertions.Tests.FindTagAbsent");

        using (source.StartActivity("op.tagged"))
        {
            // The span (no tag set) is captured when the using scope closes.
        }

        await Assert.That(capture.FindByOperationNameAndTag("op.tagged", "missing", "x")).IsNull();
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    public async Task FindByOperationNameAndTag_NullArg_Throws(int whichNull, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var capture = SpanCapture.ForSource("TracingAssertions.Tests.FindTagNull");
        await Assert.That(() => capture.FindByOperationNameAndTag(
                whichNull == 0 ? null! : "op",
                whichNull == 1 ? null! : "key",
                whichNull == 2 ? null! : "value"))
            .Throws<ArgumentNullException>();
    }

    // ---- ChildrenOf ----

    [Test]
    public async Task ChildrenOf_ReturnsDirectChildren(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Activity parentSpan;
        Activity childSpan;
        using var source = new ActivitySource("TracingAssertions.Tests.Children");
        using var capture = SpanCapture.ForSource("TracingAssertions.Tests.Children");

        using (var parent = source.StartActivity("parent"))
        {
            parentSpan = parent!;
            using (var child = source.StartActivity("child"))
            {
                childSpan = child!;
            }
        }

        var children = capture.ChildrenOf(parentSpan);
        await Assert.That(children).Count().IsEqualTo(1);
        await Assert.That(children[0]).IsSameReferenceAs(childSpan);
    }

    [Test]
    public async Task ChildrenOf_NoChildren_ReturnsEmpty(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Activity loneSpan;
        using var source = new ActivitySource("TracingAssertions.Tests.NoChildren");
        using var capture = SpanCapture.ForSource("TracingAssertions.Tests.NoChildren");

        using (var lone = source.StartActivity("lone"))
        {
            loneSpan = lone!;
        }

        await Assert.That(capture.ChildrenOf(loneSpan)).IsEmpty();
    }

    [Test]
    public async Task ChildrenOf_NullParent_Throws(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var capture = SpanCapture.ForSource("TracingAssertions.Tests.ChildrenNull");
        await Assert.That(() => capture.ChildrenOf(null!)).Throws<ArgumentNullException>();
    }
}
