using System.Collections;
using PurePrep.Localization;

namespace PurePrep.Controls;

/// <summary>
/// Focus Mode's controls flow: the fixed <see cref="Leading"/> view (the read/voice icon pair) first,
/// then one chip per item of <see cref="Timers"/> (built from <see cref="ChipTemplate"/>), all wrapped
/// to two lines by <see cref="LimitedWrapLayout"/>. When the chips don't all fit, a "+N more" chip
/// takes the last slot and raises <see cref="MoreTapped"/> so the page can list every timer of the
/// step. The leading view is never hidden, so 1–2 short timers stay on one line beside it.
/// </summary>
public partial class TimerChipsFlow : ContentView
{
    public static readonly BindableProperty TimersProperty = BindableProperty.Create(nameof(Timers), typeof(IEnumerable), typeof(TimerChipsFlow),
        propertyChanged: (b, _, _) => ((TimerChipsFlow)b).Rebuild());

    public static readonly BindableProperty ChipTemplateProperty = BindableProperty.Create(nameof(ChipTemplate), typeof(DataTemplate), typeof(TimerChipsFlow),
        propertyChanged: (b, _, _) => ((TimerChipsFlow)b).Rebuild());

    public static readonly BindableProperty LeadingProperty = BindableProperty.Create(nameof(Leading), typeof(View), typeof(TimerChipsFlow),
        propertyChanged: (b, oldValue, newValue) => ((TimerChipsFlow)b).SetLeading(oldValue as View, newValue as View));

    public TimerChipsFlow() => InitializeComponent();

    public IEnumerable? Timers { get => (IEnumerable?)GetValue(TimersProperty); set => SetValue(TimersProperty, value); }
    public DataTemplate? ChipTemplate { get => (DataTemplate?)GetValue(ChipTemplateProperty); set => SetValue(ChipTemplateProperty, value); }

    /// <summary>Fixed view opening line one ahead of the chips; inherits this control's BindingContext.</summary>
    public View? Leading { get => (View?)GetValue(LeadingProperty); set => SetValue(LeadingProperty, value); }

    /// <summary>Raised when the "+N more" chip is tapped.</summary>
    public event EventHandler? MoreTapped;

    private void SetLeading(View? oldView, View? newView)
    {
        if (oldView is not null)
            Flow.Remove(oldView);
        if (newView is not null)
            Flow.Insert(0, newView);
        Flow.LeadingCount = newView is null ? 0 : 1;
    }

    // A new step (or template) replaces every chip; the leading view stays first, MoreChip last.
    private void Rebuild()
    {
        while (Flow.Count > Flow.LeadingCount + 1)
            Flow.RemoveAt(Flow.LeadingCount);

        var items = Timers?.Cast<object>().ToList() ?? [];
        if (ChipTemplate is { } template)
        {
            foreach (var item in items)
            {
                var content = template is DataTemplateSelector selector ? selector.SelectTemplate(item, this).CreateContent() : template.CreateContent();
                if (content is View chip)
                {
                    chip.BindingContext = item;
                    Flow.Insert(Flow.Count - 1, chip);
                }
            }
        }

        // Until the layout reports the real count, size the overflow chip for the widest case.
        Flow.HiddenCount = items.Count;
        SetMoreText(items.Count);
    }

    private void OnHiddenCountChanged(object? sender, EventArgs e) => SetMoreText(Flow.HiddenCount);

    private void SetMoreText(int hidden)
    {
        var text = AppResources.Format("MoreTimersFormat", hidden);
        MoreLabel.Text = text;
        AutomationProperties.SetName(MoreChip, text);
    }

    private void OnMoreTapped(object? sender, TappedEventArgs e) => MoreTapped?.Invoke(this, EventArgs.Empty);
}
