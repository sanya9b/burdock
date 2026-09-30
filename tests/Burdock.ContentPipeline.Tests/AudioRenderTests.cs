using Burdock.ContentPipeline.Audio;
using Burdock.Contracts.Content;

namespace Burdock.ContentPipeline.Tests;

/// <summary>Stage `audio render` (ADR 0019): option parsing and the index the app and the manifest rely on.</summary>
public sealed class AudioRenderTests
{
    [Fact]
    public void Options_take_defaults_from_the_environment_and_the_voice_selection()
    {
        var options = AudioRenderOptions.Parse(["--pack", "p.json", "--out", "audio"], _ => null);

        options.Pack.ShouldBe("p.json");
        options.Out.ShouldBe("audio");
        options.Voices.ShouldBe(AudioRenderOptions.DefaultVoices);
        options.Python.ShouldBe("python");
        options.ModelDir.ShouldBeNull();
        options.Force.ShouldBeFalse();

        var custom = AudioRenderOptions.Parse(["--pack", "p.json", "--out", "audio", "--voices", "af_heart", "--force", "--model-dir", "m"], key => key == "BURDOCK_PYTHON" ? "venv/python" : null);
        custom.Voices.ShouldBe("af_heart");
        custom.Python.ShouldBe("venv/python");
        custom.ModelDir.ShouldBe("m");
        custom.Force.ShouldBeTrue();
    }

    [Fact]
    public void Options_reject_missing_paths_and_unknown_flags()
    {
        Should.Throw<ArgumentException>(() => AudioRenderOptions.Parse(["--pack", "p.json"], _ => null));
        Should.Throw<ArgumentException>(() => AudioRenderOptions.Parse(["--pack", "p.json", "--out", "a", "--bogus"], _ => null));
        Should.Throw<ArgumentException>(() => AudioRenderOptions.Parse(["--pack"], _ => null));
    }

    [Fact]
    public void Index_round_trips_with_totals()
    {
        var path = Path.Combine(Path.GetTempPath(), $"audio-index-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """
            {
              "schemaVersion": 1, "packId": "uk-en.free-pool", "pair": "uk-en", "packVersion": 1, "renderedAt": "2026-09-30T10:00:00+00:00",
              "format": {"container": "m4a"},
              "engine": {"name": "kokoro-onnx", "version": "0.6.1", "model": "kokoro-v1.0.onnx", "modelSha256": "aa", "voicesSha256": "bb"},
              "voices": [
                {"id": "af_heart", "name": "Heart", "accent": "GA", "gender": "female", "lang": "en-us",
                 "sample": {"path": "af_heart/sample.m4a", "sha256": "cc", "bytes": 100},
                 "files": {"0199a500-0000-7000-8000-000000000001": {"path": "af_heart/0199a500-0000-7000-8000-000000000001.m4a", "sha256": "dd", "bytes": 5000}}}
              ]
            }
            """);
        try
        {
            var index = AudioIndex.Load(path);

            index.SchemaVersion.ShouldBe(AudioIndexDocument.CurrentSchemaVersion);
            index.Voices.Count.ShouldBe(1);
            index.Voices[0].Accent.ShouldBe("GA");
            index.FileCount().ShouldBe(1);
            index.TotalBytes().ShouldBe(5100);
            index.Engine.Model.ShouldBe("kokoro-v1.0.onnx");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void The_render_script_is_found_from_the_repository()
    {
        AudioRenderCommand.FindScript().ShouldNotBeNull();
        AudioRenderCommand.FindScript(Path.GetTempPath()).ShouldBe(AudioRenderCommand.FindScript());
    }
}
