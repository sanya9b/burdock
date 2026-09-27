using Burdock.Presentation.ViewModels;

namespace Burdock.Presentation.Views;

public partial class MePage : ContentPage
{
    public MePage(MeViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }
}
