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
}
