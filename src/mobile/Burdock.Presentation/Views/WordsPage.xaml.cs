using Burdock.Presentation.ViewModels;

namespace Burdock.Presentation.Views;

public partial class WordsPage : ContentPage
{
    public WordsPage(WordsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }
}
