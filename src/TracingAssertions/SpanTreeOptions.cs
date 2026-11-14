using System;
using System.Collections.Generic;

namespace TracingAssertions;

/// <summary>Options for <see cref="SpanTreeRenderer.Render(SpanCapture, SpanTreeOptions?)"/>; the type can grow
/// options without changing the renderer's signature.</summary>
public sealed class SpanTreeOptions
{
    private IReadOnlyList<string> _tagKeys = [];

    /// <summary>The tag keys to show on each span, in this order. A span that does not carry a listed tag omits
    /// it. The default is empty: only the name, kind and status are shown. The options keep a copy of the list
    /// that is assigned, so changing that list afterwards has no effect.</summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A key in the value is <see langword="null"/>.</exception>
    public IReadOnlyList<string> TagKeys
    {
        get => _tagKeys;
        init
        {
            ArgumentNullException.ThrowIfNull(value);

            // Copy first, then check the copy: the caller's list can change afterwards, and a null key added later
            // would otherwise fail the render.
            string[] keys = [.. value];
            if (Array.Exists(keys, static key => key is null))
            {
                throw new ArgumentException("A tag key must not be null.", nameof(value));
            }

            _tagKeys = keys;
        }
    }
}
