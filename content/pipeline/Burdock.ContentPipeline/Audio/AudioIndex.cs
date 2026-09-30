using System.Text.Json;
using Burdock.Contracts.Content;

namespace Burdock.ContentPipeline.Audio;

/// <summary>Reads audio-index.json (the shared <see cref="AudioIndexDocument"/> contract) and sums it up for the stage report.</summary>
public static class AudioIndex
{
    public static AudioIndexDocument Load(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize(stream, AudioIndexJsonContext.Default.AudioIndexDocument) ?? throw new InvalidDataException($"{path}: empty audio index.");
    }

    public static int FileCount(this AudioIndexDocument index)
    {
        ArgumentNullException.ThrowIfNull(index);
        return index.Voices.Sum(v => v.Files.Count);
    }

    public static long TotalBytes(this AudioIndexDocument index)
    {
        ArgumentNullException.ThrowIfNull(index);
        return index.Voices.Sum(v => v.Files.Values.Sum(f => f.Bytes) + v.Sample.Bytes);
    }

    /// <summary>Paths the index promises, relative to the output folder.</summary>
    public static IEnumerable<string> AllPaths(this AudioIndexDocument index)
    {
        ArgumentNullException.ThrowIfNull(index);
        return index.Voices.SelectMany(v => v.Files.Values.Select(f => f.Path).Append(v.Sample.Path));
    }
}
