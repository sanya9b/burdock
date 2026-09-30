"""Renders pronunciation audio for a content pack with Kokoro-82M (ADR 0019, spec 08 stage `audio render`).

For every lexeme of the pack and every voice of the selection it writes `<out>/<voice>/<lexemeId>.m4a`
(AAC-LC, mono, 24 kHz, 32 kbit/s), plus `<voice>/sample.m4a` with the sample phrase for the voice picker,
and `<out>/audio-index.json` with provenance (engine, model checksums) and per-file SHA-256.
Existing files are kept unless `--force` is given, so re-runs only add what is missing.

    python render_audio.py --pack content/seed/uk-en.free-pool.seed.json --out content/seed/audio

Requirements: requirements.txt (kokoro-onnx, soundfile, imageio-ffmpeg) and the model files in --model-dir
(kokoro-v1.0.onnx, voices-v1.0.bin from https://github.com/thewh1teagle/kokoro-onnx/releases/tag/model-files-v1.0).
"""
from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import importlib.metadata
import json
import subprocess
import sys
import tempfile
import time
from pathlib import Path

import numpy as np
import soundfile as sf

DEFAULT_VOICES = "af_heart,am_michael,bf_emma,bm_george"
SAMPLE_RATE = 24000
SILENCE_DB = -50.0
PAD_SECONDS = 0.04


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1 << 16), b""):
            digest.update(chunk)
    return digest.hexdigest()


def voice_meta(voice: str) -> dict:
    prefix, name = voice.split("_", 1)
    return {
        "id": voice,
        "name": name.capitalize(),
        "accent": "GA" if prefix[0] == "a" else "RP",
        "gender": "female" if prefix[1] == "f" else "male",
        "lang": "en-us" if prefix[0] == "a" else "en-gb",
    }


def trim(samples: np.ndarray, rate: int) -> np.ndarray:
    """Cuts leading and trailing silence below SILENCE_DB, keeping a short pad so consonants are not clipped."""
    threshold = 10 ** (SILENCE_DB / 20)
    loud = np.flatnonzero(np.abs(samples) > threshold)
    if loud.size == 0:
        return samples
    pad = int(rate * PAD_SECONDS)
    start = max(0, int(loud[0]) - pad)
    end = min(len(samples), int(loud[-1]) + pad)
    return samples[start:end]


def encode(ffmpeg: str, wav: Path, target: Path, bitrate: str) -> None:
    target.parent.mkdir(parents=True, exist_ok=True)
    subprocess.run(
        [ffmpeg, "-y", "-loglevel", "error", "-i", str(wav), "-ac", "1", "-ar", str(SAMPLE_RATE), "-c:a", "aac", "-b:a", bitrate, "-movflags", "+faststart", str(target)],
        check=True,
    )


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--pack", required=True, help="pack JSON (content/seed/*.json or a built pack)")
    parser.add_argument("--out", required=True, help="output directory; one sub-folder per voice")
    parser.add_argument("--voices", default=DEFAULT_VOICES, help="comma-separated Kokoro voice ids")
    parser.add_argument("--model-dir", default=str(Path(__file__).resolve().parent / "models"))
    parser.add_argument("--ffmpeg", default=None, help="ffmpeg executable; default: the one bundled with imageio-ffmpeg")
    parser.add_argument("--sample-text", default="Burdock. Words that stick.")
    parser.add_argument("--word-speed", type=float, default=0.9)
    parser.add_argument("--bitrate", default="32k")
    parser.add_argument("--force", action="store_true", help="re-render files that already exist")
    args = parser.parse_args()

    sys.stdout.reconfigure(encoding="utf-8")
    ffmpeg = args.ffmpeg
    if ffmpeg is None:
        import imageio_ffmpeg

        ffmpeg = imageio_ffmpeg.get_ffmpeg_exe()

    pack_path = Path(args.pack)
    pack = json.loads(pack_path.read_text(encoding="utf-8"))
    entries = pack["entries"]
    model_dir = Path(args.model_dir)
    model = model_dir / "kokoro-v1.0.onnx"
    voices_bin = model_dir / "voices-v1.0.bin"
    if not model.exists() or not voices_bin.exists():
        print(f"model files missing in {model_dir}; see requirements and the download link in the module docstring", file=sys.stderr)
        return 3

    from kokoro_onnx import Kokoro

    kokoro = Kokoro(str(model), str(voices_bin))
    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    started = time.time()
    rendered = 0

    def render(text: str, voice: dict, speed: float, target: Path) -> None:
        nonlocal rendered
        if target.exists() and not args.force:
            return
        samples, rate = kokoro.create(text, voice=voice["id"], speed=speed, lang=voice["lang"])
        samples = trim(np.asarray(samples, dtype=np.float32), rate)
        with tempfile.TemporaryDirectory() as tmp:
            wav = Path(tmp) / "clip.wav"
            sf.write(wav, samples, rate)
            encode(ffmpeg, wav, target, args.bitrate)
        rendered += 1

    index_voices = []
    for voice_id in [v.strip() for v in args.voices.split(",") if v.strip()]:
        voice = voice_meta(voice_id)
        files = {}
        for entry in entries:
            target = out / voice_id / f"{entry['id']}.m4a"
            render(entry["lemma"], voice, args.word_speed, target)
            files[entry["id"]] = {"path": f"{voice_id}/{target.name}", "sha256": sha256(target), "bytes": target.stat().st_size}
        sample = out / voice_id / "sample.m4a"
        render(args.sample_text, voice, 1.0, sample)
        index_voices.append({
            **voice,
            "sample": {"path": f"{voice_id}/sample.m4a", "text": args.sample_text, "sha256": sha256(sample), "bytes": sample.stat().st_size},
            "files": files,
        })
        print(f"{voice_id}: {len(files)} words, {sum(f['bytes'] for f in files.values()) / 1024:.0f} KB", flush=True)

    index = {
        "schemaVersion": 1,
        "packId": pack["packId"],
        "pair": pack["pair"],
        "packVersion": pack["version"],
        "renderedAt": dt.datetime.now(dt.timezone.utc).isoformat(timespec="seconds"),
        "format": {"container": "m4a", "codec": "aac-lc", "bitrate": args.bitrate, "channels": 1, "sampleRate": SAMPLE_RATE},
        "engine": {
            "name": "kokoro-onnx",
            "version": importlib.metadata.version("kokoro-onnx"),
            "model": model.name,
            "modelSha256": sha256(model),
            "voicesSha256": sha256(voices_bin),
            "wordSpeed": args.word_speed,
            "license": "Apache-2.0 (Kokoro-82M weights and kokoro-onnx); rendered audio is Burdock content",
        },
        "voices": index_voices,
    }
    (out / "audio-index.json").write_text(json.dumps(index, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"index: {out / 'audio-index.json'}; rendered {rendered} new files in {time.time() - started:.0f} s")
    return 0


if __name__ == "__main__":
    sys.exit(main())
