using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using PurePrep.Localization;

namespace PurePrep.Services;

/// <summary>
/// In-app, on-brand replacements for MAUI's native <c>DisplayAlert</c>/<c>DisplayPromptAsync</c>/
/// <c>DisplayActionSheet</c>. The platform dialogs looked like bare Android system pop-ups, jarringly
/// different from the rest of the app; these render a themed card (Surface/Lime, rounded, dark) as an
/// overlay on the current page instead, so every prompt matches the app's look.
/// </summary>
public static class AppDialog
{
    // The stack of dialogs currently on screen, newest last. Used so the hardware back button can
    // dismiss the top-most dialog instead of popping the page underneath it.
    private static readonly List<Overlay> Open = new();

    /// <summary>True while at least one styled dialog is on screen.</summary>
    public static bool IsOpen => Open.Count > 0;

    /// <summary>
    /// Called by the Android back handler: cancels the top-most open dialog (returning its cancel
    /// result) and reports whether it consumed the press.
    /// </summary>
    public static bool TryHandleBack()
    {
        if (Open.Count == 0)
            return false;
        Open[^1].Cancel();
        return true;
    }

    /// <summary>A simple message with a single dismiss button.</summary>
    public static Task AlertAsync(ContentPage page, string title, string? message, string dismiss)
    {
        var overlay = new Overlay(page, title, message);
        overlay.AddButton(dismiss, primary: true, () => overlay.Complete(true));
        overlay.CancelResult = () => overlay.Complete(true);
        return overlay.ShowAsync();
    }

    /// <summary>A yes/no confirmation. Resolves true when <paramref name="accept"/> is chosen.</summary>
    public static async Task<bool> ConfirmAsync(ContentPage page, string title, string? message, string accept, string cancel)
    {
        var overlay = new Overlay(page, title, message);
        var tcs = new TaskCompletionSource<bool>();
        overlay.AddButton(cancel, primary: false, () => overlay.Complete(false));
        overlay.AddButton(accept, primary: true, () => overlay.Complete(true));
        overlay.CancelResult = () => overlay.Complete(false);
        overlay.Resolved = result => tcs.TrySetResult(result is bool b && b);
        await overlay.ShowAsync();
        return await tcs.Task;
    }

    /// <summary>A single-line text prompt. Returns the entered text, or null when cancelled.</summary>
    public static async Task<string?> PromptAsync(ContentPage page, string title, string? message, string accept, string cancel,
        string? placeholder = null, string? initialValue = null, int maxLength = -1, Keyboard? keyboard = null)
    {
        var overlay = new Overlay(page, title, message);
        var entry = overlay.AddEntry(placeholder, initialValue, maxLength, keyboard);
        var tcs = new TaskCompletionSource<string?>();
        overlay.AddButton(cancel, primary: false, () => overlay.Complete(null));
        overlay.AddButton(accept, primary: true, () => overlay.Complete(entry.Text ?? string.Empty));
        overlay.CancelResult = () => overlay.Complete(null);
        overlay.Resolved = result => tcs.TrySetResult(result as string);
        await overlay.ShowAsync();
        return await tcs.Task;
    }

    /// <summary>
    /// A list of choices, mirroring <c>DisplayActionSheet</c>: returns the chosen option's text, or
    /// null when cancelled (back / scrim / the cancel button).
    /// </summary>
    public static async Task<string?> ChooseAsync(ContentPage page, string title, string cancel, params string[] options)
    {
        var overlay = new Overlay(page, title, null);
        var tcs = new TaskCompletionSource<string?>();
        foreach (var option in options)
        {
            var captured = option;
            overlay.AddChoice(captured, () => overlay.Complete(captured));
        }
        if (!string.IsNullOrEmpty(cancel))
            overlay.AddButton(cancel, primary: false, () => overlay.Complete(null));
        overlay.CancelResult = () => overlay.Complete(null);
        overlay.Resolved = result => tcs.TrySetResult(result as string);
        await overlay.ShowAsync();
        return await tcs.Task;
    }

    // The visual tree + lifecycle for one dialog. Kept internal to this file so the public surface
    // stays the four intent-named helpers above.
    private sealed class Overlay
    {
        private readonly ContentPage _page;
        private readonly Grid _root;
        private readonly VerticalStackLayout _actions;
        private readonly VerticalStackLayout _choices;
        private readonly TaskCompletionSource<bool> _shown = new();

        public System.Action? CancelResult;
        public System.Action<object?>? Resolved;

        public Overlay(ContentPage page, string title, string? message)
        {
            _page = page;

            var card = new VerticalStackLayout { Spacing = 14 };

            if (!string.IsNullOrEmpty(title))
            {
                var titleLabel = new Label
                {
                    Text = title,
                    FontSize = 18,
                    FontAttributes = FontAttributes.Bold,
                    LineBreakMode = LineBreakMode.WordWrap,
                };
                titleLabel.SetDynamicResource(Label.TextColorProperty, "Ink");
                card.Add(titleLabel);
            }

            if (!string.IsNullOrWhiteSpace(message))
            {
                var messageLabel = new Label
                {
                    Text = message,
                    FontSize = 14,
                    LineHeight = 1.3,
                };
                messageLabel.SetDynamicResource(Label.TextColorProperty, "Muted");
                card.Add(messageLabel);
            }

            _choices = new VerticalStackLayout { Spacing = 8 };
            card.Add(_choices);

            _actions = new VerticalStackLayout { Spacing = 10 };
            card.Add(_actions);

            var border = new Border
            {
                Padding = 22,
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = 22 },
                MaximumWidthRequest = 440,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                Margin = new Thickness(22, 0),
                Content = card,
            };
            border.SetDynamicResource(Border.BackgroundColorProperty, "Surface");
            border.SetDynamicResource(Border.StrokeProperty, "Line");

            var scrim = new BoxView { Color = Color.FromArgb("#99000000") };
            scrim.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(Cancel) });

            _root = new Grid { InputTransparent = false };
            _root.Add(scrim);
            _root.Add(border);
        }

        public Entry AddEntry(string? placeholder, string? initialValue, int maxLength, Keyboard? keyboard)
        {
            var entry = new Entry
            {
                Placeholder = placeholder,
                Text = initialValue,
                Keyboard = keyboard ?? Keyboard.Default,
            };
            if (maxLength > 0)
                entry.MaxLength = maxLength;
            entry.SetDynamicResource(Entry.TextColorProperty, "Ink");
            entry.SetDynamicResource(Entry.PlaceholderColorProperty, "Muted");
            entry.SetDynamicResource(Entry.BackgroundColorProperty, "BgElevated");

            var wrap = new Border
            {
                Padding = new Thickness(12, 2),
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                Content = entry,
            };
            wrap.SetDynamicResource(Border.BackgroundColorProperty, "BgElevated");
            wrap.SetDynamicResource(Border.StrokeProperty, "Line");
            _choices.Add(wrap);
            return entry;
        }

        public void AddChoice(string text, System.Action onTap)
        {
            var label = new Label
            {
                Text = text,
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center,
            };
            label.SetDynamicResource(Label.TextColorProperty, "Ink");

            var row = new Border
            {
                Padding = new Thickness(16, 14),
                StrokeThickness = 1,
                MinimumHeightRequest = 52,
                StrokeShape = new RoundRectangle { CornerRadius = 14 },
                Content = label,
            };
            row.SetDynamicResource(Border.BackgroundColorProperty, "BgElevated");
            row.SetDynamicResource(Border.StrokeProperty, "Line");
            row.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(onTap) });
            _choices.Add(row);
        }

        public void AddButton(string text, bool primary, System.Action onTap)
        {
            var button = new Button
            {
                Text = text,
                FontAttributes = FontAttributes.Bold,
                FontSize = 15,
                HeightRequest = 52,
                CornerRadius = 14,
            };
            if (primary)
            {
                button.SetDynamicResource(Button.BackgroundColorProperty, "Lime");
                button.SetDynamicResource(Button.TextColorProperty, "LimeInk");
            }
            else
            {
                button.SetDynamicResource(Button.BackgroundColorProperty, "Surface");
                button.SetDynamicResource(Button.TextColorProperty, "Muted");
                button.SetDynamicResource(Button.BorderColorProperty, "Line");
                button.BorderWidth = 1;
            }
            button.Clicked += (_, _) => onTap();
            _actions.Add(button);
        }

        public Task ShowAsync()
        {
            var host = _page.Content as Layout
                ?? throw new System.InvalidOperationException("AppDialog requires the page's root to be a Layout.");

            if (host is Grid grid)
            {
                Grid.SetRow(_root, 0);
                Grid.SetColumn(_root, 0);
                Grid.SetRowSpan(_root, System.Math.Max(1, grid.RowDefinitions.Count));
                Grid.SetColumnSpan(_root, System.Math.Max(1, grid.ColumnDefinitions.Count));
            }

            host.Add(_root);
            Open.Add(this);
            return _shown.Task;
        }

        public void Cancel() => CancelResult?.Invoke();

        public void Complete(object? result)
        {
            Open.Remove(this);
            if (_page.Content is Layout host)
                host.Remove(_root);
            Resolved?.Invoke(result);
            _shown.TrySetResult(true);
        }
    }
}
