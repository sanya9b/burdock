"""Kokoro sidecar for the server module Pronunciation (ADR 0020 §4).

POST /render {"text": "ticket", "voice": "af_heart", "lang": "en-us", "speed": 0.9} -> audio/mp4 (AAC-LC, mono, 24 kHz, 32 kbit/s)
GET  /health                                                               -> engine, model checksums, voices

Same model, voices, trimming and encoding as render_audio.py, so a clip rendered here is byte-for-byte the kind of clip the
pipeline ships. Inference is serialised with a lock: the model is CPU-bound and the API calls this service one clip at a time.

Run locally:  KOKORO_MODEL_DIR=D:/tmp/burdock/kokoro uvicorn server:app --host 127.0.0.1 --port 8081 --app-dir content/pipeline/audio
"""
from __future__ import annotations

import importlib.metadata
import os
import tempfile
import threading
from pathlib import Path

import numpy as np
import soundfile as sf
from fastapi import FastAPI, HTTPException
from fastapi.responses import Response
from pydantic import BaseModel, Field

from render_audio import DEFAULT_VOICES, SAMPLE_RATE, encode, sha256, trim, voice_meta

MODEL_DIR = Path(os.environ.get("KOKORO_MODEL_DIR", Path(__file__).resolve().parent / "models"))
THREADS = int(os.environ.get("KOKORO_THREADS", "2"))
MAX_TEXT = 64
VOICES = [v.strip() for v in os.environ.get("KOKORO_VOICES", DEFAULT_VOICES).split(",") if v.strip()]

app = FastAPI(title="Burdock Kokoro sidecar", version="1.0")
_lock = threading.Lock()
_engine: dict = {}


class RenderRequest(BaseModel):
    text: str = Field(min_length=1, max_length=MAX_TEXT)
    voice: str
    lang: str = "en-us"
    speed: float = Field(default=0.9, ge=0.5, le=1.5)


@app.on_event("startup")
def load() -> None:
    import imageio_ffmpeg
    import onnxruntime as rt
    from kokoro_onnx import Kokoro

    model = MODEL_DIR / "kokoro-v1.0.onnx"
    voices = MODEL_DIR / "voices-v1.0.bin"
    options = rt.SessionOptions()
    options.intra_op_num_threads = THREADS
    options.inter_op_num_threads = 1
    _engine["kokoro"] = Kokoro.from_session(rt.InferenceSession(str(model), options, providers=["CPUExecutionProvider"]), str(voices))
    _engine["ffmpeg"] = imageio_ffmpeg.get_ffmpeg_exe()
    _engine["info"] = {
        "name": "kokoro-onnx",
        "version": importlib.metadata.version("kokoro-onnx"),
        "model": model.name,
        "modelSha256": sha256(model),
        "voicesSha256": sha256(voices),
        "threads": THREADS,
        "voices": VOICES,
    }


@app.get("/health")
def health() -> dict:
    if "kokoro" not in _engine:
        raise HTTPException(status_code=503, detail="model not loaded")
    return {"status": "ok", **_engine["info"]}


@app.post("/render")
def render(request: RenderRequest) -> Response:
    if request.voice not in VOICES:
        raise HTTPException(status_code=400, detail=f"unknown voice {request.voice!r}")
    voice = voice_meta(request.voice)
    lang = request.lang or voice["lang"]
    with _lock:
        samples, rate = _engine["kokoro"].create(request.text, voice=request.voice, speed=request.speed, lang=lang)
    samples = trim(np.asarray(samples, dtype=np.float32), rate)
    with tempfile.TemporaryDirectory() as tmp:
        wav = Path(tmp) / "clip.wav"
        m4a = Path(tmp) / "clip.m4a"
        sf.write(wav, samples, rate)
        encode(_engine["ffmpeg"], wav, m4a, "32k")
        data = m4a.read_bytes()
    return Response(content=data, media_type="audio/mp4", headers={"X-Engine": f"kokoro-onnx/{_engine['info']['version']}", "X-Sample-Rate": str(SAMPLE_RATE)})
