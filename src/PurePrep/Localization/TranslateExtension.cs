using System.Globalization;
using Microsoft.Maui.Controls.Xaml;

namespace PurePrep.Localization;

/// <summary>
/// XAML markup extension: <c>Text="{loc:Translate Import}"</c> resolves a localized string
/// from <see cref="AppResources"/> for the current UI culture at page-load time.
/// </summary>
[ContentProperty(nameof(Key))]
[AcceptEmptyServiceProvider]
public sealed class TranslateExtension : IMarkupExtension<string>
{
    public string Key { get; set; } = string.Empty;

    public string ProvideValue(IServiceProvider serviceProvider) =>
        string.IsNullOrEmpty(Key) ? string.Empty : AppResources.Get(Key);

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) =>
        ProvideValue(serviceProvider);
}
