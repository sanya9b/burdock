using CommunityToolkit.Mvvm.ComponentModel;

namespace Burdock.Presentation.ViewModels;

/// <summary>One selectable chip (Chip / FilterChip component, spec 02 section 3.11); the key identifies the choice, the value carries the typed payload.</summary>
public sealed partial class ChipOption(string key, string label, object? value = null) : ObservableObject
{
    public string Key { get; } = key;

    public string Label { get; } = label;

    public object? Value { get; } = value;

    [ObservableProperty]
    private bool _isSelected;
}
