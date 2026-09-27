using PurePrep.Presentation;

namespace PurePrep.Controls;

/// <summary>
/// The Lime "+ Add recipe" pill: floats over Home's library and anchors the empty-library state.
/// Taps are ignored while an import is running (the pill shows "Importing…" then).
/// </summary>
public partial class AddRecipeButton : ContentView
{
    public AddRecipeButton() => InitializeComponent();

    /// <summary>Raised when the pill is tapped and no import is in progress.</summary>
    public event EventHandler? Tapped;

    private void OnTapped(object? sender, TappedEventArgs e)
    {
        if (BindingContext is RecipeLibraryViewModel { IsImporting: true })
            return;
        Tapped?.Invoke(this, EventArgs.Empty);
    }
}
