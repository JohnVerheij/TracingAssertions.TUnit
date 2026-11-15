#if NET11_0_OR_GREATER
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Tracing;

namespace TracingAssertions.TUnit.Tests;

/// <summary>
/// Real-use tests for an application that configures tracing with the .NET 11 <c>AddTracing</c> rules
/// (<c>Microsoft.Extensions.Diagnostics</c>): the capture is a listener of its own, so it keeps seeing the spans of
/// a source whether the application's rules enable it or disable it.
/// </summary>
[Category("Smoke")]
[Timeout(10_000)]
internal sealed class AddTracingTests
{
    /// <summary>Builds a provider whose one listener, <c>app</c>, records the names of the spans it receives,
    /// with <paramref name="configureRules"/> deciding which sources it is enabled for.</summary>
    private static ServiceProvider BuildApp(ConcurrentQueue<string> received, Action<ITracingBuilder> configureRules)
    {
        var services = new ServiceCollection();
        services.AddTracing(builder =>
        {
            builder.AddListener("app", listener =>
            {
                listener.Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
                listener.ActivityStopped = activity => received.Enqueue(activity.OperationName);
            });
            configureRules(builder);
        });
        var provider = services.BuildServiceProvider();
        // Resolving the factory starts the listeners the application registered, as application startup does.
        provider.GetRequiredService<ActivitySourceFactory>();
        return provider;
    }

    [Test]
    public async Task Capture_SeesTheSpan_WhenTheRulesEnableTheSource(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        const string sourceName = "TracingAssertions.TUnit.Tests.AddTracing.Enabled";
        using var source = new ActivitySource(sourceName);
        var received = new ConcurrentQueue<string>();
        using var app = BuildApp(received, builder => builder.EnableTracing(sourceName, null, "app"));
        using var capture = SpanCapture.ForSource(sourceName);

        using (source.StartActivity("op"))
        {
            // The span stops, and is captured, when the scope closes.
        }

        // The application's own listener receives the span, and so does the capture next to it.
        await Assert.That(received.ToArray()).IsEquivalentTo(["op"]);
        await Assert.That(capture).HasSpanCount(1);
        await Assert.That(capture).HasSpan("op");
    }

    [Test]
    public async Task Capture_SeesTheSpan_WhenTheRulesDisableTheSource(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        const string sourceName = "TracingAssertions.TUnit.Tests.AddTracing.Disabled";
        using var source = new ActivitySource(sourceName);
        var received = new ConcurrentQueue<string>();
        using var app = BuildApp(received, builder =>
        {
            builder.EnableTracing(sourceName, null, "app");
            builder.DisableTracing(sourceName, null, "app");
        });
        using var capture = SpanCapture.ForSource(sourceName);

        using (source.StartActivity("op"))
        {
            // The span stops, and is captured, when the scope closes.
        }

        // The application's listener is disabled for the source and records nothing, but the capture is a
        // listener of its own and still sees the span.
        await Assert.That(received.IsEmpty).IsTrue();
        await Assert.That(capture).HasSpanCount(1);
    }
}
#endif
