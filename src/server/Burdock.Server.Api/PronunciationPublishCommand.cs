using System.Text.Json;
using Burdock.Contracts.Content;
using Burdock.Domain.SharedKernel;
using Burdock.Server.Modules.Pronunciation.Application;

namespace Burdock.Server.Api;

/// <summary>
/// `api pronunciation publish --index content/generated/uk-en/audio/audio-index.json --pack content/generated/uk-en/audio/_all-words.json [--voices af_heart,bm_george] [--parallel 8]`
/// (ADR 0020 §6): registers every clip of an `audio render` index through <see cref="ImportRenderedClipUseCase"/>, with the
/// lemma of each lexeme taken from the pack the audio was rendered for. Idempotent: clips already stored with the same
/// hash are skipped, so an interrupted run is simply repeated. Exit code 1 when any clip was refused (corrupt file,
/// missing text), 2 for usage errors.
/// </summary>
internal static partial class PronunciationPublishCommand
{
    private const int ProgressEvery = 1000;

    public static async Task<int> RunAsync(IServiceProvider services, string[] args, ILogger logger, CancellationToken cancellationToken)
    {
        var indexPath = Option(args, "--index");
        var packPath = Option(args, "--pack");
        if (indexPath is null || packPath is null)
        {
            LogUsage(logger);
            return 2;
        }

        var parallel = int.TryParse(Option(args, "--parallel"), System.Globalization.CultureInfo.InvariantCulture, out var degree) && degree > 0 ? degree : 8;
        var onlyVoices = Option(args, "--voices")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        AudioIndexDocument index;
        await using (var indexStream = File.OpenRead(indexPath))
        {
            index = await JsonSerializer.DeserializeAsync(indexStream, AudioIndexJsonContext.Default.AudioIndexDocument, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"{indexPath} is not an audio index.");
        }

        ContentPackDocument pack;
        await using (var packStream = File.OpenRead(packPath))
        {
            pack = await JsonSerializer.DeserializeAsync(packStream, ContentPackJsonContext.Default.ContentPackDocument, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"{packPath} is not a content pack.");
        }

        var texts = pack.Entries.ToDictionary(e => e.Id, e => e.Lemma);
        var root = Path.GetDirectoryName(Path.GetFullPath(indexPath))!;
        var voices = index.Voices.Where(v => onlyVoices is null || onlyVoices.Contains(v.Id, StringComparer.Ordinal)).ToArray();
        var totalClips = voices.Sum(v => v.Files.Count);
        LogStarted(logger, index.PackId, voices.Length, totalClips, index.Engine.Model, index.Engine.ModelSha256);

        foreach (var voice in voices)
        {
            var sample = await File.ReadAllBytesAsync(Path.Combine(root, voice.Sample.Path), cancellationToken).ConfigureAwait(false);
            using var scope = services.CreateScope();
            var result = await scope.ServiceProvider.GetRequiredService<ImportRenderedClipUseCase>().ImportSampleAsync(voice.Id, sample, voice.Sample.Sha256, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure)
            {
                LogClipFailed(logger, Guid.Empty, voice.Id, result.Error.Code);
                return 1;
            }
        }

        var work = voices.SelectMany(v => v.Files.Select(f => (Voice: v.Id, LexemeId: Guid.Parse(f.Key), File: f.Value))).ToArray();
        var imported = 0;
        var unchanged = 0;
        var failed = 0;
        var processed = 0;
        await Parallel.ForEachAsync(work, new ParallelOptions { MaxDegreeOfParallelism = parallel, CancellationToken = cancellationToken }, async (item, token) =>
        {
            if (!texts.TryGetValue(item.LexemeId, out var text))
            {
                Interlocked.Increment(ref failed);
                LogClipFailed(logger, item.LexemeId, item.Voice, "pronunciation.text_missing_in_pack");
                return;
            }

            var bytes = await File.ReadAllBytesAsync(Path.Combine(root, item.File.Path), token).ConfigureAwait(false);
            using var scope = services.CreateScope();
            var useCase = scope.ServiceProvider.GetRequiredService<ImportRenderedClipUseCase>();
            var result = await useCase.ExecuteAsync(new RenderedClipImport(LexemeId.From(item.LexemeId), item.Voice, text, bytes, item.File.Sha256), token).ConfigureAwait(false);
            if (result.IsFailure)
            {
                Interlocked.Increment(ref failed);
                LogClipFailed(logger, item.LexemeId, item.Voice, result.Error.Code);
            }
            else if (result.Value.AlreadyImported)
            {
                Interlocked.Increment(ref unchanged);
            }
            else
            {
                Interlocked.Increment(ref imported);
            }

            var done = Interlocked.Increment(ref processed);
            if (done % ProgressEvery == 0)
            {
                LogProgress(logger, done, work.Length);
            }
        }).ConfigureAwait(false);

        LogFinished(logger, imported, unchanged, failed);
        return failed == 0 ? 0 : 1;
    }

    private static string? Option(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Usage: api pronunciation publish --index <audio-index.json> --pack <pack.json> [--voices id,id] [--parallel 8]")]
    private static partial void LogUsage(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Publishing pronunciation of {PackId}: {Voices} voices, {Clips} clips, model {Model} ({ModelSha256})")]
    private static partial void LogStarted(ILogger logger, string packId, int voices, int clips, string model, string modelSha256);

    [LoggerMessage(Level = LogLevel.Information, Message = "Pronunciation publish progress: {Done}/{Total}")]
    private static partial void LogProgress(ILogger logger, int done, int total);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Clip {LexemeId}/{Voice} refused: {Code}")]
    private static partial void LogClipFailed(ILogger logger, Guid lexemeId, string voice, string code);

    [LoggerMessage(Level = LogLevel.Information, Message = "Pronunciation publish finished: {Imported} imported, {Unchanged} unchanged, {Failed} failed")]
    private static partial void LogFinished(ILogger logger, int imported, int unchanged, int failed);
}
