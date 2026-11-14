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
/// <remarks>A span that has not started has no ids yet (its id format is unknown, so it is treated as W3C, and its
/// trace id and span id are default). It is never the child of another span and shares no trace with any span, not
/// even with another unstarted one: default equals default, which is not a relationship. It is a root, as it has no
/// parent yet. The spans of a <see cref="SpanCapture"/> have all stopped, so this only concerns a span a test
/// creates itself.</remarks>
internal static class SpanRelations
{
    /// <summary>Whether <paramref name="span"/> uses hierarchical activity ids, the format in which the W3C trace
    /// and span ids are all default.</summary>
    /// <param name="span">The span to inspect.</param>
    /// <returns><see langword="true"/> for <see cref="ActivityIdFormat.Hierarchical"/>.</returns>
    internal static bool IsHierarchical(Activity span) => span.IdFormat is ActivityIdFormat.Hierarchical;

    /// <summary>Whether <paramref name="span"/> has a W3C trace id, which a span only has once it has started (the
    /// id of an unstarted span is default).</summary>
    /// <param name="span">The span to inspect.</param>
    /// <returns><see langword="true"/> when the trace id is not default.</returns>
    private static bool HasTrace(Activity span) => span.TraceId != default;

    /// <summary>Whether <paramref name="span"/> is a direct child of <paramref name="parent"/>, compared by the ids
    /// both spans use. A span that has not started is nobody's child.</summary>
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
            : HasTrace(span) && span.ParentSpanId == parent.SpanId && span.TraceId == parent.TraceId;
    }

    /// <summary>Whether two spans belong to the same trace, compared by the ids both spans use (trace id or root
    /// id). A span that has not started shares a trace with no span.</summary>
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
            : HasTrace(span) && span.TraceId == other.TraceId;
    }

    /// <summary>Whether <paramref name="span"/> has no parent, which is the case when it has no parent id. A W3C
    /// parent span id always shows up in the parent id, so this holds for both id formats; and a span with W3C ids
    /// and a parent id that is not a W3C id (for example a hierarchical one given to
    /// <see cref="Activity.SetParentId(string)"/>) has a parent although its parent span id is default.</summary>
    /// <param name="span">The span to inspect.</param>
    /// <returns><see langword="true"/> when the span has no parent. A span that has not started has none yet.</returns>
    internal static bool IsRoot(Activity span) => span.ParentId is null;
}
