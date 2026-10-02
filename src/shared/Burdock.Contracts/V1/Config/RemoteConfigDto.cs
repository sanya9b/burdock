namespace Burdock.Contracts.V1.Config;

/// <summary>GET /v1/config (spec 09 FR-09-22): what the app may do before any authenticated call.</summary>
public sealed record RemoteConfigDto(
    IReadOnlyDictionary<string, string> MinAppVersion,
    IReadOnlyDictionary<string, bool> Features,
    string RulesVersion,
    IReadOnlyDictionary<string, string> Urls);
