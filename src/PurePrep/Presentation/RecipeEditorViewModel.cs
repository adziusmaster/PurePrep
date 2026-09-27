using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using PurePrep.Domain;
using PurePrep.Localization;

namespace PurePrep.Presentation;

/// <summary>
/// Thin UI wrapper over a <see cref="RecipeDraft"/> for the list-based editor. Every mutation goes to the
/// draft first and is then mirrored into the bound collections, so the draft stays the single source of
/// truth (step ingredient keys, re-detected timers) and <see cref="ToDraft"/> never has to rebuild it.
/// </summary>
public sealed class RecipeEditorViewModel : INotifyPropertyChanged
{
    private static readonly char[] LineBreaks = { '\r', '\n' };

    private readonly RecipeDraft _draft;
    private string _title;
    private int? _servings;
    private int? _prepMinutes;
    private int? _cookMinutes;
    private ImageSource? _photo;

    /// <param name="draft">The recipe being edited.</param>
    /// <param name="currentPhoto">The recipe's stored photo, or null when it has none.</param>
    public RecipeEditorViewModel(RecipeDraft draft, ImageSource? currentPhoto = null)
    {
        _draft = draft;
        _photo = currentPhoto;
        _title = draft.Title;
        _servings = draft.Servings;
        _prepMinutes = draft.PrepMinutes;
        _cookMinutes = draft.CookMinutes;
        Ingredients = new ObservableCollection<IngredientRow>(draft.Ingredients.Select(i => new IngredientRow(i)));
        Steps = new ObservableCollection<StepRow>(draft.Steps.Select((s, index) => new StepRow(draft, s, index + 1)));

        AddIngredientCommand = new Command(AddIngredient);
        RemoveIngredientCommand = new Command<IngredientRow>(RemoveIngredient);
        AddStepCommand = new Command(AddStep);
        RemoveStepCommand = new Command<StepRow>(RemoveStep);
    }

    /// <summary>Raised with a newly inserted ingredient row that should receive keyboard focus.</summary>
    public event EventHandler<IngredientRow>? IngredientFocusRequested;

    public ObservableCollection<IngredientRow> Ingredients { get; }
    public ObservableCollection<StepRow> Steps { get; }

    public ICommand AddIngredientCommand { get; }
    public ICommand RemoveIngredientCommand { get; }
    public ICommand AddStepCommand { get; }
    public ICommand RemoveStepCommand { get; }

    public string Title
    {
        get => _title;
        set => SetField(ref _title, value ?? string.Empty);
    }

    public int? Servings
    {
        get => _servings;
        set => SetField(ref _servings, value);
    }

    public int? PrepMinutes
    {
        get => _prepMinutes;
        set => SetField(ref _prepMinutes, value);
    }

    public int? CookMinutes
    {
        get => _cookMinutes;
        set => SetField(ref _cookMinutes, value);
    }

    /// <summary>What the photo area shows: the pending photo, else the stored one; null → the "Add photo" placeholder.</summary>
    public ImageSource? Photo
    {
        get => _photo;
        private set
        {
            SetField(ref _photo, value);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasPhoto)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PhotoActionText)));
        }
    }

    public bool HasPhoto => _photo is not null;

    /// <summary>Screen-reader label for the photo area.</summary>
    public string PhotoActionText => AppResources.Get(HasPhoto ? "ChangePhoto" : "AddPhoto");

    /// <summary>A newly picked photo (JPEG) not yet written to disk; stored on Save.</summary>
    public byte[]? PendingPhoto { get; private set; }

    /// <summary>The stored photo was removed in this session; its file is deleted on Save.</summary>
    public bool PhotoRemoved { get; private set; }

    /// <summary>Previews a picked photo. Nothing is written until the recipe is saved.</summary>
    public void SetPhoto(byte[] jpeg)
    {
        PendingPhoto = jpeg;
        PhotoRemoved = false;
        Photo = ImageSource.FromStream(() => new MemoryStream(jpeg));
    }

    /// <summary>Clears the photo (pending or stored) back to the placeholder. Applied on Save.</summary>
    public void RemovePhoto()
    {
        PendingPhoto = null;
        PhotoRemoved = true;
        Photo = null;
    }

    /// <summary>Enter in an ingredient row: open a fresh row directly below it and focus that.</summary>
    public void IngredientCompleted(IngredientRow row)
    {
        var index = Ingredients.IndexOf(row);
        InsertIngredientAt(index < 0 ? Ingredients.Count : index + 1);
    }

    /// <summary>
    /// Multi-line text landed in a single ingredient row (a paste): the row keeps the first line and every
    /// further non-empty line becomes its own row below it.
    /// </summary>
    public void IngredientPasted(IngredientRow row, string text)
    {
        var breakAt = text.IndexOfAny(LineBreaks);
        if (breakAt < 0)
        {
            row.Text = text;
            return;
        }

        var index = Ingredients.IndexOf(row);
        if (index < 0)
            return;

        row.Text = text[..breakAt].Trim();
        var added = _draft.PasteIngredients(index + 1, text[(breakAt + 1)..]);
        for (var i = 0; i < added.Count; i++)
            Ingredients.Insert(index + 1 + i, new IngredientRow(added[i]));
    }

    /// <summary>Pushes the on-screen order (after a drag) into the draft and renumbers the steps.</summary>
    public void SyncOrder()
    {
        _draft.ReorderIngredients(Ingredients.Select(r => r.Key).ToList());
        _draft.ReorderSteps(Steps.Select(r => r.Key).ToList());
        RenumberSteps();
    }

    public RecipeDraft ToDraft()
    {
        SyncOrder();
        _draft.Title = Title;
        _draft.Servings = Servings;
        _draft.PrepMinutes = PrepMinutes;
        _draft.CookMinutes = CookMinutes;
        return _draft;
    }

    private void AddIngredient() => InsertIngredientAt(Ingredients.Count);

    private void InsertIngredientAt(int index)
    {
        var row = new IngredientRow(_draft.InsertIngredient(index));
        Ingredients.Insert(index, row);
        IngredientFocusRequested?.Invoke(this, row);
    }

    private void RemoveIngredient(IngredientRow? row)
    {
        if (row is null)
            return;
        _draft.RemoveIngredient(row.Key);
        Ingredients.Remove(row);
    }

    private void AddStep()
    {
        var step = _draft.InsertStep(Steps.Count);
        Steps.Add(new StepRow(_draft, step, Steps.Count + 1));
    }

    private void RemoveStep(StepRow? row)
    {
        if (row is null)
            return;
        _draft.RemoveStep(row.Key);
        Steps.Remove(row);
        RenumberSteps();
    }

    private void RenumberSteps()
    {
        for (var i = 0; i < Steps.Count; i++)
            Steps[i].Number = i + 1;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>One ingredient line in the editor; edits write straight through to its draft row.</summary>
public sealed class IngredientRow : INotifyPropertyChanged
{
    private readonly DraftIngredient _ingredient;

    internal IngredientRow(DraftIngredient ingredient) => _ingredient = ingredient;

    public Guid Key => _ingredient.Key;

    public string Text
    {
        get => _ingredient.Text;
        set
        {
            value ??= string.Empty;
            if (_ingredient.Text == value)
                return;
            _ingredient.Text = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>One method step in the editor; text edits go through the draft so its timers are re-detected.</summary>
public sealed class StepRow : INotifyPropertyChanged
{
    private readonly RecipeDraft _draft;
    private readonly DraftStep _step;
    private int _number;

    internal StepRow(RecipeDraft draft, DraftStep step, int number)
    {
        _draft = draft;
        _step = step;
        _number = number;
    }

    public Guid Key => _step.Key;

    public int Number
    {
        get => _number;
        set
        {
            if (_number == value)
                return;
            _number = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Number)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NumberDescription)));
        }
    }

    /// <summary>Screen-reader label for the step's number badge ("Step 2").</summary>
    public string NumberDescription => AppResources.Format("StepNumberFormat", Number);

    public string Text
    {
        get => _step.Text;
        set
        {
            value ??= string.Empty;
            if (_step.Text == value)
                return;
            _draft.UpdateStepText(Key, value);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
