using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace TracingAssertions.Tests;

/// <summary>
/// Tests for the branches of the query helpers and the capture listener that the other tests reach on one side
/// only: a name that matches with a tag that does not and a parent given as an id string.
/// </summary>
[Category("Smoke")]
[Timeout(10_000)]
internal sealed class SpanCaptureQueryArmsTests
{
    [Test]
    public async Task FindByOperationNameAndTag_NameMatchesButTagDiffers_ReturnsNull(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.Tests.Arms.Tag");
        using var capture = SpanCapture.ForSource("TracingAssertions.Tests.Arms.Tag");
        using (var span = source.StartActivity("op"))
        {
            span!.SetTag("k", "v");
        }

        await Assert.That(capture.FindByOperationNameAndTag("op", "k", "other")).IsNull();
        await Assert.That(capture.FindByOperationNameAndTag("op", "missing", "v")).IsNull();
        await Assert.That(capture.FindByOperationNameAndTag("other.op", "k", "v")).IsNull();
        await Assert.That(capture.FindByOperationNameAndTag("op", "k", "v")).IsNotNull();
    }

    [Test]
    public async Task ParentGivenAsAnIdString_IsSampledAndCaptured(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.Tests.Arms.ParentId");
        using var capture = SpanCapture.ForSource("TracingAssertions.Tests.Arms.ParentId");
        const string parentId = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";

        using (var span = source.StartActivity("from.parent.id", ActivityKind.Server, parentId))
        {
            await Assert.That(span).IsNotNull();
        }

        var captured = await Assert.That(capture.Captured).HasSingleItem();
        await Assert.That(captured.ParentId).IsEqualTo(parentId);
    }
}
