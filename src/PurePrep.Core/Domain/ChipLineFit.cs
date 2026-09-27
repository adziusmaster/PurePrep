namespace PurePrep.Domain;

/// <summary>
/// How many chips of a left-to-right wrapping row fit in a fixed number of lines. When they don't
/// all fit, the last visible slot goes to an overflow chip ("+N more"), so the answer is the
/// largest prefix that still leaves room for it. Pure arithmetic on measured widths — the layout
/// measures, this decides — so the same widths always give the same count. Leading items (Focus
/// Mode's read/voice icon pair) open line one and are never hidden; they only consume its width.
/// </summary>
public static class ChipLineFit
{
    /// <returns>
    /// The number of chips (after the <paramref name="leadingWidths"/> items) to show: all of them
    /// when they fit in <paramref name="maxLines"/> (or while the width is still unknown), otherwise
    /// the largest count that fits together with an overflow chip of <paramref name="overflowWidth"/>
    /// after it (possibly 0).
    /// </returns>
    public static int VisibleCount(IReadOnlyList<double> leadingWidths, IReadOnlyList<double> widths, double overflowWidth,
        double availableWidth, double spacing, int maxLines)
    {
        if (availableWidth <= 0 || maxLines <= 0 || LineCount(leadingWidths.Concat(widths), availableWidth, spacing) <= maxLines)
            return widths.Count;

        for (var visible = widths.Count - 1; visible > 0; visible--)
        {
            if (LineCount(leadingWidths.Concat(widths.Take(visible)).Append(overflowWidth), availableWidth, spacing) <= maxLines)
                return visible;
        }
        return 0;
    }

    /// <summary>
    /// Greedy line count: each chip joins the current line when it fits after the spacing, else
    /// starts a new one. A chip wider than the line is clamped to take a line of its own.
    /// </summary>
    public static int LineCount(IEnumerable<double> widths, double availableWidth, double spacing)
    {
        var lines = 0;
        var used = 0.0;
        foreach (var raw in widths)
        {
            var width = Math.Min(raw, availableWidth);
            if (lines == 0 || used + spacing + width > availableWidth)
            {
                lines++;
                used = width;
            }
            else
                used += spacing + width;
        }
        return lines;
    }
}
