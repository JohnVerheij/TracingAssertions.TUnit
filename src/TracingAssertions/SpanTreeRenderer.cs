using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;

namespace TracingAssertions;

/// <summary>
/// Renders the spans of a <see cref="SpanCapture"/> as an indented tree, for failure messages and for snapshot
/// tests. The text holds no span ids, trace ids or durations, and lines are joined with <c>\n</c>. Siblings (and
/// the spans at the top level) are ordered by start time, then by operation name (ordinal), and spans equal in both
/// keep the order in which they were captured, so the same spans in the same capture order always give the same
/// text.
/// </summary>
/// <remarks>
/// <para>Each line is the operation name, then <c>[Kind, Status]</c>, then <c>{key=value, ...}</c> for the tags
/// selected through <see cref="SpanTreeOptions.TagKeys"/>. A child is indented two spaces deeper than its
/// parent. A span whose parent was not captured (a remote parent, another source, a span still running) is shown
/// at the top level. The text is the same for W3C and hierarchical activity ids.</para>
/// <para>The tree shows spans that have stopped; the capture holds nothing else. Tag values appear in their
/// invariant <see cref="object.ToString"/> form.</para>
/// </remarks>
public static class SpanTreeRenderer
{
    /// <summary>The text for a capture that holds no span; the failure text also compares against it to choose its
    /// wording.</summary>
    internal const string NoSpans = "<no spans>";

    /// <summary>Renders the spans captured so far as an indented tree, parents before their children and siblings in
    /// start-time order.</summary>
    /// <param name="capture">The capture to render.</param>
    /// <param name="options">What to show besides name, kind and status, or <see langword="null"/> for the
    /// default.</param>
    /// <returns>The tree text without a trailing newline, or <c>&lt;no spans&gt;</c> when nothing was
    /// captured.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="capture"/> is <see langword="null"/>.</exception>
    public static string Render(SpanCapture capture, SpanTreeOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(capture);
        return Render(capture.Captured, options);
    }

    /// <summary>Renders a block for a failure message from a snapshot of spans the caller already holds: the tree
    /// indented under a heading line, or a sentence when nothing was captured. The decision, the count and the tree
    /// in one message come from the same snapshot.</summary>
    /// <param name="spans">The snapshot to describe.</param>
    /// <param name="options">The tree options, or <see langword="null"/> for the default.</param>
    /// <returns>Text that starts with <c>captured spans:</c> or <c>no spans were captured</c>.</returns>
    internal static string Describe(IReadOnlyList<Activity> spans, SpanTreeOptions? options = null)
    {
        var tree = Render(spans, options);
        return string.Equals(tree, NoSpans, StringComparison.Ordinal)
            ? "no spans were captured"
            : "captured spans:\n  " + tree.Replace("\n", "\n  ", StringComparison.Ordinal);
    }

    /// <summary>Renders a snapshot of spans as an indented tree; the capture overload and the failure text both use
    /// this method.</summary>
    /// <param name="spans">The spans, in any order.</param>
    /// <param name="options">The options, or <see langword="null"/>.</param>
    /// <returns>The tree text.</returns>
    internal static string Render(IReadOnlyList<Activity> spans, SpanTreeOptions? options)
    {
        if (spans.Count is 0)
        {
            return NoSpans;
        }

        var (roots, children) = BuildForest(spans);
        var keys = options?.TagKeys ?? [];
        var sb = new StringBuilder();
        var stack = new Stack<(int Index, int Depth)>();
        for (var i = roots.Count - 1; i >= 0; i--)
        {
            stack.Push((roots[i], 0));
        }

        while (stack.Count > 0)
        {
            var (index, depth) = stack.Pop();
            AppendLine(sb, spans[index], depth, keys);
            if (children[index] is { } kids)
            {
                for (var i = kids.Count - 1; i >= 0; i--)
                {
                    stack.Push((kids[i], depth + 1));
                }
            }
        }

        return sb.ToString();
    }

    /// <summary>Splits the spans into the top-level spans and the children of each span; a span whose parent is not
    /// in the set is a top-level span. The parent ids of stopped spans cannot form a loop (a span id exists only once
    /// the span has started, and a parent id set after the start is ignored), so every span is reached from a
    /// top-level span exactly once.</summary>
    /// <param name="spans">The spans to arrange.</param>
    /// <returns>The roots and, per span index, the children (<see langword="null"/> for none); both in sibling
    /// order.</returns>
    private static (List<int> Roots, List<int>?[] Children) BuildForest(IReadOnlyList<Activity> spans)
    {
        var indexByKey = new Dictionary<string, int>(spans.Count, StringComparer.Ordinal);
        for (var i = 0; i < spans.Count; i++)
        {
            indexByKey.TryAdd(NodeKey(spans[i]), i);
        }

        // OrderBy is stable: spans equal in start time and name keep the order they have in the capture.
        var order = Enumerable.Range(0, spans.Count)
            .OrderBy(i => spans[i].StartTimeUtc)
            .ThenBy(i => spans[i].OperationName, StringComparer.Ordinal);

        var roots = new List<int>();
        var children = new List<int>?[spans.Count];
        foreach (var i in order)
        {
            if (ParentKey(spans[i]) is { } parentKey && indexByKey.TryGetValue(parentKey, out var parent))
            {
                (children[parent] ??= []).Add(i);
            }
            else
            {
                roots.Add(i);
            }
        }

        return (roots, children);
    }

    /// <summary>The key that identifies a span among the captured spans, tagged with the id format so a W3C and a
    /// hierarchical id can never collide.</summary>
    /// <param name="span">The span.</param>
    /// <returns>A format-tagged id.</returns>
    private static string NodeKey(Activity span)
        => SpanRelations.IsHierarchical(span)
            ? "h" + span.Id
            : string.Concat("w", span.TraceId.ToHexString(), span.SpanId.ToHexString());

    /// <summary>The key of the parent of a span, in the same form as <see cref="NodeKey(Activity)"/>; <see langword="null"/>
    /// for a root.</summary>
    /// <param name="span">The span.</param>
    /// <returns>The key of the parent, or <see langword="null"/> for a root.</returns>
    private static string? ParentKey(Activity span)
    {
        if (SpanRelations.IsRoot(span))
        {
            return null;
        }

        return SpanRelations.IsHierarchical(span)
            ? "h" + span.ParentId
            : string.Concat("w", span.TraceId.ToHexString(), span.ParentSpanId.ToHexString());
    }

    /// <summary>Writes a line break inside a name or a value as <c>\r</c> or <c>\n</c> text, so one span is always one
    /// line and a name can never forge another span line.</summary>
    /// <param name="text">The name or value.</param>
    /// <returns>The text with line breaks escaped.</returns>
    internal static string Escape(string text)
        => text.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);

    /// <summary>The invariant text of a tag value, with line breaks escaped. Numbers and dates never take the
    /// culture of the machine.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The escaped text.</returns>
    internal static string TextOf(object value)
        => Escape(string.Format(CultureInfo.InvariantCulture, "{0}", value));

    /// <summary>Appends one span line (indentation, operation name, kind and status, selected tags), preceded by a
    /// line break unless it is the first line.</summary>
    /// <param name="sb">The output.</param>
    /// <param name="span">The span.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <param name="keys">The tag keys to show.</param>
    private static void AppendLine(StringBuilder sb, Activity span, int depth, IReadOnlyList<string> keys)
    {
        if (sb.Length is not 0)
        {
            sb.Append('\n');
        }

        sb.Append(' ', depth * 2)
          .Append(Escape(span.OperationName))
          .Append(" [")
          .Append(span.Kind.ToString())
          .Append(", ")
          .Append(span.Status.ToString())
          .Append(']');
        AppendTags(sb, span, keys);
    }

    /// <summary>Appends the selected tags of a span as <c>{key=value, ...}</c>; a tag the span does not carry is
    /// left out, and nothing is appended when none is present.</summary>
    /// <param name="sb">The output.</param>
    /// <param name="span">The span.</param>
    /// <param name="keys">The tag keys to show.</param>
    private static void AppendTags(StringBuilder sb, Activity span, IReadOnlyList<string> keys)
    {
        var first = true;
        foreach (var key in keys)
        {
            var value = span.GetTagItem(key);
            if (value is null)
            {
                continue;
            }

            sb.Append(first ? " {" : ", ")
              .Append(Escape(key))
              .Append('=')
              .Append(TextOf(value));
            first = false;
        }

        if (!first)
        {
            sb.Append('}');
        }
    }
}
