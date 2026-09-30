using System.Text.Json.Serialization;

namespace Burdock.Contracts.Content;

/// <summary>
/// audio-index.json written by the `audio render` stage (ADR 0019): provenance of the engine plus, per voice, the sample clip
/// and one clip per lexeme. Shared by the pipeline (writer/verifier) and the app (bundled and downloaded audio).
/// </summary>
public sealed record AudioIndexDocument(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("packId")] string PackId,
    [property: JsonPropertyName("pair")] string Pair,
    [property: JsonPropertyName("packVersion")] int PackVersion,
    [property: JsonPropertyName("renderedAt")] DateTimeOffset RenderedAt,
    [property: JsonPropertyName("engine")] AudioEngineDocument Engine,
    [property: JsonPropertyName("voices")] IReadOnlyList<AudioVoiceDocument> Voices)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record AudioEngineDocument(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("modelSha256")] string ModelSha256,
    [property: JsonPropertyName("voicesSha256")] string VoicesSha256);

public sealed record AudioVoiceDocument(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("accent")] string Accent,
    [property: JsonPropertyName("gender")] string Gender,
    [property: JsonPropertyName("sample")] AudioFileDocument Sample,
    [property: JsonPropertyName("files")] IReadOnlyDictionary<string, AudioFileDocument> Files);

public sealed record AudioFileDocument(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("sha256")] string Sha256,
    [property: JsonPropertyName("bytes")] long Bytes);

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(AudioIndexDocument))]
public sealed partial class AudioIndexJsonContext : JsonSerializerContext;
