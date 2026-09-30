# content/ — контент «Реп'яха»

Тут живе все, з чого будується база слів: вихідні джерела, пайплайн, власні списки, черги рецензування та зібрані пакети. Специфікація напрямку: [docs/specs/08-content-pipeline.md](../docs/specs/08-content-pipeline.md). Ліцензійна позиція: [журнал рішень, Q37](../docs/decisions/2026-09-25-interview.md).

## Структура

```text
content/
├── README.md              # цей файл
├── ATTRIBUTIONS.md        # генерується командою `attributions render`; не редагувати вручну
├── sources/               # реєстр джерел (README.md) і малі вихідні файли з дозволеними ліцензіями
│   ├── cefrj/             # CEFR-J Wordlist v1.6 (Excel) + примітка про умови
│   ├── oewn/              # Open English WordNet 2025: скрипт завантаження + SHA-256 (сам XML не комітиться)
│   ├── cmudict/           # cmudict.dict + LICENSE (BSD-2-Clause)
│   └── derived/           # малі похідні файли з атрибуцією (наприклад, частотні смуги для ~10k лем, коли джерело затверджене)
├── pipeline/
│   ├── Burdock.ContentPipeline/        # консольний застосунок .NET 10 (System.CommandLine)
│   ├── audio/                          # render_audio.py (Kokoro-82M), requirements.txt, models/ (.gitignore)
│   ├── prompts/                        # translate-v3.md, examples-v2.md, classify-v2.md, review-v2.md
│   ├── filters/                        # offensive-en.txt, offensive-uk.txt, russisms-uk.csv
│   └── appsettings.json                # константи (розд. 3.16 специфікації), без секретів
├── seed/                  # стартовий пакет для розробки + seed/audio/ (аудіо 4 голосів, потрапляє в застосунок як MauiAsset)
├── own-lists/             # CSV / Markdown-списки власника (імпортуються зі статусом human_reviewed)
├── review/
│   ├── REVIEWER_GUIDE.md  # інструкція рецензента (що перевіряти, що заборонено копіювати)
│   ├── queues/            # експортовані черги і заповнені вердикти (комітяться як доказова база)
│   └── reports/           # витягнуті скарги користувачів без персональних даних
└── packs/                 # зібрані пакети .json.gz і manifest.json — у .gitignore
```

## Як запустити пайплайн (заплановані команди)

Передумови: .NET 10 SDK, PostgreSQL 18 (локально або в Docker), змінні середовища `CONTENT_DB` (рядок підключення), `ANTHROPIC_API_KEY`, для завантаження — `CONTENT_S3_ENDPOINT`, `CONTENT_S3_KEY`, `CONTENT_S3_SECRET`.

```bash
# аліас для зручності
alias burdock-content='dotnet run --project content/pipeline/Burdock.ContentPipeline --'

# 1. Джерела
burdock-content ingest cefrj --file content/sources/cefrj/CEFR-J_Wordlist_v1.6.xlsx
burdock-content normalize
burdock-content enrich oewn --file content/sources/oewn/english-wordnet-2025.xml
burdock-content pronounce cmudict --file content/sources/cmudict/cmudict.dict
burdock-content propose c1c2 --target 2000          # кандидати C1/C2 (рівень — оцінка моделі)

# 2. Генерація (Message Batches API, Claude Sonnet 5)
burdock-content cost estimate --level A1            # перевірка бюджету перед батчем
burdock-content classify  --level A1
burdock-content translate --level A1
burdock-content examples  --level A1
burdock-content pronounce rp --level A1

# 3. Рецензування
burdock-content review ai --level A1                # Claude Opus 5.5, рубрика
burdock-content review export --stratum A1/food --format md   # → content/review/queues/
#    ... рецензент заповнює verdict / edited_value / reason_code ...
burdock-content review import --file content/review/queues/2026-10-14-a1-food.md

# 3а. Аудіо вимови (ADR 0019; див. content/pipeline/audio/README.md)
burdock-content audio render --pack content/seed/uk-en.free-pool.seed.json --out content/seed/audio --python .venv/Scripts/python.exe

# 4. Публікація та пакети
burdock-content lint                                # ліцензії, текст, схема
burdock-content publish --level A1 --topic food
burdock-content pack build --pair uk-en --out content/packs
burdock-content pack verify --against content/packs/previous
burdock-content pack sign
burdock-content pack upload --bucket burdock-content
burdock-content manifest update --publish

# Допоміжні
burdock-content own-lists import --file content/own-lists/travel-owner.csv
burdock-content reports pull --since 2026-10-01
burdock-content attributions render                 # → content/ATTRIBUTIONS.md
burdock-content stats
```

Кожен запуск отримує `run_id`; LLM-стадії ідемпотентні (повторний запуск не генерує вже згенероване, якщо не вказано `--force`).

## Куди йдуть пакети

- Локально: `content/packs/<pair>/<level>/<topic>/<topic>-<version>.json.gz` і `content/packs/manifests/<pair>/manifest.json`.
- Публікація: Hetzner Object Storage → CDN `https://cdn.burdock.org/packs/v{schemaVersion}/{pair}/{level}/{topic}/{topic}-{version}.json.gz`; маніфест віддає API `https://api.burdock.org/v1/content/manifest` з ETag (дзеркало на CDN).
- Файли на CDN незмінні: новий вміст = нова версія = новий шлях.

## Правила git

- **Не комітити** файли понад 10 МБ (повний OEWN XML, частотні дампи, `packs/`). Замість них — скрипт завантаження і SHA-256 у `content/sources/README.md`.
- **Комітити**: малі джерела з дозволеними ліцензіями (CEFR-J Excel, cmudict.dict), промти, фільтри, власні списки, черги рецензування і заповнені вердикти, витягнуті скарги (без персональних даних), `ATTRIBUTIONS.md`.
- Черги у `review/queues/` після імпорту не редагуються: вони є доказом людського редагування (Закон 2811-IX, ст. 33).
- Секрети — лише у змінних середовища або GitHub Secrets; `appsettings.json` містить лише константи.
- Заборонені джерела (Wiktionary/Kaikki, Octanove, Kelly, VESUM, Oxford, Cambridge EVP, COCA, SUBTLEX напряму, wordfreq як дані пакетів) не додаються в `sources/` ні в якому вигляді; `lint --licenses` блокує збірку.
