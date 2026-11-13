using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace TracingAssertions.TUnit.Tests;

/// <summary>
/// Pins the output samples of the README (the tree and the failure texts) to what the package really prints for the
/// worked example, so a change to the wording or the format fails here and not in a reader's copy.
/// </summary>
[Category("Smoke")]
[Timeout(10_000)]
[NotInParallel("TracingAssertions.TUnit.Tests.Readme.Orders")]
internal sealed class ReadmeOutputTests
{
    private const string SourceName = "TracingAssertions.TUnit.Tests.Readme.Orders";

    private static readonly ActivitySource Source = new(SourceName);

    /// <summary>The worked example of the README: a <c>Server</c> span with a <c>Client</c> child.</summary>
    private static void Place(int orderId)
    {
        using var place = Source.StartActivity("order.place", ActivityKind.Server);
        place?.SetTag("order.id", orderId);

        using (Source.StartActivity("order.persist", ActivityKind.Client))
        {
            // The write itself is out of scope for the example.
        }

        place?.SetStatus(ActivityStatusCode.Ok);
    }

    [Test]
    public async Task Tree_OfTheWorkedExample_IsTheReadmeText(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var capture = SpanCapture.ForSource(SourceName);
        Place(42);

        var tree = SpanTreeRenderer.Render(capture, new SpanTreeOptions { TagKeys = ["order.id"] });

        await Assert.That(tree).IsEqualTo(
            "order.place [Server, Ok] {order.id=42}\n" +
            "  order.persist [Client, Unset]");
    }

    [Test]
    public async Task HasSpanWithTag_WrongValue_IsTheReadmeText(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var capture = SpanCapture.ForSource(SourceName);
        Place(42);

        var text = await FailureText.OfAsync(async () => await Assert.That(capture).HasSpanWithTag("order.place", "order.id", 43));

        await Assert.That(text).IsEqualTo(
            "Expected to include a span named 'order.place' with tag 'order.id' with the expected value\n" +
            "but none did (the value looked for was '43'); captured spans:\n" +
            "  order.place [Server, Ok] {order.id=42}\n" +
            "    order.persist [Client, Unset]");
    }

    [Test]
    public async Task HasSpan_Missing_IsTheReadmeText(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var capture = SpanCapture.ForSource(SourceName);
        Place(42);

        var text = await FailureText.OfAsync(async () => await Assert.That(capture).HasSpan("order.refund"));

        await Assert.That(text).IsEqualTo(
            "Expected to include a span named 'order.refund'\n" +
            "but none did; captured spans:\n" +
            "  order.place [Server, Ok]\n" +
            "    order.persist [Client, Unset]");
    }

    [Test]
    public async Task HasStatus_Mismatch_IsTheReadmeText(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var capture = SpanCapture.ForSource(SourceName);
        Place(42);
        var place = await Assert.That(capture).HasSpanWithTag("order.place", "order.id", 42);

        var text = await FailureText.OfAsync(async () => await Assert.That(place).HasStatus(ActivityStatusCode.Error));

        await Assert.That(text).IsEqualTo("Expected to have status Error\nbut it was Ok");
    }
}
