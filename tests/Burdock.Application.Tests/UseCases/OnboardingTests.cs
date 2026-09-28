using Burdock.Application.Ports;
using Burdock.Application.Tests.Fakes;
using Burdock.Application.UseCases.Onboarding;
using Burdock.Domain.Learning;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;
using Burdock.TestKit;

namespace Burdock.Application.Tests.UseCases;

public sealed class OnboardingTests
{
    private static readonly LearnerId Learner = LearnerId.From(Guid.Parse("00000000-0000-0000-0000-000000000077"));

    [Fact]
    public async Task Complete_applies_profile_pace_level_topics_consent_and_flag()
    {
        var context = new FakeLearnerContext(Learner, LearnerSettings.Moderate(LanguagePair.UkrainianToEnglish, CefrLevel.A1, [TopicId.From("food")]));
        var consent = new FakePrivacyConsent();
        var state = new FakeOnboardingState();
        var useCase = new CompleteOnboardingUseCase(context, consent, state);
        var choices = new OnboardingChoices("  Хоробрий Реп’ях #4821 ", CefrLevel.B1, [TopicId.From("travel"), TopicId.From("it")], PacePreset.Intensive, NotificationsEnabled: true, AnalyticsConsent: true);

        var result = await useCase.ExecuteAsync(choices, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        context.Profile.ShouldNotBeNull();
        context.Profile.Nickname.ShouldBe("Хоробрий Реп’ях #4821");
        context.Settings.Level.ShouldBe(CefrLevel.B1);
        context.Settings.Topics.ShouldBe([TopicId.From("travel"), TopicId.From("it")]);
        context.Settings.NewWordsPerDay.ShouldBe(20);
        context.Settings.NotificationsPerDay.ShouldBe(4);
        consent.Analytics.ShouldBeTrue();
        state.Completed.ShouldBeTrue();
    }

    [Fact]
    public async Task Complete_turns_notifications_off_when_declined_and_rejects_a_bad_nickname()
    {
        var context = new FakeLearnerContext(Learner, LearnerSettings.Moderate(LanguagePair.UkrainianToEnglish, CefrLevel.A1, [TopicId.From("food")]));
        var state = new FakeOnboardingState();
        var useCase = new CompleteOnboardingUseCase(context, new FakePrivacyConsent(), state);

        var declined = await useCase.ExecuteAsync(new OnboardingChoices("Оля", CefrLevel.A2, [TopicId.From("food")], PacePreset.Light, NotificationsEnabled: false, AnalyticsConsent: false), CancellationToken.None);
        declined.IsSuccess.ShouldBeTrue();
        context.Settings.NotificationsPerDay.ShouldBe(0);
        context.Settings.NewWordsPerDay.ShouldBe(5);

        var rejected = await useCase.ExecuteAsync(new OnboardingChoices("x", CefrLevel.A2, [TopicId.From("food")], PacePreset.Light, false, false), CancellationToken.None);
        rejected.IsFailure.ShouldBeTrue();
        rejected.Error!.Code.ShouldBe("profile.nickname.length");
    }

    [Fact]
    public async Task Estimate_counts_words_of_the_level_neighbours_in_the_chosen_topics()
    {
        var content = new InMemoryContentRepository();
        WordEntry Entry(string lemma, CefrLevel level, string topic) => new(LexemeBuilder.ALexeme().WithLemma(lemma).AtLevel(level).InTopics(topic).WithId(Guid.NewGuid()).Build(), lemma, [], [lemma], null, null, null, null);
        await content.ImportPackAsync(new ImportedContentPack("test", LanguagePair.UkrainianToEnglish, 1,
        [
            Entry("a1-travel", CefrLevel.A1, "travel"),
            Entry("a2-travel", CefrLevel.A2, "travel"),
            Entry("b1-travel", CefrLevel.B1, "travel"),
            Entry("c1-travel", CefrLevel.C1, "travel"),
            Entry("a2-food", CefrLevel.A2, "food"),
        ]), CancellationToken.None);
        var context = new FakeLearnerContext(Learner, LearnerSettings.Moderate(LanguagePair.UkrainianToEnglish, CefrLevel.A1, [TopicId.From("food")]));
        var useCase = new EstimateWordCountUseCase(context, content);

        (await useCase.ExecuteAsync(CefrLevel.A2, [TopicId.From("travel")], CancellationToken.None)).ShouldBe(3);
        (await useCase.ExecuteAsync(CefrLevel.A2, [TopicId.From("travel"), TopicId.From("food")], CancellationToken.None)).ShouldBe(4);
        (await useCase.ExecuteAsync(CefrLevel.A2, [], CancellationToken.None)).ShouldBe(0);
    }

    [Fact]
    public void Suggested_nickname_is_adjective_noun_and_a_four_digit_number()
    {
        var useCase = new SuggestNicknameUseCase(new FakeRandom(1, 4821));

        useCase.Execute(["Хоробрий", "Уважний", "Веселий"], "Реп’ях").ShouldBe("Уважний Реп’ях #4821");
        LearnerProfile.Create("Уважний Реп’ях #4821").IsSuccess.ShouldBeTrue();
    }
}
