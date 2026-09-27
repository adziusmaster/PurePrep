using System.Globalization;
using PurePrep.Domain;
using PurePrep.Localization;
using PurePrep.Presentation;
using PurePrep.Services;

namespace PurePrep;

/// <summary>List-based recipe editor, used both for "Add manually" and for editing an existing recipe.</summary>
public partial class ManualAddPage : ContentPage
{
    private readonly RecipeLibraryViewModel _library;
    private readonly ParsedRecipe? _editing;
    private readonly RecipeEditorViewModel _editor;
    private IngredientRow? _pendingFocus;

    public ManualAddPage(RecipeLibraryViewModel library) : this(library, null)
    {
    }

    public ManualAddPage(RecipeLibraryViewModel library, ParsedRecipe? editing)
    {
        InitializeComponent();
        _library = library;
        _editing = editing;

        var draft = editing is not null ? RecipeDraft.FromRecipe(editing) : RecipeDraft.Empty();
        // A blank editor starts with one empty row of each so there is somewhere to type straight away.
        if (draft.Ingredients.Count == 0)
            draft.InsertIngredient(0);
        if (draft.Steps.Count == 0)
            draft.InsertStep(0);

        // Same streamed-from-disk source the library card and detail header use.
        var currentPhoto = editing is null ? null
            : new RecipeImageConverter().Convert(editing, typeof(ImageSource), null, CultureInfo.InvariantCulture) as ImageSource;
        _editor = new RecipeEditorViewModel(draft, currentPhoto);
        _editor.IngredientFocusRequested += OnIngredientFocusRequested;
        BindingContext = _editor;

        ServesEntry.Text = _editor.Servings?.ToString();
        PrepEntry.Text = _editor.PrepMinutes?.ToString();
        CookEntry.Text = _editor.CookMinutes?.ToString();

        if (editing is not null)
        {
            HeaderTopBar.Title = AppResources.Get("EditRecipeTitle");
            HeaderTopBar.Subtitle = AppResources.Get("EditRecipeSubtitle");
            SaveButton.Text = AppResources.Get("SaveChanges");
        }
    }

    // Resize (not pan) for the keyboard while this page is visible, so the sticky Save button sits
    // just above the keyboard and the form scrolls in the space left (see SheetKeyboard).
    protected override void OnAppearing()
    {
        base.OnAppearing();
#if ANDROID
        SheetKeyboard.RequestPageResize(this);
#endif
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
#if ANDROID
        SheetKeyboard.ReleasePageResize(this);
#endif
    }

    private async void OnCancelTapped(object? sender, EventArgs e) => await Navigation.PopAsync();

    // No aspect-ratio layout in MAUI: keep the photo area 16:9 of whatever width it gets.
    private void OnPhotoAreaSizeChanged(object? sender, EventArgs e)
    {
        var height = Math.Round(PhotoArea.Width * 9 / 16);
        if (height > 0 && Math.Abs(PhotoArea.HeightRequest - height) > 0.5)
            PhotoArea.HeightRequest = height;
    }

    private async void OnPhotoTapped(object? sender, EventArgs e)
    {
        var take = AppResources.Get("ImportPhotoTake");
        var choose = AppResources.Get("ImportPhotoChoose");
        var remove = AppResources.Get("RemovePhoto");

        // No row icons: the icon font subset has nothing for "gallery", and a mix of rows with and
        // without an icon doesn't line up.
        var options = new List<DialogChoice>();
        if (MediaPicker.Default.IsCaptureSupported)
            options.Add(new DialogChoice(take));
        options.Add(new DialogChoice(choose));
        if (_editor.HasPhoto)
            options.Add(new DialogChoice(remove, Destructive: true));

        var title = AppResources.Get(_editor.HasPhoto ? "ChangePhoto" : "AddPhoto");
        var choice = await AppDialog.ChooseAsync(this, title, AppResources.Get("Cancel"), options.ToArray());
        if (choice == remove)
        {
            _editor.RemovePhoto();
            return;
        }
        if (choice != take && choice != choose)
            return;

        try
        {
            // SelectionLimit = 1 asks for single select; not every Android picker enforces it, so take the first.
            var pickerOptions = new MediaPickerOptions { SelectionLimit = 1 };
#if !ANDROID
            // Elsewhere the resizer doesn't read EXIF, so let MediaPicker turn the photo upright. On Android the
            // resizer does that itself with a bounded decode (MAUI's RotateImage decodes the full-size bitmap).
            pickerOptions.RotateImage = true;
#endif
            var photo = choice == take
                ? await MediaPicker.Default.CapturePhotoAsync(pickerOptions)
                : (await MediaPicker.Default.PickPhotosAsync(pickerOptions))?.FirstOrDefault();
            if (photo is null)
                return;

            await using var stream = await photo.OpenReadAsync();
            if (await RecipePhotoResizer.ToJpegAsync(stream, CancellationToken.None) is { } jpeg)
                _editor.SetPhoto(jpeg);
        }
        catch (FeatureNotSupportedException)
        {
            await AppDialog.AlertAsync(this, title, AppResources.Get("ImportPhotoUnavailable"), AppResources.Get("Ok"));
        }
        catch (PermissionException)
        {
            await AppDialog.AlertAsync(this, title, AppResources.Get("CameraPermissionDenied"), AppResources.Get("Ok"));
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException)
        {
            // Picker cancelled or the file became unreadable: keep the photo as it was.
        }
    }

    private void OnReorderCompleted(object? sender, EventArgs e) => _editor.SyncOrder();

    private void OnIngredientCompleted(object? sender, EventArgs e)
    {
        if (sender is Entry { BindingContext: IngredientRow row })
            _editor.IngredientCompleted(row);
    }

    // A single-line Entry can still receive a multi-line paste; split it into one row per line.
    private void OnIngredientTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is Entry { BindingContext: IngredientRow row } && e.NewTextValue?.IndexOfAny(['\r', '\n']) >= 0)
            _editor.IngredientPasted(row, e.NewTextValue);
    }

    // The row's Entry may not exist yet when the VM asks for focus, so remember the row and focus its
    // Entry as soon as one is realised (or bound) for it.
    private void OnIngredientFocusRequested(object? sender, IngredientRow row) => _pendingFocus = row;

    private void OnIngredientEntryLoaded(object? sender, EventArgs e) => TryFocusPending(sender as Entry);

    private void OnIngredientEntryBindingContextChanged(object? sender, EventArgs e) => TryFocusPending(sender as Entry);

    private void TryFocusPending(Entry? entry)
    {
        if (entry is null || _pendingFocus is null || !ReferenceEquals(entry.BindingContext, _pendingFocus))
            return;
        _pendingFocus = null;
        entry.Dispatcher.Dispatch(() => entry.Focus());
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        _editor.Servings = ParseNumber(ServesEntry.Text);
        _editor.PrepMinutes = ParseNumber(PrepEntry.Text);
        _editor.CookMinutes = ParseNumber(CookEntry.Text);
        var draft = _editor.ToDraft();

        if (string.IsNullOrWhiteSpace(draft.Title))
        {
            ShowError(AppResources.Get("ErrTitleRequired"));
            return;
        }

        if (draft.Steps.All(s => string.IsNullOrWhiteSpace(s.Text)))
        {
            ShowError(AppResources.Get("ErrStepsRequired"));
            return;
        }

        SaveButton.IsEnabled = false;
        try
        {
            var recipe = _editing is not null ? draft.ApplyTo(_editing) : draft.ToNewRecipe();
            await _library.SaveFromEditorAsync(recipe, isNew: _editing is null, _editor.PendingPhoto, _editor.PhotoRemoved,
                CancellationToken.None);
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            // Never show a raw exception message (English, technical) to the cook.
            System.Diagnostics.Debug.WriteLine($"Recipe save failed: {ex}");
            ShowError(AppResources.Get("ErrGeneric"));
            SaveButton.IsEnabled = true;
        }
    }

    private static int? ParseNumber(string? text) =>
        int.TryParse(text?.Trim(), out var value) && value > 0 ? value : null;

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }
}
