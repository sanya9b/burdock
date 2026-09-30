using System.Text.Json;
using Burdock.Application.Ports;
using Burdock.Contracts.Content;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;

namespace Burdock.Infrastructure.Content;

/// <summary>
/// Pronunciation audio shipped inside the app package as MauiAssets under content/audio/ (the seed pack today; downloaded
/// per-voice assets arrive with the server, ADR 0019). The index is read once; a missing or invalid index means «no audio»
/// and the callers fall back to the system voice.
/// </summary>
public sealed partial class BundledPronunciationAudio(ILogger<BundledPronunciationAudio> logger) : IPronunciationAudio
{
    internal const string AssetRoot = "content/audio/";
    private const string IndexAsset = AssetRoot + "audio-index.json";

    private Task<AudioCatalog>? _catalog;

    public async Task<IReadOnlyList<PronunciationVoice>> GetVoicesAsync(CancellationToken cancellationToken)
        => (await CatalogAsync(cancellationToken).ConfigureAwait(false)).Voices;

    public async Task<PronunciationClip?> FindWordAsync(LexemeId lexemeId, VoiceId voice, CancellationToken cancellationToken)
        => (await CatalogAsync(cancellationToken).ConfigureAwait(false)).FindWord(lexemeId, voice);

    public async Task<PronunciationClip?> FindSampleAsync(VoiceId voice, CancellationToken cancellationToken)
        => (await CatalogAsync(cancellationToken).ConfigureAwait(false)).FindSample(voice);

    // The first caller starts the load and everyone awaits the same task; a lost race only reads the small index twice.
    private Task<AudioCatalog> CatalogAsync(CancellationToken cancellationToken) => _catalog ??= LoadAsync(cancellationToken);

    private async Task<AudioCatalog> LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!await FileSystem.Current.AppPackageFileExistsAsync(IndexAsset).ConfigureAwait(false))
            {
                LogNoIndex();
                return AudioCatalog.Empty;
            }

            await using var stream = await FileSystem.Current.OpenAppPackageFileAsync(IndexAsset).ConfigureAwait(false);
            var document = await JsonSerializer.DeserializeAsync(stream, AudioIndexJsonContext.Default.AudioIndexDocument, cancellationToken).ConfigureAwait(false);
            if (document is null)
            {
                LogInvalid("empty");
                return AudioCatalog.Empty;
            }

            var mapped = AudioCatalog.FromDocument(document, AssetRoot);
            if (mapped.IsFailure)
            {
                LogInvalid(mapped.Error.Code);
                return AudioCatalog.Empty;
            }

            return mapped.Value;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            LogInvalid(ex.GetType().Name);
            return AudioCatalog.Empty;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "No bundled pronunciation audio index; the system voice is used")]
    private partial void LogNoIndex();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Bundled pronunciation audio index is invalid: {Code}")]
    private partial void LogInvalid(string code);
}
