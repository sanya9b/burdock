# Kokoro sidecar for the Pronunciation module (ADR 0020 §4): compose service `kokoro`, private network only.
# Build from the repository root:  docker build -f infra/kokoro/Dockerfile -t burdock-kokoro .
FROM python:3.12-slim

ARG MODEL_URL=https://github.com/thewh1teagle/kokoro-onnx/releases/download/model-files-v1.0/kokoro-v1.0.onnx
ARG VOICES_URL=https://github.com/thewh1teagle/kokoro-onnx/releases/download/model-files-v1.0/voices-v1.0.bin
ARG MODEL_SHA256=7d5df8ecf7d4b1878015a32686053fd0eebe2bc377234608764cc0ef3636a6c5
ARG VOICES_SHA256=bca610b8308e8d99f32e6fe4197e7ec01679264efed0cac9140fe9c29f1fbf7d

ENV PYTHONUNBUFFERED=1 \
    KOKORO_MODEL_DIR=/models \
    KOKORO_THREADS=2

RUN apt-get update && apt-get install -y --no-install-recommends curl ca-certificates && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /models \
    && curl -fsSL -o /models/kokoro-v1.0.onnx "$MODEL_URL" \
    && curl -fsSL -o /models/voices-v1.0.bin "$VOICES_URL" \
    && echo "$MODEL_SHA256  /models/kokoro-v1.0.onnx" | sha256sum -c - \
    && echo "$VOICES_SHA256  /models/voices-v1.0.bin" | sha256sum -c -

WORKDIR /app
COPY content/pipeline/audio/requirements.txt content/pipeline/audio/requirements-server.txt ./
RUN pip install --no-cache-dir -r requirements-server.txt
COPY content/pipeline/audio/render_audio.py content/pipeline/audio/server.py ./

RUN useradd --system --no-create-home kokoro
USER kokoro
EXPOSE 8081
HEALTHCHECK --interval=30s --timeout=5s --start-period=60s CMD python -c "import urllib.request,sys; sys.exit(0 if urllib.request.urlopen('http://127.0.0.1:8081/health').status == 200 else 1)"
CMD ["uvicorn", "server:app", "--host", "0.0.0.0", "--port", "8081", "--workers", "1"]
