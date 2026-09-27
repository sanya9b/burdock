using System.Text.Json.Serialization;

namespace Burdock.Infrastructure.Persistence;

/// <summary>Source-generated JSON for small string lists stored in SQLite columns (trim/AOT friendly).</summary>
[JsonSerializable(typeof(IReadOnlyList<string>))]
internal sealed partial class StringListJsonContext : JsonSerializerContext;
