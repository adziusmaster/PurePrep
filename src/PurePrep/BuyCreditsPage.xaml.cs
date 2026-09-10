using PurePrep.Application;
using PurePrep.Controls;
using PurePrep.Localization;
using PurePrep.Services;

namespace PurePrep;

/// <summary>
/// The single, on-brand "buy Smart Credits" screen, reached from both the Home paywall and the
/// Settings credits row. Replaces the old bottom-sheet overlay (which looked different in each place
/// and could wrongly claim you were out of credits): a real page shows the live balance and lets the
/// pricing read like the rest of the app.
/// </summary>
public partial class BuyCreditsPage : ContentPage
{
    private readonly IBillingService? _billing;
    private readonly ISmartCreditsClient? _credits;
    private int _balance;
    private bool _loaded;

    public BuyCreditsPage(IBillingService? billing, ISmartCreditsClient? credits, int initialBalance = -1)
    {
        InitializeComponent();
        _billing = billing;
        _credits = credits;
        _balance = initialBalance;
        UpdateBalance();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded)
            return;
        _loaded = true;

        // Refresh the balance from the backend so the header is always accurate — this is what makes
        // the copy honest ("You have 42 Smart Credits") instead of a canned out-of-credits line.
        if (_credits is not null)
        {
            try
            {
                _balance = await _credits.GetBalanceAsync();
                UpdateBalance();
            }
            catch
            {
                // Offline: keep whatever we were given rather than blocking the purchase UI.
            }
        }

        await LoadPacksAsync();
    }

    private void UpdateBalance() =>
        BalanceLabel.Text = _balance <= 0
            ? AppResources.Get("CreditsBalanceZero")
            : AppResources.Format("CreditsBalanceFormat", _balance);

    private async Task LoadPacksAsync()
    {
        var supported = _billing?.IsSupported == true;
        PackSection.IsVisible = supported;
        TopUpButton.IsVisible = !supported;
        if (!supported || _billing is null)
            return;

        // Live, tax-inclusive Play prices (falls back to placeholder labels if unavailable).
        var packs = await _billing.GetPacksAsync();
        PacksContainer.Children.Clear();
        foreach (var pack in packs)
        {
            var captured = pack;
            PacksContainer.Children.Add(new CreditPackCard
            {
                Credits = pack.Credits,
                Price = pack.DisplayPrice,
                TapCommand = new Command(() => _ = PurchaseAsync(captured)),
            });
        }
    }

    private async Task PurchaseAsync(CreditPack pack)
    {
        if (_billing is null || _credits is null)
            return;

        SetBusy(true);
        try
        {
            var newBalance = await CreditPurchaseFlow.PurchaseAsync(_billing, _credits, pack.ProductId);
            if (newBalance is null)
                return; // user cancelled the Google purchase sheet

            _balance = newBalance.Value;
            UpdateBalance();
            await AppDialog.AlertAsync(this, AppResources.Get("BuyCredits"),
                AppResources.Format("RedeemSuccessFormat", pack.Credits), AppResources.Get("Ok"));
        }
        catch (Exception ex)
        {
            await AppDialog.AlertAsync(this, AppResources.Get("BuyCredits"),
                AppResources.Format("ErrCouldNotPurchaseFormat", ex.Message), AppResources.Get("Ok"));
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        BusyIndicator.IsRunning = busy;
        BusyIndicator.IsVisible = busy;
    }

    // Non-billing platforms (iOS/web preview) have no in-app purchase; the fallback button is inert
    // by design — real purchasing only exists on the Android build.
    private void OnTopUpTapped(object? sender, EventArgs e)
    {
    }

    private async void OnBackTapped(object? sender, EventArgs e) => await Navigation.PopAsync();
}
