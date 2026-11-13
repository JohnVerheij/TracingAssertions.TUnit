using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace TracingAssertions;

/// <summary>
/// Renders the spans of a <see cref="SpanCapture"/> as an indented tree, for failure messages and for snapshot
/// tests. The text is stable between runs: it holds no span ids, trace ids or durations, children follow their
/// parent in start-time order (ties by operation name), and lines are joined with <c>\n</c>.
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
    /// <summary>The sibling order as a delegate created once, so each sort does not go through a cached method
    /// group.</summary>
    private static readonly Comparison<Activity> BySiblingOrder = CompareSiblings;

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

    /// <summary>Renders a block for a failure message: the tree indented under a heading line, or a sentence when
    /// nothing was captured.</summary>
    /// <param name="capture">The capture to describe.</param>
    /// <param name="options">The tree options, or <see langword="null"/> for the default.</param>
    /// <returns>Text that starts with <c>captured spans:</c> or <c>no spans were captured</c>.</returns>
    internal static string Describe(SpanCapture capture, SpanTreeOptions? options = null)
    {
        var tree = Render(capture, options);
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
        var stack = new Stack<(Activity Span, int Depth)>();
        for (var i = roots.Count - 1; i >= 0; i--)
        {
            stack.Push((roots[i], 0));
        }

        while (stack.Count > 0)
        {
            var (span, depth) = stack.Pop();
            AppendLine(sb, span, depth, keys);
            if (children.TryGetValue(NodeKey(span), out var kids))
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
    /// in the set is a top-level span.</summary>
    /// <param name="spans">The spans to arrange.</param>
    /// <returns>The sorted roots and, per node key, the sorted children.</returns>
    private static (List<Activity> Roots, Dictionary<string, List<Activity>> Children) BuildForest(
        IReadOnlyList<Activity> spans)
    {
        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach (var span in spans)
        {
            known.Add(NodeKey(span));
        }

        var roots = new List<Activity>();
        var children = new Dictionary<string, List<Activity>>(StringComparer.Ordinal);
        foreach (var span in spans)
        {
            var parentKey = ParentKey(span);
            if (parentKey is not null && known.Contains(parentKey))
            {
                if (!children.TryGetValue(parentKey, out var list))
                {
                    list = [];
                    children[parentKey] = list;
                }

                list.Add(span);
            }
            else
            {
                roots.Add(span);
            }
        }

        roots.Sort(BySiblingOrder);
        foreach (var list in children.Values)
        {
            list.Sort(BySiblingOrder);
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

    /// <summary>Orders siblings by start time, then by operation name (ordinal). The sort is stable only through
    /// these two keys; two spans equal in both keep an unspecified relative order.</summary>
    /// <param name="left">The first span.</param>
    /// <param name="right">The second span.</param>
    /// <returns>The comparison result.</returns>
    private static int CompareSiblings(Activity left, Activity right)
    {
        var byStart = left.StartTimeUtc.CompareTo(right.StartTimeUtc);
        return byStart is not 0
            ? byStart
            : string.CompareOrdinal(left.OperationName, right.OperationName);
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
