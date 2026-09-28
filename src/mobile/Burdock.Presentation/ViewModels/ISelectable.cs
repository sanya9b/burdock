using System.ComponentModel;

namespace Burdock.Presentation.ViewModels;

/// <summary>Anything an option card can bind its selected look to (ChipOption, PaceOption).</summary>
public interface ISelectable : INotifyPropertyChanged
{
    bool IsSelected { get; }
}
