using Burdock.Presentation.ViewModels;

namespace Burdock.Presentation.Views;

public partial class SessionSummaryPage : ContentPage, IQueryAttributable
{
    private readonly SessionSummaryViewModel _viewModel;

    public SessionSummaryPage(SessionSummaryViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        BindingContext = _viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query) => _viewModel.ApplyQueryAttributes(query);

    /// <summary>The summary is terminal: back returns to Today instead of re-entering the finished session.</summary>
    protected override bool OnBackButtonPressed()
    {
        _viewModel.BackToTodayCommand.Execute(null);
        return true;
    }
}
