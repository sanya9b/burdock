using System.Globalization;
using System.Text;
using Burdock.Application.Ports;
using Burdock.Application.UseCases.LearningDays;
using Burdock.Presentation.Resources.Strings;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Burdock.Presentation.ViewModels;

/// <summary>S-10 «Сьогодні»: greeting, learning day, words of the day (empty until the first content pack is cached).</summary>
public sealed partial class TodayViewModel : ObservableObject
{
    private static readonly CultureInfo Ukrainian = CultureInfo.GetCultureInfo("uk-UA");
    private static readonly CompositeFormat LearningDayFormat = CompositeFormat.Parse(AppResources.Today_LearningDay_Label);

    private readonly LearningDayResolver _learningDays;
    private readonly IClock _clock;

    [ObservableProperty]
    private ViewState _state = ViewState.Empty;

    [ObservableProperty]
    private string _greeting = string.Empty;

    [ObservableProperty]
    private string _learningDayText = string.Empty;

    public TodayViewModel(LearningDayResolver learningDays, IClock clock)
    {
        _learningDays = learningDays ?? throw new ArgumentNullException(nameof(learningDays));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        Refresh();
    }

    public void Refresh()
    {
        var localHour = TimeZoneInfo.ConvertTime(_clock.UtcNow, _clock.LocalTimeZone).Hour;
        Greeting = localHour switch
        {
            >= 5 and < 12 => AppResources.Today_Greeting_Morning,
            >= 12 and < 18 => AppResources.Today_Greeting_Day,
            >= 18 and < 23 => AppResources.Today_Greeting_Evening,
            _ => AppResources.Today_Greeting_Night,
        };

        var day = _learningDays.Today();
        LearningDayText = string.Format(Ukrainian, LearningDayFormat, day.Date.ToString("d MMMM", Ukrainian));
    }
}
