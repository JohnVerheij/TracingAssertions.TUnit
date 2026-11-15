using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace TracingAssertions.TUnit.Tests;

/// <summary>
/// Tests for <c>HasSpanWithTag</c> (the non-null lookup that yields the span), <c>HasChildSpan</c> (yields the
/// child) and <c>HasChildSpanCount</c>.
/// </summary>
[Category("Smoke")]
[Timeout(10_000)]
internal sealed class SpanLookupAndChildAssertionsTests
{
    // ---- HasSpanWithTag ----

    [Test]
    public async Task HasSpanWithTag_Match_YieldsTheSpan(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.Lookup.Match");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.Lookup.Match");
        using (var span = source.StartActivity("cycle"))
        {
            span!.SetTag("cycle.guid", "abc");
        }

        var found = await Assert.That(capture).HasSpanWithTag("cycle", "cycle.guid", "abc");

        // The yielded span is usable at once, with no null check.
        await Assert.That(found.OperationName).IsEqualTo("cycle");
        await Assert.That(found.GetTagItem("cycle.guid")).IsEqualTo("abc");
    }

    [Test]
    public async Task HasSpanWithTag_ReturnsTheFirstMatchingSpanInCompletionOrder(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.Lookup.First");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.Lookup.First");
        using (var first = source.StartActivity("cycle"))
        {
            first!.SetTag("n", 1);
            first.SetTag("k", "v");
        }

        using (var second = source.StartActivity("cycle"))
        {
            second!.SetTag("n", 2);
            second.SetTag("k", "v");
        }

        var found = await Assert.That(capture).HasSpanWithTag("cycle", "k", "v");

        await Assert.That(found.GetTagItem("n")).IsEqualTo(1);
    }

    [Test]
    public async Task HasSpanWithTag_ComparesByInvariantToString_NonStringValues(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.Lookup.Types");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.Lookup.Types");
        using (var span = source.StartActivity("op"))
        {
            span!.SetTag("flag", true);
            span.SetTag("count", 3);
        }

        // A bool tag matches the bool and its invariant text "True": exact case, as documented.
        await Assert.That(await Assert.That(capture).HasSpanWithTag("op", "flag", true)).IsNotNull();
        await Assert.That(await Assert.That(capture).HasSpanWithTag("op", "flag", "True")).IsNotNull();
        await Assert.That(await Assert.That(capture).HasSpanWithTag("op", "count", 3)).IsNotNull();
        await Assert.That(await Assert.That(capture).HasSpanWithTag("op", "count", "3")).IsNotNull();
        await Assert.That(async () => await Assert.That(capture).HasSpanWithTag("op", "flag", "true"))
            .Throws<global::TUnit.Assertions.Exceptions.AssertionException>();
    }

    [Test]
    public async Task HasSpanWithTag_RightNameWrongTag_Fails(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.Lookup.WrongTag");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.Lookup.WrongTag");
        using (var span = source.StartActivity("cycle"))
        {
            span!.SetTag("other", "abc");
        }

        await Assert.That(async () => await Assert.That(capture).HasSpanWithTag("cycle", "cycle.guid", "abc"))
            .Throws<global::TUnit.Assertions.Exceptions.AssertionException>();
    }

    [Test]
    public async Task HasSpanWithTag_NullArguments_Throw(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.Lookup.Null");

        await Assert.That(() => SpanCaptureAssertions.HasSpanWithTag(null!, "op", "k", "v")).Throws<ArgumentNullException>();
        await Assert.That(() => SpanCaptureAssertions.HasSpanWithTag(capture, null!, "k", "v")).Throws<ArgumentNullException>();
        await Assert.That(() => SpanCaptureAssertions.HasSpanWithTag(capture, "op", null!, "v")).Throws<ArgumentNullException>();
        await Assert.That(() => SpanCaptureAssertions.HasSpanWithTag(capture, "op", "k", null!)).Throws<ArgumentNullException>();
    }

    // ---- HasChildSpan ----

    [Test]
    public async Task HasChildSpan_Present_YieldsTheChild(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.Child.Present");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.Child.Present");
        Activity root;
        using (var rootSpan = source.StartActivity("root"))
        {
            root = rootSpan!;
            using (var child = source.StartActivity("child"))
            {
                child!.SetTag("n", 1);
            }

            using (source.StartActivity("sibling"))
            {
                // The span stops, and is captured, when the scope closes.
            }
        }

        var found = await Assert.That(root).HasChildSpan(capture, "child");

        await Assert.That(found.OperationName).IsEqualTo("child");
        await Assert.That(found.GetTagItem("n")).IsEqualTo(1);
    }

    [Test]
    public async Task HasChildSpan_NameExistsOnlyAsGrandchild_Fails(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.Child.Grand");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.Child.Grand");
        Activity root;
        using (var rootSpan = source.StartActivity("root"))
        {
            root = rootSpan!;
            using (source.StartActivity("child"))
            {
                using (source.StartActivity("grandchild"))
                {
                    // The span stops, and is captured, when the scope closes.
                }
            }
        }

        await Assert.That(async () => await Assert.That(root).HasChildSpan(capture, "grandchild"))
            .Throws<global::TUnit.Assertions.Exceptions.AssertionException>();
    }

    [Test]
    public async Task HasChildSpan_HierarchicalIds_FindsTheChild(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.Child.Hier");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.Child.Hier");
        Activity root;
        var rootSpan = source.CreateActivity("root", ActivityKind.Internal)!;
        rootSpan.SetIdFormat(ActivityIdFormat.Hierarchical);
        using (rootSpan.Start())
        {
            root = rootSpan;
            var child = source.CreateActivity("child", ActivityKind.Internal)!;
            child.SetIdFormat(ActivityIdFormat.Hierarchical);
            using (child.Start())
            {
                // The span stops, and is captured, when the scope closes.
            }
        }

        var found = await Assert.That(root).HasChildSpan(capture, "child");

        await Assert.That(found.OperationName).IsEqualTo("child");
    }

    [Test]
    public async Task HasChildSpan_NullArguments_Throw(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.Child.Null");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.Child.Null");
        using var root = source.StartActivity("root");

        await Assert.That(() => SpanAssertions.HasChildSpan(null!, capture, "c")).Throws<ArgumentNullException>();
        await Assert.That(() => SpanAssertions.HasChildSpan(root!, null!, "c")).Throws<ArgumentNullException>();
        await Assert.That(() => SpanAssertions.HasChildSpan(root!, capture, null!)).Throws<ArgumentNullException>();
    }

    // ---- HasChildSpanCount ----

    [Test]
    public async Task HasChildSpanCount_CountsOnlyDirectChildren(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.ChildCount.Direct");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.ChildCount.Direct");
        Activity root;
        using (var rootSpan = source.StartActivity("root"))
        {
            root = rootSpan!;
            using (source.StartActivity("a"))
            {
                using (source.StartActivity("a.child"))
                {
                    // The span stops, and is captured, when the scope closes.
                }
            }

            using (source.StartActivity("b"))
            {
                // The span stops, and is captured, when the scope closes.
            }
        }

        await Assert.That(root).HasChildSpanCount(capture, 2);
        await Assert.That(async () => await Assert.That(root).HasChildSpanCount(capture, 3))
            .Throws<global::TUnit.Assertions.Exceptions.AssertionException>();
    }

    [Test]
    public async Task HasChildSpanCount_ZeroForALeaf(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.ChildCount.Leaf");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.ChildCount.Leaf");
        Activity leaf;
        using (var span = source.StartActivity("leaf"))
        {
            leaf = span!;
        }

        await Assert.That(leaf).HasChildSpanCount(capture, 0);
    }

    [Test]
    public async Task HasChildSpanCount_InvalidArguments_Throw(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.ChildCount.Args");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.ChildCount.Args");
        using var root = source.StartActivity("root");

        await Assert.That(() => SpanAssertions.HasChildSpanCount(null!, capture, 1)).Throws<ArgumentNullException>();
        await Assert.That(() => SpanAssertions.HasChildSpanCount(root!, null!, 1)).Throws<ArgumentNullException>();
        await Assert.That(() => SpanAssertions.HasChildSpanCount(root!, capture, -1)).Throws<ArgumentOutOfRangeException>();
    }
}
