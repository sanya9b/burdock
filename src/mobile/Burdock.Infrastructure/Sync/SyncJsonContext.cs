using System.Text.Json.Serialization;
using Burdock.Contracts.V1.Content;
using Burdock.Contracts.V1.Identity;
using Burdock.Contracts.V1.Progress;
using Burdock.Contracts.V1.Settings;

namespace Burdock.Infrastructure.Sync;

/// <summary>OAuth token response of POST /v1/connect/token (RFC 6749 section 5.1).</summary>
internal sealed record TokenResponseDto(
    [property: JsonPropertyName("access_token")] string? AccessToken,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken,
    [property: JsonPropertyName("expires_in")] int? ExpiresIn);

/// <summary>OAuth error response (RFC 6749 section 5.2); <c>invalid_grant</c> means the installation or refresh token is no longer valid.</summary>
internal sealed record TokenErrorDto(
    [property: JsonPropertyName("error")] string? Error,
    [property: JsonPropertyName("error_description")] string? ErrorDescription);

/// <summary>The slice of an RFC 9457 problem the app acts on: the server writes the domain error code into <c>code</c> (spec 09 section 3.11).</summary>
internal sealed record ProblemDocument(string? Type, string? Title, int? Status, string? Code);

/// <summary>Source-generated serialization of everything the gateway sends and receives; camelCase like the server, tolerant on read.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(RegisterInstallationDto))]
[JsonSerializable(typeof(AccountDto))]
[JsonSerializable(typeof(PostEventsDto))]
[JsonSerializable(typeof(PostEventsResultDto))]
[JsonSerializable(typeof(EventsPageDto))]
[JsonSerializable(typeof(PutSettingsDto))]
[JsonSerializable(typeof(SettingsDocumentDto))]
[JsonSerializable(typeof(ContentManifestDto))]
[JsonSerializable(typeof(TokenResponseDto))]
[JsonSerializable(typeof(TokenErrorDto))]
[JsonSerializable(typeof(ProblemDocument))]
internal sealed partial class SyncJsonContext : JsonSerializerContext;
