using Burdock.Application.Ports;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;
using Burdock.Presentation.Navigation;
using Burdock.Presentation.Services;
using Burdock.Presentation.ViewModels;

namespace Burdock.Presentation.Tests.Fakes;

/// <summary>Records navigation instead of touching Shell.</summary>
public sealed class FakeNavigationService : INavigationService
{
    public List<(Route Route, IReadOnlyDictionary<string, object>? Parameters)> Visits { get; } = [];

    public List<(Route Route, IReadOnlyDictionary<string, object>? Parameters)> Modals { get; } = [];

    public int BackCount { get; private set; }

    public (Route Route, IReadOnlyDictionary<string, object>? Parameters) Last => Visits[^1];

    public Task GoToAsync(Route route, IReadOnlyDictionary<string, object>? parameters = null, CancellationToken cancellationToken = default)
    {
        Visits.Add((route, parameters));
        return Task.CompletedTask;
    }

    public Task GoBackAsync(CancellationToken cancellationToken = default)
    {
        BackCount++;
        return Task.CompletedTask;
    }

    public Task ShowModalAsync(Route route, IReadOnlyDictionary<string, object>? parameters = null, CancellationToken cancellationToken = default)
    {
        Modals.Add((route, parameters));
        return Task.CompletedTask;
    }
}

public sealed class FakeSpeechService : ISpeechService
{
    public List<string> Spoken { get; } = [];

    public PronunciationAccent Accent { get; set; } = PronunciationAccent.American;

    public bool CanOpenSystemVoiceSettings { get; set; } = true;

    public int VoiceSettingsOpened { get; private set; }

    public Task SpeakEnglishAsync(string text, CancellationToken cancellationToken)
    {
        Spoken.Add(text);
        return Task.CompletedTask;
    }

    public Task OpenSystemVoiceSettingsAsync()
    {
        VoiceSettingsOpened++;
        return Task.CompletedTask;
    }
}

public sealed class FakeHaptics : IHaptics
{
    public List<string> Pulses { get; } = [];

    public void Success() => Pulses.Add(nameof(Success));

    public void Error() => Pulses.Add(nameof(Error));

    public void Light() => Pulses.Add(nameof(Light));
}

public sealed class FakeToastService : IToastService
{
    public List<string> Messages { get; } = [];

    public Task ShowAsync(string message, CancellationToken cancellationToken)
    {
        Messages.Add(message);
        return Task.CompletedTask;
    }
}

/// <summary>Answers action sheets from a queue; an empty queue means the learner cancelled.</summary>
public sealed class FakeChoiceSheet : IChoiceSheet
{
    public Queue<string?> Answers { get; } = new();

    public List<IReadOnlyList<string>> OfferedOptions { get; } = [];

    public Task<string?> ShowAsync(string title, string cancel, IReadOnlyList<string> options, CancellationToken cancellationToken)
    {
        OfferedOptions.Add(options);
        return Task.FromResult(Answers.Count > 0 ? Answers.Dequeue() : null);
    }
}

public sealed class FakeNotificationPrompt : INotificationPrompt
{
    public bool Granted { get; set; }

    public int Requests { get; private set; }

    public Task<bool> RequestAsync(CancellationToken cancellationToken)
    {
        Requests++;
        return Task.FromResult(Granted);
    }
}

public sealed class FakeMotionPreferences : IMotionPreferences
{
    public bool ReduceMotion { get; set; }
}

public sealed class FakeAppThemeService : IAppThemeService
{
    public AppThemeMode Mode { get; set; } = AppThemeMode.System;

    public List<AppThemeMode> Applied { get; } = [];

    public void Apply(AppThemeMode mode)
    {
        Mode = mode;
        Applied.Add(mode);
    }
}

/// <summary>Delegates to the in-memory repository until <see cref="Failure"/> is set; then every call throws it (error-state tests).</summary>
public sealed class FlakyContentRepository(IContentRepository inner) : IContentRepository
{
    public Exception? Failure { get; set; }

    public Task<WordEntry?> GetAsync(LexemeId id, CancellationToken cancellationToken) => Guard(() => inner.GetAsync(id, cancellationToken));

    public Task<IReadOnlyList<WordEntry>> ListAsync(LanguagePair pair, IReadOnlyCollection<CefrLevel> levels, IReadOnlyCollection<TopicId> topics, CancellationToken cancellationToken)
        => Guard(() => inner.ListAsync(pair, levels, topics, cancellationToken));

    public Task<int> CountAsync(LanguagePair pair, CancellationToken cancellationToken) => Guard(() => inner.CountAsync(pair, cancellationToken));

    public Task<int?> GetImportedPackVersionAsync(string packId, CancellationToken cancellationToken) => Guard(() => inner.GetImportedPackVersionAsync(packId, cancellationToken));

    public Task ImportPackAsync(ImportedContentPack pack, CancellationToken cancellationToken) => Guard(() => inner.ImportPackAsync(pack, cancellationToken));

    private T Guard<T>(Func<T> call) => Failure is { } failure ? throw failure : call();
}
