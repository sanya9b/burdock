using Burdock.Application.DependencyInjection;
using Burdock.Application.Ports;
using Burdock.Domain.Learning;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;
using Burdock.Presentation.Navigation;
using Burdock.Presentation.Services;
using Burdock.Presentation.Tests.Fakes;
using Burdock.Presentation.ViewModels;
using Burdock.TestKit;
using Burdock.TestKit.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace Burdock.Presentation.Tests;

/// <summary>
/// Composes ViewModels the way the app does (use cases through DI) over in-memory ports and fake platform services.
/// The clock starts at 09:00 UTC = 11:00 in the fixed Kyiv zone on 5 October 2026.
/// </summary>
public sealed class ViewModelHarness
{
    public static readonly LearnerId Learner = LearnerId.From(Guid.Parse("0199a4c0-0000-7000-8000-00000000aaaa"));
    public static readonly DateTimeOffset DefaultNow = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);

    private ServiceProvider? _provider;

    public ViewModelHarness(LearnerSettings? settings = null)
    {
        LearnerContext = new FakeLearnerContext(Learner, settings ?? DefaultSettings);
        ContentPort = new FlakyContentRepository(Content);
    }

    public static LearnerSettings DefaultSettings { get; } = LearnerSettings.Moderate(LanguagePair.UkrainianToEnglish, CefrLevel.A1, []) with { NewWordsPerDay = 5 };

    public FakeClock Clock { get; } = new(DefaultNow, TestTimeZones.KyivFixed);

    public InMemoryContentRepository Content { get; } = new();

    public FlakyContentRepository ContentPort { get; }

    public InMemoryCardRepository Cards { get; } = new();

    public InMemoryReviewLog Reviews { get; } = new();

    public InMemoryExposureStore Exposures { get; } = new();

    public InMemoryRewardsStore Rewards { get; } = new();

    public SequentialIdGenerator Ids { get; } = new();

    public FakeScheduler Scheduler { get; } = new();

    public FakeLearnerContext LearnerContext { get; }

    public FakeOnboardingState Onboarding { get; } = new();

    public FakePrivacyConsent Privacy { get; } = new();

    public FakeRandom Random { get; } = new(0);

    public List<ImportedContentPack> Packs { get; } = [];

    public FakeNavigationService Navigation { get; } = new();

    public FakeSpeechService Speech { get; } = new();

    public FakeHaptics Haptics { get; } = new();

    public FakeToastService Toast { get; } = new();

    public FakeChoiceSheet Sheet { get; } = new();

    public FakeNotificationPrompt Notifications { get; } = new();

    public FakeMotionPreferences Motion { get; } = new();

    public FakeAppThemeService Theme { get; } = new();

    /// <summary>A bundled pack of <paramref name="count"/> words; imported by the bootstrap use case on the first load.</summary>
    public ViewModelHarness WithBundledWords(int count)
    {
        Packs.Add(SeedPack(count));
        return this;
    }

    /// <summary>Puts <paramref name="count"/> words straight into the repository (screens that do not bootstrap).</summary>
    public async Task<ViewModelHarness> WithImportedWordsAsync(int count)
    {
        await Content.ImportPackAsync(SeedPack(count), CancellationToken.None);
        return this;
    }

    public T Resolve<T>() where T : notnull => (_provider ??= Build()).GetRequiredService<T>();

    /// <summary>word1…wordN: odd numbers in «food», even in «shopping»; every third word is A2; every fourth has an alternative translation.</summary>
    public static WordEntry Word(int n) => new(
        LexemeBuilder.ALexeme()
            .WithId(Guid.Parse($"0199a500-0000-7000-8000-0000000000{n:x2}"))
            .WithLemma($"word{n}")
            .AtLevel(n % 3 == 0 ? CefrLevel.A2 : CefrLevel.A1)
            .InTopics(n % 2 == 0 ? "shopping" : "food")
            .Build(),
        $"слово{n}",
        n % 4 == 0 ? ["варіант"] : [],
        [$"word{n}"],
        $"/wɜrd{n}/",
        $"/wɜːd{n}/",
        $"Example with word{n} inside.",
        $"Приклад {n}.");

    public static ImportedContentPack SeedPack(int count, int version = 1)
        => new("uk-en.free-pool", LanguagePair.UkrainianToEnglish, version, Enumerable.Range(1, count).Select(Word).ToArray());

    private ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBurdockApplication();

        services.AddSingleton<IClock>(Clock);
        services.AddSingleton<IContentRepository>(ContentPort);
        services.AddSingleton<ILearnerCardRepository>(Cards);
        services.AddSingleton<IDailyPlanCache>(new InMemoryDailyPlanCache());
        services.AddSingleton<IReviewLogStore>(Reviews);
        services.AddSingleton<IExposureStore>(Exposures);
        services.AddSingleton<IRewardsStore>(Rewards);
        services.AddSingleton<IIdGenerator>(Ids);
        services.AddSingleton<IReviewScheduler>(Scheduler);
        services.AddSingleton<ILearnerContext>(LearnerContext);
        services.AddSingleton<IOnboardingState>(Onboarding);
        services.AddSingleton<IPrivacyConsent>(Privacy);
        services.AddSingleton<IRandom>(Random);
        services.AddSingleton<IContentPackSource>(new InMemoryPackSource([.. Packs]));

        services.AddSingleton<INavigationService>(Navigation);
        services.AddSingleton<ISpeechService>(Speech);
        services.AddSingleton<IHaptics>(Haptics);
        services.AddSingleton<IToastService>(Toast);
        services.AddSingleton<IChoiceSheet>(Sheet);
        services.AddSingleton<INotificationPrompt>(Notifications);
        services.AddSingleton<IMotionPreferences>(Motion);
        services.AddSingleton<IAppThemeService>(Theme);

        services.AddTransient<BootstrapViewModel>();
        services.AddTransient<OnboardingViewModel>();
        services.AddTransient<TodayViewModel>();
        services.AddTransient<LearnViewModel>();
        services.AddTransient<WordsViewModel>();
        services.AddTransient<MeViewModel>();
        services.AddTransient<WordCardViewModel>();
        services.AddTransient<SessionViewModel>();
        services.AddTransient<SessionSummaryViewModel>();
        services.AddTransient<SettingsViewModel>();
        return services.BuildServiceProvider();
    }
}
