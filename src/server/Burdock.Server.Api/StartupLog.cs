namespace Burdock.Server.Api;

/// <summary>Source-generated log messages of the host (CA1848): keeps Program.cs free of string-formatting log calls.</summary>
internal static partial class StartupLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Schema {Schema} migrated")]
    public static partial void SchemaMigrated(ILogger logger, string schema);
}
