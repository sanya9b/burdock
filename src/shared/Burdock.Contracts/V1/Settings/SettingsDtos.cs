using System.Text.Json;

namespace Burdock.Contracts.V1.Settings;

/// <summary>One synchronised setting (spec 09 §3.6, FR-09-13): the value plus the last-writer-wins stamp (updatedAt, clientId).</summary>
public sealed record SettingsEntryDto(JsonElement Value, DateTimeOffset UpdatedAt, string ClientId);

/// <summary>GET /v1/settings: the merged document; the ETag changes whenever any entry changes.</summary>
public sealed record SettingsDocumentDto(int SchemaVersion, IReadOnlyDictionary<string, SettingsEntryDto> Entries, string Etag);

/// <summary>PUT /v1/settings: a full or partial set of entries; the server merges per key and returns the resulting document.</summary>
public sealed record PutSettingsDto(int SchemaVersion, IReadOnlyDictionary<string, SettingsEntryDto> Entries);
