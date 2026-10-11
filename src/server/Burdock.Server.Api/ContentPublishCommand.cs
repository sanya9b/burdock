using System.Text.Json;
using System.Text.Json.Serialization;
using Burdock.Contracts.Content;
using Burdock.Domain.Vocabulary;
using Burdock.Server.Modules.Content.Application;
using Burdock.Server.Modules.Content.Domain;

namespace Burdock.Server.Api;

/// <summary>
/// `api content publish --index content/generated/uk-en/packs/index.json [--attributions content/pipeline/packs/attributions.json] [--min-app-version 1.0.0]`
/// (spec 09 §3.4, FR-09-8): publishes every pack of a build_packs.py index through PublishPackUseCase. Runs on the VM
/// over SSH or from CI with a limited key; idempotent for unchanged packs.
/// </summary>
internal static partial class ContentPublishCommand
{
    public static async Task<int> RunAsync(IServiceProvider services, string[] args, ILogger logger, CancellationToken cancellationToken)
    {
        var indexPath = Option(args, "--index");
        if (indexPath is null)
        {
            LogUsage(logger);
            return 2;
        }

        var minAppVersion = Option(args, "--min-app-version") ?? "1.0.0";
        var attributions = await LoadAttributionsAsync(Option(args, "--attributions"), cancellationToken).ConfigureAwait(false);
        await using var indexStream = File.OpenRead(indexPath);
        var index = await JsonSerializer.DeserializeAsync(indexStream, ContentPackJsonContext.Default.ContentPackIndexDocument, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"{indexPath} is not a pack index.");
        var pair = LanguagePair.Parse(index.Pair);
        if (pair.IsFailure)
        {
            LogFailure(logger, index.Pair, pair.Error.Code);
            return 2;
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(indexPath))!;
        var published = 0;
        var unchanged = 0;
        foreach (var entry in index.Packs)
        {
            var json = await File.ReadAllBytesAsync(Path.Combine(directory, entry.File), cancellationToken).ConfigureAwait(false);
            var identity = new PackIdentity(entry.PackId, pair.Value, entry.Level, entry.Topic, entry.Version);
            var applicable = attributions.Where(a => a.AppliesTo(entry.PackId, entry.Level)).Select(a => new PackAttribution(a.Source, a.License, a.Text, a.Url)).ToArray();

            using var scope = services.CreateScope();
            var useCase = scope.ServiceProvider.GetRequiredService<PublishPackUseCase>();
            var result = await useCase.ExecuteAsync(new PublishPackCommand(identity, json, minAppVersion, applicable), cancellationToken).ConfigureAwait(false);
            if (result.IsFailure)
            {
                LogFailure(logger, entry.PackId, result.Error.Code);
                return 1;
            }

            if (result.Value.AlreadyPublished)
            {
                unchanged++;
            }
            else
            {
                published++;
                LogPublished(logger, entry.PackId, entry.Version, result.Value.Pack.SizeBytes, result.Value.Pack.ObjectKey);
            }
        }

        LogSummary(logger, published, unchanged);
        return 0;
    }

    private static string? Option(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static async Task<IReadOnlyList<AttributionRule>> LoadAttributionsAsync(string? path, CancellationToken cancellationToken)
    {
        if (path is null)
        {
            return [];
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync(stream, AttributionRulesJsonContext.Default.ListAttributionRule, cancellationToken).ConfigureAwait(false) ?? [];
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "usage: api content publish --index <index.json> [--attributions <attributions.json>] [--min-app-version <x.y.z>]")]
    private static partial void LogUsage(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Published {PackId} v{Version}: {Bytes} bytes at {ObjectKey}")]
    private static partial void LogPublished(ILogger logger, string packId, int version, long bytes, string objectKey);

    [LoggerMessage(Level = LogLevel.Error, Message = "Publishing {PackId} failed: {Code}")]
    private static partial void LogFailure(ILogger logger, string packId, string code);

    [LoggerMessage(Level = LogLevel.Information, Message = "Content publish done: {Published} published, {Unchanged} already current")]
    private static partial void LogSummary(ILogger logger, int published, int unchanged);
}

/// <summary>One line of attributions.json: an attribution and the packs it applies to (by level or by pack id; neither means every pack).</summary>
internal sealed record AttributionRule(
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("license")] string License,
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("levels")] IReadOnlyList<string>? Levels,
    [property: JsonPropertyName("packIds")] IReadOnlyList<string>? PackIds)
{
    public bool AppliesTo(string packId, string level)
        => (Levels is null && PackIds is null)
           || (Levels?.Contains(level, StringComparer.OrdinalIgnoreCase) ?? false)
           || (PackIds?.Contains(packId, StringComparer.Ordinal) ?? false);
}

[JsonSerializable(typeof(List<AttributionRule>))]
internal sealed partial class AttributionRulesJsonContext : JsonSerializerContext;
