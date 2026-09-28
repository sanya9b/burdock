using CommunityToolkit.Mvvm.ComponentModel;

namespace Burdock.Presentation.ViewModels;

/// <summary>One selectable option (Chip / FilterChip / option card, spec 02 section 3.11); the key identifies the choice, the value carries the typed payload, the hint is an optional one-line description.</summary>
public sealed partial class ChipOption(string key, string label, object? value = null, string hint = "") : ObservableObject, ISelectable
{
    public string Key { get; } = key;

    public string Label { get; } = label;

    public object? Value { get; } = value;

    public string Hint { get; } = hint;

    public bool HasHint => Hint.Length > 0;

    [ObservableProperty]
    private bool _isSelected;
}
