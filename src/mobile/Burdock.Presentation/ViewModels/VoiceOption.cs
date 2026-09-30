using Burdock.Domain.Vocabulary;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Burdock.Presentation.ViewModels;

/// <summary>One row of the S-18 «Голос» list: the voice, its accent-and-gender line, and whether it is the learner's choice.</summary>
public sealed partial class VoiceOption(PronunciationVoice voice, string subtitle) : ObservableObject, ISelectable
{
    public PronunciationVoice Voice { get; } = voice;

    public string Title => Voice.Name;

    public string Subtitle { get; } = subtitle;

    [ObservableProperty]
    private bool _isSelected;
}
