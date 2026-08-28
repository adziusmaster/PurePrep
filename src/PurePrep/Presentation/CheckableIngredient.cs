using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PurePrep.Presentation;

/// <summary>An ingredient line with a tick-off state, used while cooking.</summary>
public sealed class CheckableIngredient(string text) : INotifyPropertyChanged
{
    private bool _isChecked;

    public string Text { get; } = text;

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value)
                return;
            _isChecked = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TextDecoration));
        }
    }

    /// <summary>Strikes through a ticked ingredient so the remaining ones stand out at a glance.</summary>
    public TextDecorations TextDecoration => _isChecked ? TextDecorations.Strikethrough : TextDecorations.None;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
