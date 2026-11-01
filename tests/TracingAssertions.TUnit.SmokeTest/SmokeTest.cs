using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TracingAssertions;
using TUnit.Core;

namespace Smoke.Consumer;

/// <summary>
/// External-consumer smoke test that verifies the just-packed TracingAssertions.TUnit NuGet
/// package can be consumed from a deliberately-different namespace (<c>Smoke.Consumer</c>)
/// without leaking into TracingAssertions.TUnit's internals. Compiles + runs against the
/// local-feed version pinned in <c>NuGet.config</c>, never the in-repo ProjectReference. This is
/// the last CI step before release and the canary that proves the packed nupkg is a usable
/// consumer artifact.
/// </summary>
[Category("Smoke")]
[Timeout(5_000)]
internal sealed class SmokeTest
{
    [Test]
    public async Task ConsumesSpanCaptureFromCore(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("Smoke.Consumer.Core");
        using var capture = SpanCapture.ForSource("Smoke.Consumer.Core");
        using var span = source.StartActivity("smoke.op");

        await Assert.That(capture.Captured).IsNotNull();
    }

    [Test]
    public async Task ConsumesHasOperationNameFromAdapter(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("Smoke.Consumer.Adapter");
        using var capture = SpanCapture.ForSource("Smoke.Consumer.Adapter");
        using var span = source.StartActivity("smoke.op");

        await Assert.That(span!).HasOperationName("smoke.op");
    }

    [Test]
    public async Task WorkedExampleParentChildPairResolvesAndPasses(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var capture = SpanCapture.ForSource(OrderService.SourceName);
        using var root = new Activity("test");
        root.Start();

        OrderService.Place(orderId: 42);

        var place = await Assert.That(capture.ChildrenOf(root)).HasSingleItem();
        await Assert.That(place).HasOperationName("order.place")
            .And.HasKind(ActivityKind.Server)
            .And.HasTagValue("order.id", 42)
            .And.HasStatus(ActivityStatusCode.Ok);

        var persist = await Assert.That(capture.ChildrenOf(place)).HasSingleItem();
        await Assert.That(persist).HasOperationName("order.persist")
            .And.HasKind(ActivityKind.Client);
    }

    [Test]
    public async Task ClearedAmbientActivityMakesTheOperationARoot(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var capture = SpanCapture.ForSource(OrderService.SourceName);
        Activity.Current = null;

        OrderService.Place(orderId: 7);

        var place = capture.FindByOperationNameAndTag("order.place", "order.id", 7)!;
        await Assert.That(place).IsRoot();
    }
}
