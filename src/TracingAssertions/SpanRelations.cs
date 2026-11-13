using System;
using System.Diagnostics;

namespace TracingAssertions;

/// <summary>
/// The single definition of how spans relate, shared by <see cref="SpanCapture.ChildrenOf(Activity)"/>, the
/// tree renderer and the TUnit assertions. Spans with W3C activity ids (the .NET default) are related by
/// <see cref="Activity.TraceId"/>, <see cref="Activity.SpanId"/> and <see cref="Activity.ParentSpanId"/>; spans
/// with <see cref="ActivityIdFormat.Hierarchical"/> ids, where those three are always default, are related by
/// <see cref="Activity.Id"/>, <see cref="Activity.ParentId"/> and <see cref="Activity.RootId"/>. A hierarchical
/// span is never related to a W3C span.
/// </summary>
internal static class SpanRelations
{
    /// <summary>Whether <paramref name="span"/> uses hierarchical activity ids, the format in which the W3C trace
    /// and span ids are all default.</summary>
    /// <param name="span">The span to inspect.</param>
    /// <returns><see langword="true"/> for <see cref="ActivityIdFormat.Hierarchical"/>.</returns>
    internal static bool IsHierarchical(Activity span) => span.IdFormat is ActivityIdFormat.Hierarchical;

    /// <summary>Whether <paramref name="span"/> is a direct child of <paramref name="parent"/>, compared by the ids
    /// both spans use.</summary>
    /// <param name="span">The candidate child.</param>
    /// <param name="parent">The candidate parent.</param>
    /// <returns><see langword="true"/> when the parent and trace relationship holds in the ids both spans use.</returns>
    internal static bool IsChildOf(Activity span, Activity parent)
    {
        var hierarchical = IsHierarchical(span);
        if (hierarchical != IsHierarchical(parent))
        {
            return false;
        }

        return hierarchical
            ? span.ParentId is not null && string.Equals(span.ParentId, parent.Id, StringComparison.Ordinal)
            : span.ParentSpanId == parent.SpanId && span.TraceId == parent.TraceId;
    }

    /// <summary>Whether two spans belong to the same trace, compared by the ids both spans use (trace id or root
    /// id).</summary>
    /// <param name="span">The first span.</param>
    /// <param name="other">The second span.</param>
    /// <returns><see langword="true"/> when they share the trace id (W3C) or the root id (hierarchical).</returns>
    internal static bool SharesTrace(Activity span, Activity other)
    {
        var hierarchical = IsHierarchical(span);
        if (hierarchical != IsHierarchical(other))
        {
            return false;
        }

        return hierarchical
            ? span.RootId is not null && string.Equals(span.RootId, other.RootId, StringComparison.Ordinal)
            : span.TraceId == other.TraceId;
    }

    /// <summary>Whether <paramref name="span"/> has no parent, compared by the ids the span uses (parent span id or
    /// parent id).</summary>
    /// <param name="span">The span to inspect.</param>
    /// <returns><see langword="true"/> when the parent span id (W3C) or parent id (hierarchical) is absent.</returns>
    internal static bool IsRoot(Activity span)
        => IsHierarchical(span) ? span.ParentId is null : span.ParentSpanId == default;
}
