using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using PurePrep.Application;
using PurePrep.Localization;

namespace PurePrep;

public partial class LanguagePacksPage : ContentPage
{
    private readonly ITranslationService? _service;
    private readonly ObservableCollection<LanguagePackItem> _items = new();

    public LanguagePacksPage()
    {
        InitializeComponent();
        _service = IPlatformApplication.Current?.Services.GetService<ITranslationService>();
        PacksList.ItemsSource = _items;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        _items.Clear();
        if (_service is null || !_service.IsSupported)
            return;

        IReadOnlyList<string> downloaded;
        try
        {
            downloaded = await _service.GetDownloadedModelsAsync();
        }
        catch
        {
            downloaded = new[] { "en" };
        }

        foreach (var lang in LocalizationService.Supported
                     .Where(l => l.Code.Length == 2 && _service.SupportedLanguageCodes.Contains(l.Code)))
        {
            var bundled = string.Equals(lang.Code, "en", StringComparison.OrdinalIgnoreCase);
            _items.Add(new LanguagePackItem(lang.Code, lang.NativeName, bundled)
            {
                IsDownloaded = bundled || downloaded.Contains(lang.Code),
            });
        }
    }

    private LanguagePackItem? Find(object? commandParameter) =>
        _items.FirstOrDefault(i => i.Code == commandParameter as string);

    private async void OnDownloadClicked(object? sender, EventArgs e)
    {
        var item = Find((sender as Button)?.CommandParameter);
        if (item is null || _service is null)
            return;

        item.IsBusy = true;
        try
        {
            await _service.DownloadModelAsync(item.Code);
            item.IsDownloaded = true;
        }
        catch (Exception ex)
        {
            await DisplayAlert(AppResources.Get("SectionLanguagePacks"),
                AppResources.Format("LanguagePackDownloadFailedFormat", ex.Message), AppResources.Get("Ok"));
        }
        finally
        {
            item.IsBusy = false;
        }
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        var item = Find((sender as Button)?.CommandParameter);
        if (item is null || _service is null)
            return;

        var ok = await DisplayAlert(AppResources.Get("SectionLanguagePacks"),
            AppResources.Format("LanguagePackDeletePromptFormat", item.Name),
            AppResources.Get("Delete"), AppResources.Get("Cancel"));
        if (!ok)
            return;

        item.IsBusy = true;
        try
        {
            await _service.DeleteModelAsync(item.Code);
            item.IsDownloaded = false;
        }
        catch (Exception ex)
        {
            await DisplayAlert(AppResources.Get("SectionLanguagePacks"),
                AppResources.Format("LanguagePackDeleteFailedFormat", ex.Message), AppResources.Get("Ok"));
        }
        finally
        {
            item.IsBusy = false;
        }
    }

    private void OnBackTapped(object? sender, EventArgs e) => _ = Navigation.PopAsync();

    private sealed class LanguagePackItem : INotifyPropertyChanged
    {
        private bool _isDownloaded;
        private bool _isBusy;

        public LanguagePackItem(string code, string name, bool isBundled)
        {
            Code = code;
            Name = name;
            IsBundled = isBundled;
        }

        public string Code { get; }
        public string Name { get; }
        public bool IsBundled { get; }

        public bool IsDownloaded
        {
            get => _isDownloaded;
            set { if (_isDownloaded != value) { _isDownloaded = value; Notify(nameof(IsDownloaded)); NotifyDerived(); } }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { if (_isBusy != value) { _isBusy = value; Notify(nameof(IsBusy)); NotifyDerived(); } }
        }

        public bool CanDownload => !IsBundled && !IsDownloaded && !IsBusy;
        public bool CanDelete => !IsBundled && IsDownloaded && !IsBusy;

        public string StatusText => IsBundled
            ? AppResources.Get("LanguagePackBundledDesc")
            : IsDownloaded
                ? AppResources.Get("LanguagePackReady")
                : AppResources.Get("LanguagePackNotDownloaded");

        private void NotifyDerived()
        {
            Notify(nameof(CanDownload));
            Notify(nameof(CanDelete));
            Notify(nameof(StatusText));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void Notify([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
