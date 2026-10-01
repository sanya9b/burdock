namespace Burdock.Server.Modules.Pronunciation.Application;

/// <summary>Settings of the module, bound from the «Pronunciation» configuration section (environment: <c>Pronunciation__AdminKey</c> and so on).</summary>
public sealed class PronunciationOptions
{
    public const string SectionName = "Pronunciation";

    /// <summary>Shared secret of the admin write endpoints (sops secret in production); unset disables them with 503.</summary>
    public string? AdminKey { get; set; }

    /// <summary>Folder of the file store; relative paths resolve against the content root.</summary>
    public string StorePath { get; set; } = "data/pronunciation";

    /// <summary>Base address under which the CDN serves the store; unset makes the API stream clips itself (development).</summary>
    public Uri? PublicBaseUrl { get; set; }

    /// <summary>Address of the Kokoro sidecar (compose service <c>kokoro</c>).</summary>
    public Uri KokoroBaseUrl { get; set; } = new("http://localhost:8081/");

    /// <summary>Part of every storage path; bump it when the model, the voice files or the render parameters change.</summary>
    public string EngineVersion { get; set; } = "kokoro-82m-v1.0";

    /// <summary>Speaking rate for isolated words; the pipeline uses the same value.</summary>
    public double WordSpeed { get; set; } = 0.9;

    public int RenderTimeoutSeconds { get; set; } = 60;
}
