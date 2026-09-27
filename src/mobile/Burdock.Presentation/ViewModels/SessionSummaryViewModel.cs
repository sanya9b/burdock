using System.Globalization;
using System.Text;
using Burdock.Domain.Learning;
using Burdock.Presentation.Navigation;
using Burdock.Presentation.Resources.Strings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Burdock.Presentation.ViewModels;

/// <summary>S-12d: session summary; the learning-day rule counts graded cards only (T11).</summary>
public sealed partial class SessionSummaryViewModel(INavigationService navigation) : ObservableObject, IQueryAttributable
{
    private static readonly CultureInfo Ukrainian = CultureInfo.GetCultureInfo("uk-UA");
    private static readonly CompositeFormat AccuracyFormat = CompositeFormat.Parse(AppResources.Summary_Accuracy_Label);
    private static readonly CompositeFormat NotYetFormat = CompositeFormat.Parse(AppResources.Summary_LearningDay_NotYet);

    [ObservableProperty]
    private int _graded;

    [ObservableProperty]
    private int _correct;

    [ObservableProperty]
    private string _accuracyText = string.Empty;

    [ObservableProperty]
    private string _learningDayText = string.Empty;

    [ObservableProperty]
    private bool _learningDayCounted;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        Graded = Read(query, "graded");
        Correct = Read(query, "correct");
        AccuracyText = string.Format(Ukrainian, AccuracyFormat, Graded == 0 ? 0 : (int)Math.Round(100.0 * Correct / Graded));
        LearningDayCounted = Graded >= LearningPolicy.MinGradedCardsForLearningDay;
        LearningDayText = LearningDayCounted
            ? AppResources.Summary_LearningDay_Counted
            : string.Format(Ukrainian, NotYetFormat, LearningPolicy.MinGradedCardsForLearningDay - Graded);
    }

    [RelayCommand]
    private Task BackToTodayAsync(CancellationToken cancellationToken) => navigation.GoToAsync(Routes.Today.Root, null, cancellationToken);

    private static int Read(IDictionary<string, object> query, string key)
        => query.TryGetValue(key, out var raw) && int.TryParse(raw?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
}
