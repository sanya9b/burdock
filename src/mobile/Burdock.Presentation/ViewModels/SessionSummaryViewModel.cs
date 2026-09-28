using System.Globalization;
using System.Text;
using Burdock.Domain.Learning;
using Burdock.Presentation.Localization;
using Burdock.Presentation.Navigation;
using Burdock.Presentation.Resources.Strings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Burdock.Presentation.ViewModels;

/// <summary>S-12d: session summary; the learning-day rule counts graded cards only (T11).</summary>
public sealed partial class SessionSummaryViewModel(INavigationService navigation) : ObservableObject, IQueryAttributable
{
    /// <summary>Below this accuracy the mascot encourages instead of celebrating (spec 03 S-12d).</summary>
    internal const double CelebrationThreshold = 0.6;

    private static readonly CultureInfo Ukrainian = CultureInfo.GetCultureInfo("uk-UA");
    private static readonly CompositeFormat AccuracyFormat = CompositeFormat.Parse(AppResources.Summary_Accuracy_Label);
    private static readonly CompositeFormat NotYetFormat = CompositeFormat.Parse(AppResources.Summary_LearningDay_NotYet);
    private static readonly CompositeFormat GradedOne = CompositeFormat.Parse(AppResources.Summary_Graded_One);
    private static readonly CompositeFormat GradedFew = CompositeFormat.Parse(AppResources.Summary_Graded_Few);
    private static readonly CompositeFormat GradedMany = CompositeFormat.Parse(AppResources.Summary_Graded_Many);
    private static readonly CompositeFormat CorrectOne = CompositeFormat.Parse(AppResources.Summary_Correct_One);
    private static readonly CompositeFormat CorrectFew = CompositeFormat.Parse(AppResources.Summary_Correct_Few);
    private static readonly CompositeFormat CorrectMany = CompositeFormat.Parse(AppResources.Summary_Correct_Many);

    [ObservableProperty]
    private int _graded;

    [ObservableProperty]
    private int _correct;

    [ObservableProperty]
    private double _accuracy;

    [ObservableProperty]
    private string _accuracyPercentText = string.Empty;

    [ObservableProperty]
    private string _accuracyText = string.Empty;

    [ObservableProperty]
    private string _gradedText = string.Empty;

    [ObservableProperty]
    private string _correctText = string.Empty;

    [ObservableProperty]
    private string _learningDayText = string.Empty;

    [ObservableProperty]
    private bool _learningDayCounted;

    [ObservableProperty]
    private string _mascot = "mascot_party.png";

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        Graded = Read(query, "graded");
        Correct = Read(query, "correct");
        Accuracy = Graded == 0 ? 0 : (double)Correct / Graded;
        var percent = (int)Math.Round(100 * Accuracy);
        AccuracyPercentText = string.Create(Ukrainian, $"{percent}%");
        AccuracyText = string.Format(Ukrainian, AccuracyFormat, percent);
        GradedText = Plural.Uk(Graded, GradedOne, GradedFew, GradedMany);
        CorrectText = Plural.Uk(Correct, CorrectOne, CorrectFew, CorrectMany);
        Mascot = Accuracy >= CelebrationThreshold ? "mascot_party.png" : "mascot_cheer.png";
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
