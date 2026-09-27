namespace Burdock.Contracts.V1;

/// <summary>Route constants shared by the server endpoints and the client SDK (spec 09 §3.1).</summary>
public static class ApiRoutes
{
    public const string Prefix = "/v1";

    public static class Content
    {
        public const string Group = Prefix + "/content";
        public const string Manifest = Group + "/manifest";
    }

    public static class Identity
    {
        public const string Group = Prefix + "/identity";
    }

    public static class Progress
    {
        public const string Group = Prefix + "/progress";
    }

    public static class Settings
    {
        public const string Group = Prefix + "/settings";
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
