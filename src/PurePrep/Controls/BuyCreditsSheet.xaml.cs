using System.Collections;
using System.Windows.Input;

namespace PurePrep.Controls;

/// <summary>
/// The single, shared "buy Smart Credits" sheet. Both the main paywall and the Settings buy-credits
/// row host this control so the purchase menu is identical everywhere. Host pages provide the copy
/// (<see cref="Title"/>/<see cref="Subtitle"/>), the live packs, and the buy/dismiss commands; the
/// control owns the on-brand layout and the dismiss chrome (scrim + close button).
/// </summary>
public partial class BuyCreditsSheet : ContentView
{
    public static readonly BindableProperty IsOpenProperty = BindableProperty.Create(
        nameof(IsOpen), typeof(bool), typeof(BuyCreditsSheet), false);

    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title), typeof(string), typeof(BuyCreditsSheet), string.Empty);

    public static readonly BindableProperty SubtitleProperty = BindableProperty.Create(
        nameof(Subtitle), typeof(string), typeof(BuyCreditsSheet), string.Empty);

    public static readonly BindableProperty PacksProperty = BindableProperty.Create(
        nameof(Packs), typeof(IEnumerable), typeof(BuyCreditsSheet));

    public static readonly BindableProperty BuyCommandProperty = BindableProperty.Create(
        nameof(BuyCommand), typeof(ICommand), typeof(BuyCreditsSheet));

    public static readonly BindableProperty TopUpCommandProperty = BindableProperty.Create(
        nameof(TopUpCommand), typeof(ICommand), typeof(BuyCreditsSheet));

    public static readonly BindableProperty DismissCommandProperty = BindableProperty.Create(
        nameof(DismissCommand), typeof(ICommand), typeof(BuyCreditsSheet));

    public static readonly BindableProperty IsBusyProperty = BindableProperty.Create(
        nameof(IsBusy), typeof(bool), typeof(BuyCreditsSheet), false);

    public static readonly BindableProperty IsBillingSupportedProperty = BindableProperty.Create(
        nameof(IsBillingSupported), typeof(bool), typeof(BuyCreditsSheet), true);

    public BuyCreditsSheet() => InitializeComponent();

    /// <summary>Whether the sheet (and its scrim) is shown.</summary>
    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>Headline shown at the top of the sheet (context-specific copy).</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Supporting line under the headline.</summary>
    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    /// <summary>Live credit packs to offer (items expose Credits and DisplayPrice).</summary>
    public IEnumerable? Packs
    {
        get => (IEnumerable?)GetValue(PacksProperty);
        set => SetValue(PacksProperty, value);
    }

    /// <summary>Command invoked with the tapped pack as its parameter.</summary>
    public ICommand? BuyCommand
    {
        get => (ICommand?)GetValue(BuyCommandProperty);
        set => SetValue(BuyCommandProperty, value);
    }

    /// <summary>Fallback command for platforms without in-app billing.</summary>
    public ICommand? TopUpCommand
    {
        get => (ICommand?)GetValue(TopUpCommandProperty);
        set => SetValue(TopUpCommandProperty, value);
    }

    /// <summary>Command invoked when the sheet is dismissed (scrim tap or close button).</summary>
    public ICommand? DismissCommand
    {
        get => (ICommand?)GetValue(DismissCommandProperty);
        set => SetValue(DismissCommandProperty, value);
    }

    /// <summary>Shows the busy spinner while a purchase is in flight.</summary>
    public bool IsBusy
    {
        get => (bool)GetValue(IsBusyProperty);
        set => SetValue(IsBusyProperty, value);
    }

    /// <summary>Whether in-app billing is available; hides the pack list when false.</summary>
    public bool IsBillingSupported
    {
        get => (bool)GetValue(IsBillingSupportedProperty);
        set => SetValue(IsBillingSupportedProperty, value);
    }
}
