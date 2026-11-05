using System.Diagnostics;

namespace Smoke.Consumer;

/// <summary>
/// The code under test of the README's worked example: one operation traced as a <c>Server</c> span
/// with a <c>Client</c> child span for the database write.
/// </summary>
internal static class OrderService
{
    /// <summary>The name of the activity source the service emits on.</summary>
    public const string SourceName = "Shop.Orders";

    private static readonly ActivitySource Source = new(SourceName);

    /// <summary>Places an order, tracing the operation and the write.</summary>
    /// <param name="orderId">The order id, recorded as the <c>order.id</c> tag.</param>
    public static void Place(int orderId)
    {
        using var place = Source.StartActivity("order.place", ActivityKind.Server);
        place?.SetTag("order.id", orderId);

        using (Source.StartActivity("order.persist", ActivityKind.Client))
        {
            // The write itself is out of scope for the example.
        }

        place?.SetStatus(ActivityStatusCode.Ok);
    }
}
