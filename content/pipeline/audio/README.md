# Рендер аудіо вимови (стадія `audio render`, ADR 0019)

Kokoro-82M (Apache-2.0) через `kokoro-onnx` рендерить кожне слово пакета в кожному голосі добірки; ffmpeg із пакета `imageio-ffmpeg` кодує в AAC-LC (M4A, моно, 24 кГц, 32 кбіт/с). Усе працює офлайн на CPU, приблизно секунда на файл.

## Підготовка

```bash
python -m venv .venv && .venv/Scripts/pip install -r content/pipeline/audio/requirements.txt
# моделі (~340 МБ) не комітяться; папка content/pipeline/audio/models/ у .gitignore
curl -L -o content/pipeline/audio/models/kokoro-v1.0.onnx https://github.com/thewh1teagle/kokoro-onnx/releases/download/model-files-v1.0/kokoro-v1.0.onnx
curl -L -o content/pipeline/audio/models/voices-v1.0.bin https://github.com/thewh1teagle/kokoro-onnx/releases/download/model-files-v1.0/voices-v1.0.bin
```

Контрольні суми, з якими рендерилось seed-аудіо: `kokoro-v1.0.onnx` = `7d5df8ecf7d4b1878015a32686053fd0eebe2bc377234608764cc0ef3636a6c5`, `voices-v1.0.bin` = `bca610b8308e8d99f32e6fe4197e7ec01679264efed0cac9140fe9c29f1fbf7d`. Вони ж записуються в `audio-index.json`.

## Запуск

Через CLI пайплайну (запускає цей скрипт і перевіряє індекс):

```bash
dotnet run --project content/pipeline/Burdock.ContentPipeline -- audio render --pack content/seed/uk-en.free-pool.seed.json --out content/seed/audio --python .venv/Scripts/python.exe
```

Або напряму: `python content/pipeline/audio/render_audio.py --pack … --out … [--voices af_heart,am_michael,bf_emma,bm_george] [--force]`.

## Добірка голосів v1 (власник, 30.09.2026)

| id | Назва в інтерфейсі | Акцент | Стать |
|---|---|---|---|
| `af_heart` | Heart | GA | жіночий |
| `am_michael` | Michael | GA | чоловічий |
| `bf_emma` | Emma | RP | жіночий |
| `bm_george` | George | RP | чоловічий |

Голос за замовчуванням — `af_heart`. Один глобальний вибір на пристрій: зміна в налаштуваннях діє скрізь.

## Вихід

```text
<out>/
├── audio-index.json     # schemaVersion, packId, engine (версія, SHA-256 моделей), voices[] з files{lexemeId → path, sha256, bytes}
├── af_heart/<lexemeId>.m4a … sample.m4a
└── …
```

Seed-аудіо для 54 слів стартового пакета лежить у `content/seed/audio/` і потрапляє в застосунок як MauiAsset `content/audio/**` (тимчасово, поки пакети не роздає сервер).

## Сайдкар для сервера (ADR 0020)

`server.py` піднімає той самий рушій як HTTP-сервіс для модуля Pronunciation: `POST /render {text, voice, lang, speed}` → `audio/mp4`, `GET /health`. Локально:

```bash
.venv/Scripts/pip install -r content/pipeline/audio/requirements-server.txt
KOKORO_MODEL_DIR=content/pipeline/audio/models .venv/Scripts/uvicorn server:app --host 127.0.0.1 --port 8081 --app-dir content/pipeline/audio
Pronunciation__AdminKey=<ключ> dotnet run --project src/server/Burdock.Server.Api     # POST /v1/pronunciation/clips з заголовком X-Burdock-Admin-Key
```

У продакшні це контейнер `kokoro` (`infra/kokoro/Dockerfile`, моделі завантажуються під час збирання й звіряються за SHA-256, 2 потоки onnxruntime, лише приватна мережа compose). Повний рендер словника робиться не сайдкаром, а `render_audio.py` (одна команда на голос з `--threads 2`, щоб чотири процеси ділили процесор без простою; індекс для всіх голосів перезбирається останнім проходом без `--force`).
