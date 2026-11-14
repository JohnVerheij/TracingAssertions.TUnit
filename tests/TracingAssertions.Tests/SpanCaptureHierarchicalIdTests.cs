using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace TracingAssertions.Tests;

/// <summary>
/// Tests for <see cref="SpanCapture.ChildrenOf(Activity)"/> with <see cref="ActivityIdFormat.Hierarchical"/> ids,
/// where the W3C trace id, span id and parent span id are all default: the relationship must come from the
/// activity id and the parent id, and spans of the two formats must never be related.
/// </summary>
[Category("Smoke")]
[Timeout(10_000)]
internal sealed class SpanCaptureHierarchicalIdTests
{
    private static Activity StartHierarchical(ActivitySource source, string name)
    {
        var span = source.CreateActivity(name, ActivityKind.Internal)!;
        span.SetIdFormat(ActivityIdFormat.Hierarchical);
        return span.Start();
    }

    [Test]
    public async Task ChildrenOf_HierarchicalIds_ReturnsOnlyTheDirectChildren(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var noCurrent = new NoCurrentActivity();
        using var source = new ActivitySource("TracingAssertions.Tests.Hier.Children");
        using var capture = SpanCapture.ForSource("TracingAssertions.Tests.Hier.Children");
        Activity root;
        Activity unrelated;

        using (var rootSpan = StartHierarchical(source, "root"))
        {
            root = rootSpan;
            using (StartHierarchical(source, "child"))
            {
                using (StartHierarchical(source, "grandchild"))
                {
                    // The span stops, and is captured, when the scope closes.
                }
            }
        }

        // A second hierarchical tree: with the old W3C-only comparison every default id matched every other.
        using (var other = StartHierarchical(source, "other.root"))
        {
            unrelated = other;
        }

        var children = capture.ChildrenOf(root);

        await Assert.That(children).HasSingleItem();
        await Assert.That(children[0].OperationName).IsEqualTo("child");
        await Assert.That(capture.ChildrenOf(unrelated)).IsEmpty();
    }

    [Test]
    public async Task ChildrenOf_HierarchicalChildOfW3CParent_IsNotAChild(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.Tests.Hier.Mixed");
        using var capture = SpanCapture.ForSource("TracingAssertions.Tests.Hier.Mixed");
        Activity w3cParent;

        using (var parent = source.StartActivity("w3c.parent"))
        {
            w3cParent = parent!;
            using (StartHierarchical(source, "hier.child"))
            {
                // The span stops, and is captured, when the scope closes.
            }
        }

        await Assert.That(w3cParent.IdFormat).IsEqualTo(ActivityIdFormat.W3C);
        await Assert.That(capture.ChildrenOf(w3cParent)).IsEmpty();
    }

    [Test]
    public async Task ChildrenOf_W3CChildOfHierarchicalParent_IsNotAChild(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var noCurrent = new NoCurrentActivity();
        using var source = new ActivitySource("TracingAssertions.Tests.Hier.MixedReverse");
        using var capture = SpanCapture.ForSource("TracingAssertions.Tests.Hier.MixedReverse");
        Activity hierarchicalParent;

        using (var parent = StartHierarchical(source, "hier.parent"))
        {
            hierarchicalParent = parent;
            var w3cChild = source.CreateActivity("w3c.child", ActivityKind.Internal)!;
            w3cChild.SetIdFormat(ActivityIdFormat.W3C);
            using (w3cChild.Start())
            {
                // The span stops, and is captured, when the scope closes.
            }
        }

        await Assert.That(capture.ChildrenOf(hierarchicalParent)).IsEmpty();
    }
}
