using Microsoft.Maui.Layouts;
using PurePrep.Domain;

namespace PurePrep.Controls;

/// <summary>
/// Wraps its children left to right into at most <see cref="MaxLines"/> lines. The first
/// <see cref="LeadingCount"/> children are fixed (always shown, opening line one); the LAST child is
/// the overflow chip, placed only when the chips in between don't all fit, taking the last visible
/// slot. Chips that no longer fit get a zero-size, transparent, untappable arrange and leave the
/// accessibility tree. Which chips fit is decided by <see cref="ChipLineFit"/> from the measured
/// widths, so a given width always gives the same result.
/// </summary>
public sealed class LimitedWrapLayout : Layout
{
    public static readonly BindableProperty MaxLinesProperty = BindableProperty.Create(nameof(MaxLines), typeof(int), typeof(LimitedWrapLayout), 2,
        propertyChanged: (b, _, _) => ((LimitedWrapLayout)b).InvalidateMeasure());

    public static readonly BindableProperty SpacingProperty = BindableProperty.Create(nameof(Spacing), typeof(double), typeof(LimitedWrapLayout), 8d,
        propertyChanged: (b, _, _) => ((LimitedWrapLayout)b).InvalidateMeasure());

    public static readonly BindableProperty LeadingCountProperty = BindableProperty.Create(nameof(LeadingCount), typeof(int), typeof(LimitedWrapLayout), 0,
        propertyChanged: (b, _, _) => ((LimitedWrapLayout)b).InvalidateMeasure());

    public int MaxLines { get => (int)GetValue(MaxLinesProperty); set => SetValue(MaxLinesProperty, value); }

    /// <summary>Horizontal gap between chips and vertical gap between lines.</summary>
    public double Spacing { get => (double)GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }

    /// <summary>How many leading children are fixed items that are never hidden (e.g. an icon pair).</summary>
    public int LeadingCount { get => (int)GetValue(LeadingCountProperty); set => SetValue(LeadingCountProperty, value); }

    /// <summary>
    /// How many chips (excluding the overflow chip) are currently left out. The owner may preset it
    /// when it replaces the chips, so the next layout pass reports the real count again.
    /// </summary>
    public int HiddenCount { get; internal set; }

    /// <summary>
    /// Raised (posted after the layout pass, never during it) when <see cref="HiddenCount"/> changes,
    /// so the owner can update the overflow chip's "+N more" text.
    /// </summary>
    public event EventHandler? HiddenCountChanged;

    protected override ILayoutManager CreateLayoutManager() => new Manager(this);

    private void ReportHidden(int hidden)
    {
        if (hidden == HiddenCount)
            return;
        // Changing the overflow text re-measures; converges because a wider "+N more" can only hide
        // more chips (N grows, text width grows or stays), never flip back.
        Dispatcher.Dispatch(() =>
        {
            if (hidden == HiddenCount)
                return;
            HiddenCount = hidden;
            HiddenCountChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    private sealed class Manager(LimitedWrapLayout layout) : LayoutManager(layout)
    {
        private int _visible;

        private int Leading => Math.Clamp(layout.LeadingCount, 0, Math.Max(0, layout.Count - 1));
        private int ChipCount => Math.Max(0, layout.Count - 1 - Leading);

        public override Size Measure(double widthConstraint, double heightConstraint)
        {
            var padding = layout.Padding;
            var width = widthConstraint - padding.HorizontalThickness;
            foreach (var child in layout)
                child.Measure(width, double.PositiveInfinity);

            if (layout.Count == 0)
                return new Size(padding.HorizontalThickness, padding.VerticalThickness);

            var leading = LeadingItems().Select(c => c.DesiredSize.Width).ToList();
            var widths = Enumerable.Range(Leading, ChipCount).Select(i => layout[i].DesiredSize.Width).ToList();
            var overflowWidth = layout[layout.Count - 1].DesiredSize.Width;
            _visible = double.IsFinite(width)
                ? ChipLineFit.VisibleCount(leading, widths, overflowWidth, width, layout.Spacing, layout.MaxLines)
                : ChipCount;
            layout.ReportHidden(ChipCount - _visible);

            var (used, height) = Place(width, arrange: false, Point.Zero);
            var measuredWidth = double.IsFinite(widthConstraint) ? widthConstraint : used + padding.HorizontalThickness;
            return new Size(measuredWidth, height + padding.VerticalThickness);
        }

        public override Size ArrangeChildren(Rect bounds)
        {
            var padding = layout.Padding;
            Place(bounds.Width - padding.HorizontalThickness, arrange: true, new Point(bounds.X + padding.Left, bounds.Y + padding.Top));
            return bounds.Size;
        }

        // Leading items that take up space (a pair whose icons are both hidden measures 0 and would
        // otherwise still cost a gap).
        private IEnumerable<IView> LeadingItems() =>
            Enumerable.Range(0, Leading).Select(i => layout[i]).Where(c => c.DesiredSize.Width > 0);

        // Shown sequence: the leading items, the first _visible chips, then the overflow chip when
        // anything is hidden.
        private IEnumerable<IView> Shown()
        {
            foreach (var item in LeadingItems())
                yield return item;
            for (var i = 0; i < _visible; i++)
                yield return layout[Leading + i];
            if (_visible < ChipCount)
                yield return layout[layout.Count - 1];
        }

        // Greedy placement matching ChipLineFit.LineCount; returns the widest line and total height.
        private (double Width, double Height) Place(double width, bool arrange, Point origin)
        {
            var shown = Shown().ToList();
            if (arrange)
            {
                // A zero-size Border still draws a dot of its stroke, so left-out chips are also faded
                // out (opacity never affects measuring), made untappable, and taken out of the
                // accessibility tree with their pencil targets. Leading items are never touched.
                for (var i = Leading; i < layout.Count; i++)
                {
                    var child = layout[i];
                    var isShown = shown.Contains(child);
                    if (child is VisualElement element)
                    {
                        element.Opacity = isShown ? 1 : 0;
                        element.InputTransparent = !isShown;
                        AutomationProperties.SetIsInAccessibleTree(element, isShown);
                        AutomationProperties.SetExcludedWithChildren(element, !isShown);
                    }
                    if (!isShown)
                        child.Arrange(Rect.Zero);
                }
            }

            double x = 0, y = 0, lineHeight = 0, widest = 0;
            var first = true;
            foreach (var child in shown)
            {
                var size = child.DesiredSize;
                var w = double.IsFinite(width) ? Math.Min(size.Width, width) : size.Width;
                if (!first && x + layout.Spacing + w > width)
                {
                    y += lineHeight + layout.Spacing;
                    x = 0;
                    lineHeight = 0;
                }
                else if (!first)
                    x += layout.Spacing;

                if (arrange)
                    child.Arrange(new Rect(origin.X + x, origin.Y + y, w, size.Height));
                x += w;
                widest = Math.Max(widest, x);
                lineHeight = Math.Max(lineHeight, size.Height);
                first = false;
            }
            return (widest, first ? 0 : y + lineHeight);
        }
    }
}
