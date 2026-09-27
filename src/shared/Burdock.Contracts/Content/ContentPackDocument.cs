using System.Text.Json.Serialization;

namespace Burdock.Contracts.Content;

/// <summary>
/// On-disk / on-CDN shape of a content pack (spec 08 §3.10, T25): JSON (gzipped in transit) per language pair,
/// level and topic. Shared by the pipeline (writer), the server (validation) and the app (importer).
/// </summary>
public sealed record ContentPackDocument(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("packId")] string PackId,
    [property: JsonPropertyName("pair")] string Pair,
    [property: JsonPropertyName("level")] string Level,
    [property: JsonPropertyName("topic")] string Topic,
    [property: JsonPropertyName("version")] int Version,
    [property: JsonPropertyName("generatedAt")] DateTimeOffset GeneratedAt,
    [property: JsonPropertyName("provenance")] ContentProvenanceDocument Provenance,
    [property: JsonPropertyName("entries")] IReadOnlyList<ContentEntryDocument> Entries)
{
    public const int CurrentSchemaVersion = 1;
}

/// <summary>Who produced the pack and at which review status (spec 08 §3.6: draft, ai_reviewed, human_reviewed, published).</summary>
public sealed record ContentProvenanceDocument(
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("promptVersion")] string? PromptVersion,
    [property: JsonPropertyName("reviewer")] string? Reviewer,
    [property: JsonPropertyName("attributions")] IReadOnlyList<string> Attributions);

/// <summary>One lexeme flattened to its primary sense (what widgets, WordCard and exercises need).</summary>
public sealed record ContentEntryDocument(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("lemma")] string Lemma,
    [property: JsonPropertyName("pos")] string PartOfSpeech,
    [property: JsonPropertyName("level")] string Level,
    [property: JsonPropertyName("topics")] IReadOnlyList<string> Topics,
    [property: JsonPropertyName("translation")] string Translation,
    [property: JsonPropertyName("altTranslations")] IReadOnlyList<string> AlternativeTranslations,
    [property: JsonPropertyName("acceptedForms")] IReadOnlyList<string> AcceptedForms,
    [property: JsonPropertyName("ipaGa")] string? IpaGeneralAmerican,
    [property: JsonPropertyName("ipaRp")] string? IpaReceivedPronunciation,
    [property: JsonPropertyName("exampleEn")] string? ExampleTarget,
    [property: JsonPropertyName("exampleUk")] string? ExampleNative,
    [property: JsonPropertyName("frequencyBand")] int? FrequencyBand);

/// <summary>Source-generated JSON context so the app can stay trimming/AOT friendly.</summary>
[JsonSourceGenerationOptions(WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ContentPackDocument))]
public sealed partial class ContentPackJsonContext : JsonSerializerContext;
