using Microsoft.Maui.Controls.Shapes;
using PurePrep.Controls;

namespace PurePrep.Services;

/// <summary>One option in <see cref="AppDialog.ChooseAsync(ContentPage, string, string, DialogChoice[])"/>.</summary>
/// <param name="Text">The option's label, returned when it is chosen.</param>
/// <param name="Destructive">Renders the row in the danger colour (e.g. Delete).</param>
/// <param name="Icon">Optional leading icon — an <see cref="Resources.Styles.Icons"/> constant.</param>
internal record DialogChoice(string Text, bool Destructive = false, string? Icon = null);

/// <summary>
/// In-app, on-brand replacements for MAUI's native <c>DisplayAlert</c>/<c>DisplayPromptAsync</c>/
/// <c>DisplayActionSheet</c>. The platform dialogs looked like bare Android system pop-ups, jarringly
/// different from the rest of the app; these render inside the shared <see cref="BottomSheet"/> on
/// the current page instead, so every prompt matches the app's look (and the app's own sheets).
/// </summary>
public static class AppDialog
{
    /// <summary>True while at least one styled dialog or bottom sheet is on screen.</summary>
    public static bool IsOpen => BottomSheet.AnyOpen;

    /// <summary>
    /// Called by the Android back handler: cancels the top-most open dialog or sheet (returning its
    /// cancel result) and reports whether it consumed the press.
    /// </summary>
    public static bool TryHandleBack() => BottomSheet.TryCancelTop();

    /// <summary>A simple message with a single dismiss button.</summary>
    public static Task AlertAsync(ContentPage page, string title, string? message, string dismiss)
    {
        var dialog = new Dialog(page, title, message, icon: null, destructive: false);
        dialog.SetButtons(null, (dismiss, () => dialog.Complete(true)), destructive: false);
        dialog.CancelResult = true;
        return dialog.ShowAsync();
    }

    /// <summary>
    /// A yes/no confirmation. Resolves true when <paramref name="accept"/> is chosen. Pass
    /// <paramref name="destructive"/> for irreversible actions (red accept button) and an optional
    /// header <paramref name="icon"/> (an <see cref="Resources.Styles.Icons"/> constant).
    /// </summary>
    public static async Task<bool> ConfirmAsync(ContentPage page, string title, string? message, string accept, string cancel,
        bool destructive = false, string? icon = null)
    {
        var dialog = new Dialog(page, title, message, icon, destructive);
        dialog.SetButtons((cancel, () => dialog.Complete(false)), (accept, () => dialog.Complete(true)), destructive);
        dialog.CancelResult = false;
        return await dialog.ShowAsync() is true;
    }

    /// <summary>A single-line text prompt. Returns the entered text, or null when cancelled.</summary>
    public static async Task<string?> PromptAsync(ContentPage page, string title, string? message, string accept, string cancel,
        string? placeholder = null, string? initialValue = null, int maxLength = -1, Keyboard? keyboard = null)
    {
        var dialog = new Dialog(page, title, message, icon: null, destructive: false);
        var entry = dialog.AddEntry(placeholder, initialValue, maxLength, keyboard);
        dialog.SetButtons((cancel, () => dialog.Complete(null)), (accept, () => dialog.Complete(entry.Text ?? string.Empty)), destructive: false);
        dialog.CancelResult = null;
        return await dialog.ShowAsync() as string;
    }

    /// <summary>
    /// A list of choices, mirroring <c>DisplayActionSheet</c>: returns the chosen option's text, or
    /// null when cancelled (back / backdrop / the cancel button).
    /// </summary>
    public static Task<string?> ChooseAsync(ContentPage page, string title, string cancel, params string[] options) =>
        ChooseAsync(page, title, cancel, options.Select(o => new DialogChoice(o)).ToArray());

    /// <summary>
    /// A list of choices where individual rows can be marked destructive or carry an icon. Returns
    /// the chosen option's text, or null when cancelled.
    /// </summary>
    internal static async Task<string?> ChooseAsync(ContentPage page, string title, string cancel, params DialogChoice[] options)
    {
        var dialog = new Dialog(page, title, null, icon: null, destructive: false);
        foreach (var option in options)
        {
            var captured = option.Text;
            dialog.AddChoice(option, () => dialog.Complete(captured));
        }
        if (!string.IsNullOrEmpty(cancel))
            dialog.AddTextButton(cancel, () => dialog.Complete(null));
        dialog.CancelResult = null;
        return await dialog.ShowAsync() as string;
    }

    // Builds one dialog's body and hosts it in a BottomSheet attached to the page. Kept private so the
    // public surface stays the four intent-named helpers above.
    private sealed class Dialog
    {
        // Above this many characters two side-by-side buttons would squeeze their labels, so the
        // button row stacks vertically instead (primary on top) — a label must never truncate.
        private const int SideBySideMaxChars = 14;

        private readonly ContentPage _page;
        private readonly BottomSheet _sheet = new();
        private readonly VerticalStackLayout _body = new() { Spacing = 0 };
        private readonly VerticalStackLayout _choices = new() { Spacing = 2 };
        private readonly TaskCompletionSource<object?> _result = new();
        private Entry? _entry;
        private bool _completed;

        public object? CancelResult;

        public Dialog(ContentPage page, string title, string? message, string? icon, bool destructive)
        {
            _page = page;

            if (!string.IsNullOrEmpty(icon))
            {
                var badge = new Border
                {
                    WidthRequest = 48,
                    HeightRequest = 48,
                    StrokeThickness = 0,
                    StrokeShape = new RoundRectangle { CornerRadius = 16 },
                    HorizontalOptions = LayoutOptions.Start,
                    Margin = new Thickness(0, 0, 0, 16),
                    Content = Styled(new Label { Text = icon, FontSize = 24 }, "IconLabel"),
                };
                badge.SetDynamicResource(VisualElement.BackgroundColorProperty, destructive ? "DangerWash" : "LimeWash");
                ((Label)badge.Content).SetDynamicResource(Label.TextColorProperty, destructive ? "Danger" : "Lime");
                _body.Add(badge);
            }

            if (!string.IsNullOrEmpty(title))
                _body.Add(Styled(new Label { Text = title }, "SheetTitle"));

            if (!string.IsNullOrWhiteSpace(message))
                _body.Add(Styled(new Label { Text = message, Margin = new Thickness(0, 8, 0, 0) }, "SheetMessage"));

            _choices.Margin = new Thickness(-12, 12, -12, 0);
            _body.Add(_choices);

            _sheet.SheetContent = _body;
            _sheet.Cancelled += (_, _) => Complete(CancelResult);
        }

        public Entry AddEntry(string? placeholder, string? initialValue, int maxLength, Keyboard? keyboard)
        {
            var entry = new Entry
            {
                Placeholder = placeholder,
                Text = initialValue,
                Keyboard = keyboard ?? Keyboard.Default,
                FontSize = 16,
                BackgroundColor = Colors.Transparent,
            };
            if (maxLength > 0)
                entry.MaxLength = maxLength;
            entry.SetDynamicResource(Entry.TextColorProperty, "Ink");
            entry.SetDynamicResource(Entry.PlaceholderColorProperty, "Faint");

            var field = Styled(new Border { Content = entry, Margin = new Thickness(0, 20, 0, 0) }, "SheetField");
            _body.Insert(_body.IndexOf(_choices), field);
            _entry = entry;
            return entry;
        }

        public void AddChoice(DialogChoice choice, Action onTap)
        {
            var row = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
                ColumnSpacing = 14,
            };
            var colour = choice.Destructive ? "Danger" : "Ink";

            if (!string.IsNullOrEmpty(choice.Icon))
            {
                var icon = Styled(new Label { Text = choice.Icon, FontSize = 22 }, "IconLabel");
                icon.SetDynamicResource(Label.TextColorProperty, choice.Destructive ? "Danger" : "Muted");
                row.Add(icon, 0);
            }

            var label = new Label
            {
                Text = choice.Text,
                FontSize = 16,
                VerticalOptions = LayoutOptions.Center,
                LineBreakMode = LineBreakMode.WordWrap,
            };
            label.SetDynamicResource(Label.TextColorProperty, colour);
            row.Add(label, 1);

            var tile = new Border
            {
                Padding = new Thickness(12, 12),
                MinimumHeightRequest = 52,
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 14 },
                BackgroundColor = Colors.Transparent,
                Content = row,
            };
            AutomationProperties.SetIsInAccessibleTree(tile, true);
            AutomationProperties.SetName(tile, choice.Text);
            tile.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(onTap) });
            TapFeedback.SetIsEnabled(tile, true);
            _choices.Add(tile);
        }

        public void AddTextButton(string text, Action onTap)
        {
            var button = Styled(new Button { Text = text, BorderWidth = 0, Margin = new Thickness(0, 12, 0, 0) }, "SheetSecondaryButton");
            button.SetDynamicResource(Button.TextColorProperty, "Muted");
            button.Clicked += (_, _) => onTap();
            _body.Add(button);
        }

        // One button → full width. Two → side by side, equal width, secondary left / primary right;
        // stacked (primary first) when either label is too long to sit comfortably at half width.
        public void SetButtons((string Text, Action OnTap)? secondary, (string Text, Action OnTap) primary, bool destructive)
        {
            var primaryButton = Styled(new Button { Text = primary.Text }, destructive ? "SheetDangerButton" : "SheetPrimaryButton");
            primaryButton.Clicked += (_, _) => primary.OnTap();

            View row;
            if (secondary is not { } sec)
            {
                row = primaryButton;
            }
            else
            {
                var secondaryButton = Styled(new Button { Text = sec.Text }, "SheetSecondaryButton");
                secondaryButton.Clicked += (_, _) => sec.OnTap();

                if (sec.Text.Length <= SideBySideMaxChars && primary.Text.Length <= SideBySideMaxChars)
                {
                    var grid = new Grid
                    {
                        ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
                        ColumnSpacing = 12,
                    };
                    grid.Add(secondaryButton, 0);
                    grid.Add(primaryButton, 1);
                    row = grid;
                }
                else
                {
                    row = new VerticalStackLayout { Spacing = 10, Children = { primaryButton, secondaryButton } };
                }
            }

            row.Margin = new Thickness(0, 24, 0, 0);
            _body.Add(row);
        }

        public async Task<object?> ShowAsync()
        {
            var host = _page.Content as Layout
                ?? throw new InvalidOperationException("AppDialog requires the page's root to be a Layout.");

            if (host is Grid grid)
            {
                Grid.SetRow(_sheet, 0);
                Grid.SetColumn(_sheet, 0);
                Grid.SetRowSpan(_sheet, Math.Max(1, grid.RowDefinitions.Count));
                Grid.SetColumnSpan(_sheet, Math.Max(1, grid.ColumnDefinitions.Count));
            }

            host.Add(_sheet);
            await _sheet.ShowAsync();
            // A prompt is for typing: focus the field so the keyboard comes up with the sheet.
            _entry?.Focus();
            return await _result.Task;
        }

        public async void Complete(object? result)
        {
            if (_completed)
                return;
            _completed = true;

            // Unfocus alone leaves the Android keyboard up; hide it explicitly.
            if (_entry is not null)
            {
                if (_entry.IsSoftInputShowing())
                    await _entry.HideSoftInputAsync(CancellationToken.None);
                _entry.Unfocus();
            }
            await _sheet.HideAsync();
            if (_page.Content is Layout host)
                host.Remove(_sheet);
            _result.TrySetResult(result);
        }

        private static T Styled<T>(T view, string styleKey) where T : VisualElement
        {
            if (Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(styleKey, out var style) == true && style is Style s)
                view.Style = s;
            return view;
        }
    }
}
