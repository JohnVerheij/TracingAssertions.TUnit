using System;
using System.Diagnostics;

namespace TracingAssertions.Tests;

/// <summary>
/// Clears <see cref="Activity.Current"/> for the lifetime of the instance and restores it on dispose. Inside a
/// TUnit test the current activity is the test's own, a W3C activity, which every span started in the test
/// would have as its parent; a test that needs a true root, or hierarchical ids, starts from none.
/// </summary>
internal sealed class NoCurrentActivity : IDisposable
{
    // Held as object: the activity belongs to its owner, which disposes it, not to this scope.
    private readonly object? _previous = Activity.Current;

    /// <summary>Clears the current activity.</summary>
    internal NoCurrentActivity() => Activity.Current = null;

    /// <inheritdoc/>
    public void Dispose() => Activity.Current = (Activity?)_previous;
}
