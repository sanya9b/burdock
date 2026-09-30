using System.Diagnostics;
using System.Globalization;

namespace Burdock.ContentPipeline.Audio;

/// <summary>Options of `audio render`; parsing is explicit so a typo fails fast with a usage line.</summary>
public sealed record AudioRenderOptions(string Pack, string Out, string Voices, string Python, string? ModelDir, bool Force)
{
    public const string DefaultVoices = "af_heart,am_michael,bf_emma,bm_george";

    public static AudioRenderOptions Parse(IReadOnlyList<string> args, Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(environment);
        string? pack = null, output = null, modelDir = null;
        var voices = DefaultVoices;
        var python = environment("BURDOCK_PYTHON") ?? "python";
        var force = false;
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--pack": pack = Next(args, ref i); break;
                case "--out": output = Next(args, ref i); break;
                case "--voices": voices = Next(args, ref i); break;
                case "--python": python = Next(args, ref i); break;
                case "--model-dir": modelDir = Next(args, ref i); break;
                case "--force": force = true; break;
                default: throw new ArgumentException($"Unknown option '{args[i]}'. Usage: audio render --pack <json> --out <dir> [--voices a,b] [--python <exe>] [--model-dir <dir>] [--force]");
            }
        }

        if (string.IsNullOrWhiteSpace(pack) || string.IsNullOrWhiteSpace(output))
        {
            throw new ArgumentException("audio render needs --pack <json> and --out <dir>.");
        }

        if (voices.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length == 0)
        {
            throw new ArgumentException("--voices must list at least one Kokoro voice id.");
        }

        return new AudioRenderOptions(pack, output, voices, python, modelDir, force);

        static string Next(IReadOnlyList<string> args, ref int i)
            => i + 1 < args.Count ? args[++i] : throw new ArgumentException($"Option '{args[i]}' needs a value.");
    }
}

/// <summary>
/// Stage `audio render` (spec 08, ADR 0019): runs content/pipeline/audio/render_audio.py (Kokoro-82M + ffmpeg) for a pack
/// and verifies the resulting audio-index.json. The heavy lifting stays in Python because the Kokoro tokenizer has no .NET port.
/// </summary>
public static class AudioRenderCommand
{
    public static async Task<int> RunAsync(AudioRenderOptions options, TextWriter stdout, TextWriter stderr, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);
        if (!File.Exists(options.Pack))
        {
            await stderr.WriteLineAsync($"Pack not found: {options.Pack}").ConfigureAwait(false);
            return 2;
        }

        var script = FindScript();
        if (script is null)
        {
            await stderr.WriteLineAsync("render_audio.py not found; run from the repository (content/pipeline/audio/render_audio.py).").ConfigureAwait(false);
            return 2;
        }

        var start = new ProcessStartInfo(options.Python)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
        };
        start.ArgumentList.Add(script);
        start.ArgumentList.Add("--pack");
        start.ArgumentList.Add(options.Pack);
        start.ArgumentList.Add("--out");
        start.ArgumentList.Add(options.Out);
        start.ArgumentList.Add("--voices");
        start.ArgumentList.Add(options.Voices);
        if (options.ModelDir is not null)
        {
            start.ArgumentList.Add("--model-dir");
            start.ArgumentList.Add(options.ModelDir);
        }

        if (options.Force)
        {
            start.ArgumentList.Add("--force");
        }

        using var process = new Process { StartInfo = start };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) { stdout.WriteLine(e.Data); } };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) { stderr.WriteLine(e.Data); } };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            return process.ExitCode;
        }

        var indexPath = Path.Combine(options.Out, "audio-index.json");
        var index = AudioIndex.Load(indexPath);
        var missing = index.AllPaths().Where(p => !File.Exists(Path.Combine(options.Out, p))).ToList();
        if (missing.Count > 0)
        {
            await stderr.WriteLineAsync($"audio-index.json lists {missing.Count} missing files, first: {missing[0]}").ConfigureAwait(false);
            return 4;
        }

        await stdout.WriteLineAsync(string.Format(CultureInfo.InvariantCulture, "audio render: pack {0} v{1}, {2} voices, {3} files, {4:N0} KB, engine {5} {6}",
            index.PackId, index.PackVersion, index.Voices.Count, index.FileCount(), index.TotalBytes() / 1024d, index.Engine.Name, index.Engine.Version)).ConfigureAwait(false);
        return 0;
    }

    /// <summary>Walks up from the working directory and the binary to the repository root that holds content/pipeline/audio.</summary>
    internal static string? FindScript(string? startFrom = null)
    {
        foreach (var origin in new[] { startFrom ?? Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var dir = new DirectoryInfo(origin); dir is not null; dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, "content", "pipeline", "audio", "render_audio.py");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }
}
