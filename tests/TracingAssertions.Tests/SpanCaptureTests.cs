using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace TracingAssertions.Tests;

/// <summary>
/// Tests for the framework-agnostic <see cref="SpanCapture"/>: single-source capture via a raw
/// <see cref="ActivityListener"/>, collection of stopped activities, and listener detach on dispose.
/// </summary>
[Category("Smoke")]
[Timeout(10_000)]
internal sealed class SpanCaptureTests
{
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

    [Test]
    public async Task ForSource_NullName_Throws(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Assert.That(() => SpanCapture.ForSource(null!)).Throws<System.ArgumentNullException>();
    }
}
