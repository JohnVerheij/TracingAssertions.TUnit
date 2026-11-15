// What the README samples use without declaring it themselves: the code under test of the worked example and the
// span, capture and worker id a sample takes from the sample before it. Compiled by CI together with every C#
// sample of the READMEs (readme_samples.py of assertions-ci); never part of a package.
using System.Diagnostics;
using TracingAssertions;

namespace ReadmeSamples;

/// <summary>The code under test of the worked example, as the samples after it use it.</summary>
public static class OrderService
{
    /// <summary>Places an order, tracing the operation and the write.</summary>
    /// <param name="orderId">The order id.</param>
    public static void Place(int orderId)
    {
        using var place = new ActivitySource("Shop.Orders").StartActivity("order.place", ActivityKind.Server);
        place?.SetTag("order.id", orderId);
    }
}

/// <summary>The members every README sample can use.</summary>
public partial class Prelude
{
    /// <summary>The capture of the test.</summary>
    protected SpanCapture capture = SpanCapture.ForSource("Shop.Orders");

    /// <summary>The span of the operation under test.</summary>
    protected Activity place = new("order.place");

    /// <summary>A span under test.</summary>
    protected Activity span = new("order.place");

    /// <summary>The id of a worker.</summary>
    protected int workerId = 7;
}
