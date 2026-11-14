using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace TracingAssertions.TUnit;

/// <summary>
/// The pieces of text the span assertions put after <c>but</c> in a failure. Names and values are in single
/// quotes, the message rule the family shares (an expectation placed in a generated interpolated string cannot
/// hold a double quote).
/// </summary>
internal static class SpanDescriptions
{
    /// <summary>Quotes a name or value in single quotes, the form every span failure text uses for names and
    /// values, with line breaks written out so the text keeps its shape.</summary>
    /// <param name="text">The text to quote.</param>
    /// <returns>The text in single quotes.</returns>
    internal static string Quote(string text) => "'" + SpanTreeRenderer.Escape(text) + "'";

    /// <summary>Quotes a value of any type by its invariant text, the same text the tree shows for a tag, so a
    /// failure and the tree agree on how a value reads.</summary>
    /// <param name="value">The value to quote.</param>
    /// <returns>The invariant text in single quotes, with line breaks written out.</returns>
    internal static string QuoteValue(object value) => "'" + SpanTreeRenderer.TextOf(value) + "'";

    /// <summary>Describes one span by operation name and identity, so a failure names the span and not only its
    /// id.</summary>
    /// <param name="span">The span to describe.</param>
    /// <returns>For example <c>'checkout' (span 0102030405060708)</c>.</returns>
    internal static string Span(Activity span)
        => string.Concat(Quote(span.OperationName), " (", Identity(span), ")");

    /// <summary>Describes the parent of a span by operation name and identity; the activity of the parent is read
    /// from <see cref="Activity.Parent"/>, which a remote parent does not have.</summary>
    /// <param name="span">The span whose parent is described.</param>
    /// <returns>The parent's name and identity when the parent activity is known, the bare id when it is not,
    /// or <c>no parent</c> for a root.</returns>
    internal static string ParentOf(Activity span)
    {
        if (SpanRelations.IsRoot(span))
        {
            return "no parent";
        }

        // A W3C span whose parent id is not a W3C id has a parent id but no parent span id to show.
        var id = SpanRelations.IsHierarchical(span) || span.ParentSpanId == default
            ? "id " + EscapeId(span.ParentId)
            : "span " + span.ParentSpanId.ToHexString();
        return span.Parent is { } parent
            ? string.Concat(Quote(parent.OperationName), " (", id, ")")
            : id;
    }

    /// <summary>Describes the trace of a span: the trace id for W3C ids, the root id for hierarchical ids, where the
    /// trace id is always default.</summary>
    /// <param name="span">The span.</param>
    /// <returns>The trace id (W3C) or root id (hierarchical).</returns>
    internal static string TraceOf(Activity span)
    {
        if (!SpanRelations.IsHierarchical(span))
        {
            return span.TraceId.ToHexString();
        }

        return span.RootId is null ? "none" : EscapeId(span.RootId);
    }

    /// <summary>Describes the children of a span for a failure message, as <c>no child spans</c> or
    /// <c>child spans 'a', 'b'</c>, so the text reads after <c>it had</c>.</summary>
    /// <param name="children">The direct children of the span.</param>
    /// <returns>The description.</returns>
    internal static string Children(IEnumerable<Activity> children)
    {
        var names = Names(children.Select(static child => child.OperationName), string.Empty);
        return names.Length is 0 ? "no child spans" : "child spans " + names;
    }

    /// <summary>Lists names in single quotes, separated by commas, or the given text when there is no name to
    /// list.</summary>
    /// <param name="names">The names to list.</param>
    /// <returns>The list, or <paramref name="none"/> when there are no names.</returns>
    /// <param name="none">The text for an empty list.</param>
    internal static string Names(IEnumerable<string> names, string none)
    {
        var sb = new StringBuilder();
        foreach (var name in names)
        {
            sb.Append(sb.Length is 0 ? string.Empty : ", ").Append(Quote(name));
        }

        return sb.Length is 0 ? none : sb.ToString();
    }

    /// <summary>The identity of a span: its span id for W3C ids, or its activity id for hierarchical ids, where the
    /// span id is always default.</summary>
    /// <param name="span">The span.</param>
    /// <returns>The identity text.</returns>
    private static string Identity(Activity span)
        => SpanRelations.IsHierarchical(span) ? "id " + EscapeId(span.Id) : "span " + span.SpanId.ToHexString();

    /// <summary>Writes an activity id so that it stays on one line. A hierarchical id, a root id and a parent id
    /// given through <see cref="Activity.SetParentId(string)"/> are caller-supplied strings and can hold a line
    /// break; a W3C hex id cannot.</summary>
    /// <param name="id">The id, or <see langword="null"/> for a span that has none.</param>
    /// <returns>The id with line breaks written out, or an empty string for <see langword="null"/>.</returns>
    private static string EscapeId(string? id) => id is null ? string.Empty : SpanTreeRenderer.Escape(id);
}
