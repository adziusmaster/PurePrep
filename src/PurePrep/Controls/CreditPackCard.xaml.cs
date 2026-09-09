using System.Windows.Input;
using PurePrep.Localization;

namespace PurePrep.Controls;

/// <summary>
/// On-brand credit-pack card used in both the main paywall and the Settings buy sheet, so the
/// pack picker looks identical in both places. Exposes the pack's credits/price plus a tap command
/// and parameter; the whole card is the purchase button.
/// </summary>
public partial class CreditPackCard : ContentView
{
    public static readonly BindableProperty CreditsProperty = BindableProperty.Create(
        nameof(Credits), typeof(int), typeof(CreditPackCard), 0, propertyChanged: OnCreditsChanged);

    public static readonly BindableProperty PriceProperty = BindableProperty.Create(
        nameof(Price), typeof(string), typeof(CreditPackCard), string.Empty, propertyChanged: OnPriceChanged);

    public static readonly BindableProperty TapCommandProperty = BindableProperty.Create(
        nameof(TapCommand), typeof(ICommand), typeof(CreditPackCard));

    public static readonly BindableProperty TapCommandParameterProperty = BindableProperty.Create(
        nameof(TapCommandParameter), typeof(object), typeof(CreditPackCard));

    public CreditPackCard() => InitializeComponent();

    /// <summary>Number of Smart Credits in this pack.</summary>
    public int Credits
    {
        get => (int)GetValue(CreditsProperty);
        set => SetValue(CreditsProperty, value);
    }

    /// <summary>Live, tax-inclusive Play price string shown on the CTA badge.</summary>
    public string Price
    {
        get => (string)GetValue(PriceProperty);
        set => SetValue(PriceProperty, value);
    }

    /// <summary>Command invoked when the card is tapped.</summary>
    public ICommand? TapCommand
    {
        get => (ICommand?)GetValue(TapCommandProperty);
        set => SetValue(TapCommandProperty, value);
    }

    /// <summary>Parameter passed to <see cref="TapCommand"/> (typically the pack option).</summary>
    public object? TapCommandParameter
    {
        get => GetValue(TapCommandParameterProperty);
        set => SetValue(TapCommandParameterProperty, value);
    }

    private static void OnCreditsChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var card = (CreditPackCard)bindable;
        var credits = (int)newValue;
        card.CreditsChipLabel.Text = credits.ToString();
        card.CreditsLabel.Text = AppResources.Format("PackCreditsFormat", credits);
        card.UpdateAutomationName();
    }

    private static void OnPriceChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var card = (CreditPackCard)bindable;
        card.PriceLabel.Text = (string)newValue;
        card.UpdateAutomationName();
    }

    private void UpdateAutomationName() =>
        AutomationProperties.SetName(this, AppResources.Format("PackOptionFormat", Credits, Price));
}
