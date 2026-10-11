namespace Burdock.Contracts.V1;

/// <summary>Route constants shared by the server endpoints and the client SDK (spec 09 §3.1).</summary>
public static class ApiRoutes
{
    public const string Prefix = "/v1";

    /// <summary>GET /v1/config: anonymous remote configuration (spec 09 FR-09-22).</summary>
    public const string Config = Prefix + "/config";

    public static class Content
    {
        public const string Group = Prefix + "/content";
        public const string Manifest = Group + "/manifest";
    }

    public static class Pronunciation
    {
        public const string Group = Prefix + "/pronunciation";
        public const string Voices = Group + "/voices";
        public const string Clips = Group + "/clips";

        /// <summary>Shared admin secret of the write endpoints until the Identity module issues scoped tokens (ADR 0020).</summary>
        public const string AdminKeyHeader = "X-Burdock-Admin-Key";
    }

    public static class Identity
    {
        public const string Group = Prefix + "/identity";
        public const string Anonymous = Group + "/anonymous";
        public const string Me = Group + "/me";
    }

    /// <summary>OpenIddict endpoints (spec 09 §3.2, §3.3.1): the only issuer of tokens.</summary>
    public static class Connect
    {
        public const string Token = Prefix + "/connect/token";
        public const string Revocation = Prefix + "/connect/revocation";

        /// <summary>Custom grant that exchanges an installation id + secret for tokens of the anonymous account.</summary>
        public const string InstallationGrantType = "urn:burdock:grant:installation";
        public const string ClientId = "burdock-mobile";
    }

    public static class Progress
    {
        public const string Group = Prefix + "/progress";
        public const string Events = Group + "/events";
    }

    public static class Settings
    {
        public const string Group = Prefix + "/settings";
        public const string Document = Group;
    }

    public static class Rewards
    {
        public const string Group = Prefix + "/rewards";
    }

    public static class Entitlements
    {
        public const string Group = Prefix + "/entitlements";
    }

    public static class LiveActivities
    {
        public const string Group = Prefix + "/live-activities";
    }

    public static class Analytics
    {
        public const string Group = Prefix + "/analytics";
        public const string Events = Group + "/events";
    }
}
