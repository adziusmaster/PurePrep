using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using PurePrep.Domain;
using PurePrep.Localization;
using PurePrep.Services;

namespace PurePrep;

public partial class ShoppingListPage : ContentPage
{
    private readonly ShoppingListStore? _store;
    private readonly ObservableCollection<ShoppingRow> _rows = new();

    public ShoppingListPage()
    {
        InitializeComponent();
        _store = IPlatformApplication.Current?.Services.GetService<ShoppingListStore>();
        ItemsView.ItemsSource = _rows;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ReloadAsync();
    }

    protected override async void OnDisappearing()
    {
        // Tick state is edited in place, so it is persisted on the way out rather than on every tap.
        await PersistAsync();
        base.OnDisappearing();
    }

    private async Task ReloadAsync()
    {
        if (_store is null)
            return;

        _rows.Clear();
        foreach (var item in await _store.LoadAsync())
            _rows.Add(new ShoppingRow(item));

        UpdateChrome();
    }

    private Task PersistAsync() =>
        _store?.SaveAsync(_rows.Select(r => r.ToItem()).ToArray()) ?? Task.CompletedTask;

    private void UpdateChrome()
    {
        var remaining = _rows.Count(r => !r.IsChecked);
        CountLabel.Text = $"{remaining} / {_rows.Count}";
        ActionsRow.IsVisible = _rows.Count > 0;
    }

    private void OnItemTapped(object? sender, EventArgs e)
    {
        if (sender is Element { BindingContext: ShoppingRow row })
        {
            row.IsChecked = !row.IsChecked;
            UpdateChrome();
        }
    }

    private async void OnClearTickedClicked(object? sender, EventArgs e)
    {
        foreach (var row in _rows.Where(r => r.IsChecked).ToList())
            _rows.Remove(row);

        UpdateChrome();
        await PersistAsync();
    }

    private async void OnClearAllClicked(object? sender, EventArgs e)
    {
        if (_rows.Count == 0)
            return;

        var confirmed = await DisplayAlert(
            AppResources.Get("ClearAll"),
            AppResources.Get("ShoppingList"),
            AppResources.Get("ClearAll"),
            AppResources.Get("Cancel"));
        if (!confirmed)
            return;

        _rows.Clear();
        UpdateChrome();
        await PersistAsync();
    }

    private async void OnBackTapped(object? sender, EventArgs e) => await Navigation.PopAsync();

    /// <summary>Bindable wrapper so a tick updates the row without rebuilding the whole list.</summary>
    private sealed class ShoppingRow(ShoppingListItem item) : INotifyPropertyChanged
    {
        private bool _isChecked = item.IsChecked;

        public string Text { get; } = item.Text;
        public string? Source { get; } = item.Source;
        public bool HasSource => !string.IsNullOrWhiteSpace(Source);

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked == value) return;
                _isChecked = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TextDecoration));
            }
        }

        public TextDecorations TextDecoration => _isChecked ? TextDecorations.Strikethrough : TextDecorations.None;

        public ShoppingListItem ToItem() => new(Text, Source, _isChecked);

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
