using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TracingAssertions;
using TUnit.Assertions.Exceptions;

namespace TracingAssertions.TUnit.Tests;

/// <summary>
/// Tests for the capture-level adapter assertion <c>HasSpan</c> generated over
/// <see cref="SpanCapture"/>.
/// </summary>
[Category("Smoke")]
[Timeout(10_000)]
internal sealed class SpanCaptureAssertionsTests
{
    [Test]
    public async Task HasSpan_Present_Passes(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.HasSpanPass");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.HasSpanPass");

        using (source.StartActivity("order.pipeline"))
        {
            // The span is captured when the using scope closes.
        }

        await Assert.That(capture).HasSpan("order.pipeline");
    }

    [Test]
    public async Task HasSpan_Absent_Fails_ListsCapturedNames(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.HasSpanAbsent");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.HasSpanAbsent");

        using (source.StartActivity("other.op"))
        {
            // A non-matching span is captured when the using scope closes.
        }

        var exception = await Assert.That(async () =>
        {
            await Assert.That(capture).HasSpan("order.pipeline");
        }).Throws<AssertionException>();

        await Assert.That(exception!.Message).Contains("order.pipeline");
        await Assert.That(exception.Message).Contains("other.op");
    }

    [Test]
    public async Task HasSpan_Absent_MultipleCaptured_ListsAllNamesSeparated(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.HasSpanMulti");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.HasSpanMulti");

        using (source.StartActivity("first.op"))
        {
            // captured on scope exit.
        }

        using (source.StartActivity("second.op"))
        {
            // captured on scope exit.
        }

        var exception = await Assert.That(async () =>
        {
            await Assert.That(capture).HasSpan("order.pipeline");
        }).Throws<AssertionException>();

        await Assert.That(exception!.Message).Contains("first.op");
        await Assert.That(exception.Message).Contains("second.op");
        await Assert.That(exception.Message).Contains("\", \"");
    }

    [Test]
    public async Task HasSpan_EmptyCapture_Fails(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.HasSpanEmpty");

        var exception = await Assert.That(async () =>
        {
            await Assert.That(capture).HasSpan("order.pipeline");
        }).Throws<AssertionException>();

        await Assert.That(exception!.Message).Contains("<none>");
    }

    [Test]
    public async Task HasSpan_NullCapture_Throws(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Assert.That(() => SpanCaptureAssertions.HasSpan(null!, "op")).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task HasSpan_NullOperationName_Throws(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.HasSpanNull");
        await Assert.That(() => SpanCaptureAssertions.HasSpan(capture, null!)).Throws<ArgumentNullException>();
    }
}
