namespace PurePrep.Controls;

/// <summary>
/// Focus Mode's ⋯ "Cooking options" sheet: read-aloud and keep-screen-on, each a full-width label +
/// switch row so both render properly (the old top-bar chip crammed them into ~28px of height).
/// Binds straight to the host page's BindingContext (FocusModeViewModel) — no properties of its own.
/// </summary>
public partial class OptionsSheet : ContentView
{
    public OptionsSheet() => InitializeComponent();

    public void Show()
    {
        IsVisible = true;
        Sheet.Show();
    }

    public async void Hide()
    {
        await Sheet.HideAsync();
        if (!Sheet.IsOpen)
            IsVisible = false;
    }

    private void OnSheetCancelled(object? sender, EventArgs e) => Hide();
}
