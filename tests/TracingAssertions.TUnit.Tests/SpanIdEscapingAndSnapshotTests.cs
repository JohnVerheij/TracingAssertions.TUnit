using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace TracingAssertions.TUnit.Tests;

/// <summary>
/// Tests for the activity ids in a failure text (a parent id, a hierarchical id and a root id are caller-supplied
/// strings and can hold a line break) and for the lookup that decides and describes a failure from one snapshot.
/// </summary>
[Category("Smoke")]
[Timeout(10_000)]
internal sealed class SpanIdEscapingAndSnapshotTests
{
    /// <summary>A tag value that starts a matching span on its first conversion to text, which is what the lookup
    /// does while it compares tag values: the capture grows in the middle of the assertion.</summary>
    private sealed class GrowsTheCapture(ActivitySource source) : IFormattable
    {
        private int _grown;

        public string ToString(string? format, IFormatProvider? formatProvider)
        {
            if (Interlocked.Exchange(ref _grown, 1) is 0)
            {
                using var late = source.StartActivity("op", ActivityKind.Internal, default(ActivityContext))!;
                late.SetTag("k", "wanted");
            }

            return "decoy";
        }
    }

    [Test]
    public async Task IsRoot_NonW3CParentIdWithLineBreak_StaysOnOneLineInTheText(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var noCurrent = new NoCurrentActivity();
        using var span = new Activity("span");
        span.SetIdFormat(ActivityIdFormat.W3C);
        span.SetParentId("|a\r\nb.1.");
        span.Start();

        var text = await FailureText.OfAsync(async () => await Assert.That(span).IsRoot());

        await Assert.That(text).IsEqualTo(
            "Expected to be a root span (no parent)" + (char)10 + "but its parent was id |a\\r\\nb.1.");
    }

    [Test]
    public async Task IsChildOf_HierarchicalIdsWithLineBreak_StayOnOneLineInTheText(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var noCurrent = new NoCurrentActivity();
        using var span = new Activity("span");
        span.SetIdFormat(ActivityIdFormat.Hierarchical);
        span.SetParentId("|a\nb.1.");
        span.Start();
        using var parent = new Activity("parent");
        parent.SetIdFormat(ActivityIdFormat.Hierarchical);
        parent.SetParentId("|c\r\nd.1.");
        parent.Start();

        var text = await FailureText.OfAsync(async () => await Assert.That(span).IsChildOf(parent));

        // The span's parent id, both root ids and the parent's own id are caller-supplied: none may start a new line.
        await Assert.That(text.Split((char)10).Length).IsEqualTo(2);
        await Assert.That(text).Contains("but its parent was id |a\\nb.1. in trace a\\nb (expected 'parent' (id |c\\r\\nd.1.");
        await Assert.That(text).Contains(" in trace c\\r\\nd)");
    }

    [Test]
    public async Task IsChildOf_HierarchicalParentThatHasNotStarted_NamesNoIdAndNoTrace(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var noCurrent = new NoCurrentActivity();
        using var span = new Activity("span");
        span.SetIdFormat(ActivityIdFormat.Hierarchical);
        span.Start();
        using var parent = new Activity("parent");
        parent.SetIdFormat(ActivityIdFormat.Hierarchical);

        var text = await FailureText.OfAsync(async () => await Assert.That(span).IsChildOf(parent));

        await Assert.That(text).Contains("(expected 'parent' (id ) in trace none)");
    }

    [Test]
    public async Task HasSpanWithTag_CaptureGrowsDuringTheLookup_TheTextDescribesTheSnapshotItSearched(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.Snapshot.Lookup");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.Snapshot.Lookup");
        using (var decoy = source.StartActivity("op", ActivityKind.Internal, default(ActivityContext)))
        {
            decoy!.SetTag("k", new GrowsTheCapture(source));
        }

        var result = SpanCaptureAssertions.HasSpanWithTag(capture, "op", "k", "wanted");

        // The span that "wanted" matches appeared after the search began, so the search did not see it, and the text
        // must not list it either: "none did" and a tree that shows a match would contradict each other.
        await Assert.That(result.IsPassed).IsFalse();
        await Assert.That(result.Message).IsEqualTo(
            "none did (the value looked for was 'wanted'); captured spans:\n  op [Internal, Unset] {k=decoy}");
        await Assert.That(capture.Captured.Count).IsEqualTo(2);
    }
}
