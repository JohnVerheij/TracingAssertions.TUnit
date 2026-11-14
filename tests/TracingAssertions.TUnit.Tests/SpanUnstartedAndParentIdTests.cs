using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TUnit.Assertions.Exceptions;

namespace TracingAssertions.TUnit.Tests;

/// <summary>
/// Tests for the relationship assertions on spans that were never started (no ids yet) and on a W3C span whose
/// parent id is not a W3C id, so that its parent span id stays default while it still has a parent.
/// </summary>
[Category("Smoke")]
[Timeout(10_000)]
internal sealed class SpanUnstartedAndParentIdTests
{
    private static Activity StartedRoot()
    {
        var span = new Activity("started");
        span.Start();
        return span;
    }

    // ---- spans that have not started ----

    [Test]
    public async Task IsChildOf_TwoW3CSpansThatHaveNotStarted_Fails(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var child = new Activity("child");
        using var parent = new Activity("parent");

        // Every id of an unstarted span is default, and default equals default: that is not a relationship.
        await Assert.That(child.TraceId == default).IsTrue();
        await Assert.That(child.ParentSpanId == parent.SpanId).IsTrue();
        await Assert.That(async () => await Assert.That(child).IsChildOf(parent)).Throws<AssertionException>();
    }

    [Test]
    public async Task IsChildOf_AnUnstartedW3CSpanAndAStartedParent_Fails(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var noCurrent = new NoCurrentActivity();
        using var unstarted = new Activity("unstarted");
        using var started = StartedRoot();

        await Assert.That(async () => await Assert.That(unstarted).IsChildOf(started)).Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(started).IsChildOf(unstarted)).Throws<AssertionException>();
    }

    [Test]
    public async Task SharesTraceWith_TwoW3CSpansThatHaveNotStarted_FailsAndNamesTheDefaultTrace(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var first = new Activity("first");
        using var second = new Activity("second");

        var text = await FailureText.OfAsync(async () => await Assert.That(first).SharesTraceWith(second));

        await Assert.That(text).IsEqualTo(
            "Expected to share a trace with the given span" + (char)10 + "but it was in trace <trace> (expected trace <trace>)");
    }

    [Test]
    public async Task SharesTraceWith_AnUnstartedW3CSpanAndAStartedOne_Fails(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var noCurrent = new NoCurrentActivity();
        using var unstarted = new Activity("unstarted");
        using var started = StartedRoot();

        await Assert.That(async () => await Assert.That(unstarted).SharesTraceWith(started)).Throws<AssertionException>();
        await Assert.That(async () => await Assert.That(started).SharesTraceWith(unstarted)).Throws<AssertionException>();
    }

    [Test]
    public async Task IsRoot_ASpanThatHasNotStarted_Passes(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var w3c = new Activity("w3c");
        using var hierarchical = new Activity("hierarchical");
        hierarchical.SetIdFormat(ActivityIdFormat.Hierarchical);

        // An unstarted span has no parent yet, so it is a root.
        await Assert.That(w3c).IsRoot();
        await Assert.That(hierarchical).IsRoot();
    }

    // ---- a W3C span with a parent id that is not a W3C id ----

    [Test]
    public async Task IsRoot_AW3CSpanWithANonW3CParentId_Fails(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var noCurrent = new NoCurrentActivity();
        using var span = new Activity("span");
        span.SetIdFormat(ActivityIdFormat.W3C);
        span.SetParentId("|abc.1.");
        span.Start();

        // The runtime can produce this span: W3C ids, a parent id, and no parent span id to parse out of it.
        await Assert.That(span.IdFormat).IsEqualTo(ActivityIdFormat.W3C);
        await Assert.That(span.ParentId).IsEqualTo("|abc.1.");
        await Assert.That(span.ParentSpanId == default).IsTrue();

        var text = await FailureText.OfAsync(async () => await Assert.That(span).IsRoot());

        await Assert.That(text).IsEqualTo(
            "Expected to be a root span (no parent)" + (char)10 + "but its parent was id |abc.1.");
    }
}
