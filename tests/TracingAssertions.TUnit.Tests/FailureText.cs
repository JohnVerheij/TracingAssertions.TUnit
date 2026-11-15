using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TUnit.Assertions.Exceptions;

namespace TracingAssertions.TUnit.Tests;

/// <summary>
/// Helpers that turn a failing assertion into the exact text TUnit shows, so a test can pin the whole message.
/// </summary>
internal static class FailureText
{
    /// <summary>Runs an assertion that must fail and returns its message without the trailing
    /// <c>at Assert.That(...)</c> line, with line endings normalized and random ids replaced by
    /// placeholders.</summary>
    /// <param name="assertion">The assertion to run.</param>
    /// <returns>The normalized message.</returns>
    internal static async Task<string> OfAsync(Func<Task> assertion)
    {
        var exception = await Assert.That(assertion).Throws<AssertionException>();
        var message = exception!.Message.ReplaceLineEndings("\n");
        var at = message.IndexOf("\n\nat ", StringComparison.Ordinal);
        if (at >= 0)
        {
            message = message[..at];
        }

        message = Regex.Replace(message, "[0-9a-f]{32}", "<trace>", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);
        return Regex.Replace(message, "[0-9a-f]{16}", "<span>", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);
    }
}
