using Burdock.Presentation.ViewModels;

namespace Burdock.Presentation.Views;

public partial class LearnPage : ContentPage
{
    public LearnPage(LearnViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }
}
