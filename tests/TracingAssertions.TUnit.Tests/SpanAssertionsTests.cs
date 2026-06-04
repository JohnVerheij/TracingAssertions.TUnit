using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TUnit.Assertions.Exceptions;

namespace TracingAssertions.TUnit.Tests;

/// <summary>
/// Tests for the TUnit adapter's foundation assertion <c>HasOperationName</c> generated over
/// <see cref="Activity"/>.
/// </summary>
[Category("Smoke")]
[Timeout(10_000)]
internal sealed class SpanAssertionsTests
{
    [Test]
    public async Task HasOperationName_Matches_Passes(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.Pass");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.Pass");
        using var span = source.StartActivity("pick.pipeline");

        await Assert.That(span!).HasOperationName("pick.pipeline");
    }

    [Test]
    public async Task HasOperationName_Mismatch_Fails(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.Fail");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.Fail");
        using var span = source.StartActivity("actual.op");

        var exception = await Assert.That(async () =>
        {
            await Assert.That(span!).HasOperationName("expected.op");
        }).Throws<AssertionException>();

        await Assert.That(exception!.Message).Contains("expected.op");
        await Assert.That(exception.Message).Contains("actual.op");
    }

    [Test]
    public async Task HasOperationName_NullOperationName_Throws(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new ActivitySource("TracingAssertions.TUnit.Tests.Null");
        using var capture = SpanCapture.ForSource("TracingAssertions.TUnit.Tests.Null");
        using var span = source.StartActivity("op");

        await Assert.That(async () => await Assert.That(span!).HasOperationName(null!))
            .Throws<ArgumentNullException>();
    }
}
