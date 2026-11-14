using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace TracingAssertions.Tests;

/// <summary>
/// Tests for <see cref="SpanTreeRenderer"/> and <see cref="SpanTreeOptions"/>: the exact text, the ordering
/// rules, tag selection, spans without a captured parent, hierarchical ids and very large captures.
/// </summary>
[Category("Smoke")]
[Timeout(30_000)]
internal sealed class SpanTreeRendererTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Starts a span whose start time is fixed, so the sibling order does not depend on the clock.</summary>
    private static Activity Start(
        ActivitySource source, string name, int startSecond, ActivityKind kind = ActivityKind.Internal)
    {
        var span = source.CreateActivity(name, kind)!;
        span.SetStartTime(T0.AddSeconds(startSecond));
        return span.Start();
    }

    private static Activity StartHierarchical(ActivitySource source, string name, int startSecond)
    {
        var span = source.CreateActivity(name, ActivityKind.Internal)!;
        span.SetIdFormat(ActivityIdFormat.Hierarchical);
        span.SetStartTime(T0.AddSeconds(startSecond));
        return span.Start();
    }

    [Test]
    public async Task Render_EmptyCapture_ReturnsNoSpansText(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var capture = SpanCapture.ForSource("TracingAssertions.Tests.Render.Empty");

        await Assert.That(SpanTreeRenderer.Render(capture)).IsEqualTo("<no spans>");
    }

    [Test]
    public async Task Render_ParentWithChildren_IndentsByDepthAndOrdersByStartTime(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.Tests.Render.Tree");
        using var capture = SpanCapture.ForSource("TracingAssertions.Tests.Render.Tree");

        using (var root = Start(source, "root", 0, ActivityKind.Server))
        {
            // The later child completes first: completion order must not decide the rendered order.
            using (var late = Start(source, "late", 5))
            {
                late.SetStatus(ActivityStatusCode.Error);
            }

            using (var early = Start(source, "early", 1, ActivityKind.Client))
            {
                using (Start(source, "grandchild", 2))
                {
                    // Stops first, so the grandchild is captured before its parent.
                }
            }

            root.SetStatus(ActivityStatusCode.Ok);
        }

        await Assert.That(SpanTreeRenderer.Render(capture)).IsEqualTo(
            "root [Server, Ok]\n"
            + "  early [Client, Unset]\n"
            + "    grandchild [Internal, Unset]\n"
            + "  late [Internal, Error]");
    }

    [Test]
    public async Task Render_SiblingsWithTheSameStartTime_OrderByOperationName(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.Tests.Render.Ties");
        using var capture = SpanCapture.ForSource("TracingAssertions.Tests.Render.Ties");

        using (Start(source, "root", 0))
        {
            using (Start(source, "b", 1))
            {
                // The span stops, and is captured, when the scope closes.
            }

            using (Start(source, "c", 1))
            {
                // The span stops, and is captured, when the scope closes.
            }

            using (Start(source, "a", 1))
            {
                // The span stops, and is captured, when the scope closes.
            }
        }

        await Assert.That(SpanTreeRenderer.Render(capture)).IsEqualTo(
            "root [Internal, Unset]\n  a [Internal, Unset]\n  b [Internal, Unset]\n  c [Internal, Unset]");
    }

    [Test]
    public async Task Render_SiblingsTiedInStartTimeAndName_KeepTheOrderTheyWereCaptured(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.Tests.Render.TiedSiblings");
        using var capture = SpanCapture.ForSource("TracingAssertions.Tests.Render.TiedSiblings");
        const int count = 64;

        using (Start(source, "root", 0))
        {
            for (var i = 0; i < count; i++)
            {
                using var child = Start(source, "same", 1);
                child.SetTag("i", i);
            }
        }

        var expected = new List<string> { "root [Internal, Unset]" };
        for (var i = 0; i < count; i++)
        {
            expected.Add("  same [Internal, Unset] {i=" + i.ToString(CultureInfo.InvariantCulture) + "}");
        }

        // More siblings than an unstable sort keeps in place: equal spans must stay in capture (completion) order.
        await Assert.That(SpanTreeRenderer.Render(capture, new SpanTreeOptions { TagKeys = ["i"] }))
            .IsEqualTo(string.Join('\n', expected));
    }

    [Test]
    public async Task Render_TiedRoots_KeepTheOrderTheyWereCaptured(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var noCurrent = new NoCurrentActivity();
        using var source = new ActivitySource("TracingAssertions.Tests.Render.TiedRoots");
        using var capture = SpanCapture.ForSource("TracingAssertions.Tests.Render.TiedRoots");
        const int count = 64;

        for (var i = 0; i < count; i++)
        {
            using var root = Start(source, "same", 1);
            root.SetTag("i", i);
        }

        var expected = new List<string>();
        for (var i = 0; i < count; i++)
        {
            expected.Add("same [Internal, Unset] {i=" + i.ToString(CultureInfo.InvariantCulture) + "}");
        }

        await Assert.That(SpanTreeRenderer.Render(capture, new SpanTreeOptions { TagKeys = ["i"] }))
            .IsEqualTo(string.Join('\n', expected));
    }

    [Test]
    public async Task Render_W3CSpanWithANonW3CParentId_IsShownAtTopLevel(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var noCurrent = new NoCurrentActivity();
        using var source = new ActivitySource("TracingAssertions.Tests.Render.NonW3CParentId");
        using var capture = SpanCapture.ForSource("TracingAssertions.Tests.Render.NonW3CParentId");

        var span = source.CreateActivity("odd", ActivityKind.Internal)!;
        span.SetIdFormat(ActivityIdFormat.W3C);
        span.SetParentId("|abc.1.");
        using (span.Start())
        {
            await Assert.That(span.ParentSpanId == default).IsTrue();
        }

        await Assert.That(SpanTreeRenderer.Render(capture)).IsEqualTo("odd [Internal, Unset]");
    }

    [Test]
    public async Task Render_Roots_AreOrderedByStartTime(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.Tests.Render.Roots");
        using var capture = SpanCapture.ForSource("TracingAssertions.Tests.Render.Roots");

        var context = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
        using (var second = source.StartActivity("second", ActivityKind.Internal, context, startTime: new DateTimeOffset(T0.AddSeconds(2))))
        {
            // First to complete, last to start.
            await Assert.That(second).IsNotNull();
        }

        using (var first = source.StartActivity("first", ActivityKind.Internal, context, startTime: new DateTimeOffset(T0.AddSeconds(1))))
        {
            await Assert.That(first).IsNotNull();
        }

        await Assert.That(SpanTreeRenderer.Render(capture)).IsEqualTo(
            "first [Internal, Unset]\nsecond [Internal, Unset]");
    }

    [Test]
    public async Task Render_TagKeys_ShowSelectedTagsInOptionOrderAndSkipAbsentOnes(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.Tests.Render.Tags");
        using var capture = SpanCapture.ForSource("TracingAssertions.Tests.Render.Tags");

        using (var root = Start(source, "root", 0))
        {
            root.SetTag("a", 1);
            root.SetTag("z", "last");
            root.SetTag("hidden", "secret");
            using (Start(source, "bare", 1))
            {
                // The span stops, and is captured, when the scope closes.
            }
        }

        var options = new SpanTreeOptions { TagKeys = ["z", "missing", "a"] };

        await Assert.That(SpanTreeRenderer.Render(capture, options)).IsEqualTo(
            "root [Internal, Unset] {z=last, a=1}\n  bare [Internal, Unset]");
        await Assert.That(SpanTreeRenderer.Render(capture)).IsEqualTo(
            "root [Internal, Unset]\n  bare [Internal, Unset]");
    }

    [Test]
    public async Task Render_TagValues_UseTheInvariantCulture(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.Tests.Render.Culture");
        using var capture = SpanCapture.ForSource("TracingAssertions.Tests.Render.Culture");
        using (var span = Start(source, "op", 0))
        {
            span.SetTag("ratio", 1.5);
        }

        var previous = CultureInfo.CurrentCulture;
        string text;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            text = SpanTreeRenderer.Render(capture, new SpanTreeOptions { TagKeys = ["ratio"] });
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }

        await Assert.That(text).IsEqualTo("op [Internal, Unset] {ratio=1.5}");
    }

    [Test]
    public async Task Render_SpanWhoseParentWasNotCaptured_IsShownAtTopLevel(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var listened = new ActivitySource("TracingAssertions.Tests.Render.Orphan");
        using var unlistened = new ActivitySource("TracingAssertions.Tests.Render.OrphanParent");
        using var capture = SpanCapture.ForSource("TracingAssertions.Tests.Render.Orphan");
        // A listener for the second source keeps its spans alive so the child gets a parent that is not captured.
        using var keepAlive = SpanCapture.ForSource("TracingAssertions.Tests.Render.OrphanParent");

        using (Start(unlistened, "outer", 0))
        {
            using (Start(listened, "inner", 1))
            {
                // The span stops, and is captured, when the scope closes.
            }
        }

        await Assert.That(SpanTreeRenderer.Render(capture)).IsEqualTo("inner [Internal, Unset]");
    }

    [Test]
    public async Task Render_HierarchicalIds_UseTheSameFormatAsW3CIds(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var noCurrent = new NoCurrentActivity();
        using var source = new ActivitySource("TracingAssertions.Tests.Render.Hier");
        using var capture = SpanCapture.ForSource("TracingAssertions.Tests.Render.Hier");

        using (StartHierarchical(source, "root", 0))
        {
            using (var child = StartHierarchical(source, "child", 2))
            {
                await Assert.That(child.IdFormat).IsEqualTo(ActivityIdFormat.Hierarchical);
            }

            using (StartHierarchical(source, "first", 1))
            {
                // The span stops, and is captured, when the scope closes.
            }
        }

        await Assert.That(SpanTreeRenderer.Render(capture)).IsEqualTo(
            "root [Internal, Unset]\n  first [Internal, Unset]\n  child [Internal, Unset]");
    }

    [Test]
    public async Task Render_DeepChain_DoesNotOverflowTheStack(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.Tests.Render.Deep");
        using var capture = SpanCapture.ForSource("TracingAssertions.Tests.Render.Deep");
        const int depth = 3000;
        var open = new Stack<Activity>();
        for (var i = 0; i < depth; i++)
        {
            open.Push(Start(source, "n", i));
        }

        while (open.Count > 0)
        {
            open.Pop().Dispose();
        }

        var lines = SpanTreeRenderer.Render(capture).Split('\n');

        await Assert.That(lines.Length).IsEqualTo(depth);
        await Assert.That(lines[depth - 1]).IsEqualTo(new string(' ', (depth - 1) * 2) + "n [Internal, Unset]");
    }

    [Test]
    public async Task Render_ManyChildrenOfOneParent_RendersAllInOrder(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.Tests.Render.Wide");
        using var capture = SpanCapture.ForSource("TracingAssertions.Tests.Render.Wide");
        const int count = 5000;
        using (Start(source, "root", 0))
        {
            for (var i = 0; i < count; i++)
            {
                using (Start(source, "c" + i.ToString("D5", CultureInfo.InvariantCulture), 1 + i))
                {
                    // The span stops, and is captured, when the scope closes.
                }
            }
        }

        var lines = SpanTreeRenderer.Render(capture).Split('\n');

        await Assert.That(lines.Length).IsEqualTo(count + 1);
        await Assert.That(lines[1]).IsEqualTo("  c00000 [Internal, Unset]");
        await Assert.That(lines[count]).IsEqualTo("  c04999 [Internal, Unset]");
    }

    [Test]
    public async Task Render_NullCapture_Throws(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Assert.That(() => SpanTreeRenderer.Render(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Options_NullTagKeys_Throws(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Assert.That(() => new SpanTreeOptions { TagKeys = null! }).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Options_DefaultTagKeys_IsEmpty(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Assert.That(new SpanTreeOptions().TagKeys).IsEmpty();
    }

    [Test]
    public async Task Options_NullTagKeyInTheList_IsRejected(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Assert.That(() => new SpanTreeOptions { TagKeys = ["ok", null!] }).Throws<ArgumentException>();
    }

    [Test]
    public async Task Options_EmptyAndFilledTagKeys_AreAccepted(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Assert.That(new SpanTreeOptions { TagKeys = [] }.TagKeys).IsEmpty();
        await Assert.That(new SpanTreeOptions { TagKeys = ["a", "b"] }.TagKeys.Count).IsEqualTo(2);
    }
}
