using System.Net.Http.Json;
using Burdock.Domain.Vocabulary;
using Burdock.Server.Modules.Pronunciation.Application;
using Burdock.Server.Modules.Pronunciation.Domain;

namespace Burdock.Server.Modules.Pronunciation.Infrastructure;

/// <summary>Calls the Kokoro sidecar (<c>content/pipeline/audio/server.py</c>): POST /render with text, voice, language and speed, M4A bytes back.</summary>
public sealed class KokoroHttpRenderer(HttpClient http, PronunciationOptions options) : IPronunciationRenderer
{
    private static readonly Uri RenderPath = new("render", UriKind.Relative);

    public async Task<RenderedAudio> RenderAsync(PronunciationText text, PronunciationVoice voice, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(voice);
        var request = new KokoroRenderRequest(text.Value, voice.Id.Value, VoiceCatalog.Language(voice), options.WordSpeed);
        using var response = await http.PostAsJsonAsync(RenderPath, request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        return new RenderedAudio(bytes, response.Content.Headers.ContentType?.MediaType ?? "audio/mp4");
    }

    /// <summary>Wire format of the sidecar; serialised in camelCase.</summary>
    public sealed record KokoroRenderRequest(string Text, string Voice, string Lang, double Speed);
}
