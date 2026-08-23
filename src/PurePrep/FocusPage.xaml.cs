using PurePrep.Domain;
using PurePrep.Presentation;

namespace PurePrep;

public partial class FocusPage : ContentPage
{
    public FocusPage(ParsedRecipe recipe)
    {
        InitializeComponent();
        BindingContext = new FocusModeViewModel(recipe);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        DeviceDisplay.Current.KeepScreenOn = true;
    }

    protected override void OnDisappearing()
    {
        DeviceDisplay.Current.KeepScreenOn = false;
        base.OnDisappearing();
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await Navigation.PopAsync();
}
