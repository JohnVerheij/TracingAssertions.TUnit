using System;
using System.Collections.Generic;

namespace TracingAssertions;

/// <summary>Options for <see cref="SpanTreeRenderer.Render(SpanCapture, SpanTreeOptions?)"/>; the type can grow
/// options without changing the renderer's signature.</summary>
public sealed class SpanTreeOptions
{
    private IReadOnlyList<string> _tagKeys = [];

    /// <summary>The tag keys to show on each span, in this order. A span that does not carry a listed tag omits
    /// it. The default is empty: only the name, kind and status are shown.</summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A key in the value is <see langword="null"/>.</exception>
    public IReadOnlyList<string> TagKeys
    {
        get => _tagKeys;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            for (var i = 0; i < value.Count; i++)
            {
                if (value[i] is null)
                {
                    throw new ArgumentException("A tag key must not be null.", nameof(value));
                }
            }

            _tagKeys = value;
        }
    }
}
