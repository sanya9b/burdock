# CLAUDE.md — головний промт проєкту «Реп'ях» (Burdock)

Цей файл читає кожен ШІ-агент і кожен розробник перед будь-якою задачею в репозиторії. Він короткий навмисно: деталі живуть у специфікаціях, на які він посилається.

## 1. Що ми будуємо

«Реп'ях» — україномовний застосунок для вивчення англійських слів. Головна ідея: слова «чіпляються» до людини, як реп'яхи, бо з'являються на всіх поверхнях телефона (віджети головного екрана та екрана блокування, сповіщення, Live Activity-сесія), а в застосунку їх можна вчити й перевіряти. Користувач обирає рівень CEFR (A1-C2), категорії, темп і частоту показів. Є серія, щоденні нагороди, мікрозавдання й маскот Реп'яшок.

- Платформи: .NET MAUI. **iPhone та iPad спершу**, Android зі спільної кодової бази через 1-2 місяці після iOS. Мінімум iOS 16 (інтерактивні віджети лише iOS 17+), Android API 26+.
- Сервер обов'язковий: ASP.NET Core 10 Minimal APIs, PostgreSQL 18, Hetzner (ЄС). Застосунок кешує контент після першого запуску й працює з кешу; сервер авторитетний для нагород, entitlement-ів і синхронізації.
- Бізнес-модель: freemium. Тріал 7 днів через App Store/Play, далі безкоштовно 5 слів на день; Pro відкриває налаштування, усі рівні й категорії, статистику.
- Команда: соло-розробник + ШІ-агенти, лише Windows. iOS збирається на GitHub Actions (macOS-раннери). Локально запускається тільки Android-емулятор.
- Домен `burdock.org`, bundle ID `org.burdock.app`.

## 2. Джерела істини та їх пріоритет

Якщо документи суперечать один одному, вищий у списку перемагає. Знайшов суперечність — не «виправляй по-тихому», а повідом і запропонуй ADR.

1. `docs/decisions/*.md` — журнал рішень власника продукту. Це закон.
2. `docs/adr/*.md` — архітектурні рішення, які деталізують або змінюють журнал (зміна журналу можлива лише через ADR).
3. `docs/SPEC.md` — основна специфікація продукту.
4. `docs/specs/NN-*.md` — специфікації напрямків. Розділ 0 кожної є промтом для агента, що працює над цим напрямком. Індекс: 01 архітектура · 02 дизайн-система · 03 UX-потоки й екрани · 04 поверхні iOS · 05 поверхні Android · 06 рушій навчання · 07 гейміфікація · 08 контент-пайплайн · 09 сервер · 10 безпека і приватність · 11 монетизація · 12 локалізація · 13 аналітика · 14 DevOps/CI · 15 тестування · 16 документація · 17 дорожня карта.
5. `docs/research/*.md` — довідкові факти з посиланнями (англійською). Факти, не рішення. Твердження з позначкою unverified/low не цитуються.
6. `docs/glossary.md` — терміни інтерфейсу; вживай їх дослівно.

## 3. Мовна політика

- Документація, специфікації, промти, коментарі до задач, UI-рядки: **українською**. Апостроф у тексті інтерфейсу `’` (U+2019), лапки «», літера ґ там, де належить.
- Ідентифікатори, назви файлів і пакетів, коментарі в коді, повідомлення комітів, назви гілок, логи: **англійською**.
- Коміти: Conventional Commits (`feat(learning): add FSRS scheduler port`).
- UI-рядки лише через `.resx` (нейтральна мова `uk`), ніяких рядків у XAML чи C#. Множина через хелпер плюралізації (1 слово / 2 слова / 5 слів).

## 4. Архітектурні правила (Clean Architecture за Робертом Мартіном)

Правило залежностей непорушне: залежності спрямовані лише всередину.

| Шар | Проєкт | Може залежати від | Що містить |
|---|---|---|---|
| Entities (Domain) | `Burdock.Domain` у `src/shared/` | нічого (тільки BCL) | сутності, value objects, доменні події, чисті правила (FSRS-планувальник, `WordTimelinePlanner`, `EconomyRules`) |
| Application | `Burdock.Application` | Domain | use cases (інтерактори), порти (`IClock`, `ILexemeRepository`, `IWidgetBridge`, `INotificationScheduler`, `ISyncGateway`…), DTO для меж |
| Infrastructure | `Burdock.Infrastructure` | Application, Domain | SQLite, HTTP, SecureStorage, платформові адаптери, реалізації портів |
| Presentation | `Burdock.Presentation` (MAUI) | Application, Domain | XAML, ViewModels (CommunityToolkit.Mvvm), навігація, DI-корінь у `MauiProgram.cs` |

- Сервер має ту саму структуру всередині кожного модуля (`Burdock.Server.Modules.*`): Domain / Application / Infrastructure, хост `Burdock.Server.Api` лише композиція та ендпоінти.
- ViewModel — адаптер презентації. Бізнес-правило у ViewModel — дефект. Правило у Domain без тесту — дефект.
- Час лише через `IClock`. Випадковість лише через ін'єктований `IRandom` із seed. Дата навчального дня перемикається о 04:00 локального часу.
- Платформовий код живе у `Platforms/*` або за портом. Swift-код віджетів у `src/mobile/ios-extensions/BurdockWidgets` є тонким презентером: він читає готовий таймлайн із App Group, а не рахує його.
- Доменна модель оперує парою мов `LanguagePair`; «англійська» та «українська» ніде не захардкоджені.
- Архітектурні тести (залежності шарів) є частиною CI і блокують злиття.

## 5. Дизайн та інтерфейс: неперехідні правила

Власник наголосив: **якість інтерфейсу і дизайну є пріоритетом продукту**, а не «потім відполіруємо». Тому:

1. Жоден екран, віджет чи сповіщення не робиться без відповідності `docs/specs/02-design-system.md` і `docs/specs/03-ux-flows-screens.md`. Якщо екрана немає у специфікації, спершу додається wireframe і стани у 03, потім код.
2. Кольори, типографіка, відступи, радіуси, тіні, тривалості анімацій беруться **лише з дизайн-токенів** `design/tokens/tokens.json` (згенеровані `Colors.xaml`/`Styles.xaml`/Swift/Android XML). Магічне число в XAML — дефект.
3. Кожен UI-елемент має всі стани: default, pressed, disabled, focused, loading, empty (з Реп'яшком), error, offline, free/trial/Pro. Знімки екрана у світлій і темній темі додаються до PR.
4. Доступність не опційна: контраст WCAG 2.2 AA, Dynamic Type до AX5 без обрізання, цілі натискання 44pt/48dp, Reduce Motion має еквівалент, зворотний зв'язок ніколи лише кольором, VoiceOver/TalkBack-мітки для всього інтерактивного, англійські слова позначені мовою для читача екрана.
5. Віджет — головна поверхня продукту. Він проєктується text/shape-first, бо iOS може прибрати всі кольори (tinted/clear режими). Контент за пріоритетом: слово → транскрипція → переклад → приклад.
6. Стиль: грайливий, як Duolingo, але сучасний і спокійний; палітра «Реп'ях-цвіт» (фіолет `#7C5CFF`, лайм `#B4F461`, корал, сонячний, блакитний). Маскот з'являється там, де є емоція (порожні стани, нагороди, онбординг), максимум один на екран, ніколи не соромить користувача.
7. Мікрокопі: коротко, дружньо, наказовий спосіб для кнопок, без тиску й без «ти втратиш». Шаблони у 03.
8. Перед тим як писати XAML, подивись прев'ю `design/preview/index.html` і компоненти у 02. Використовуй встановлені дизайн-скіли (див. `.claude/skills/README.md`).
9. Мова інтерфейсу v2 (ADR 0017): кнопки лише `controls:PushButton` (платформовий `Button` заборонений), картки без контуру зі стилями `CardElevated`/`CardTonal`/`CardSunken`/`HeroCard`, власний `TopBar` замість панелі Shell, іконки лише Lucide через `Icon.*`, складені відступи лише як іменовані `Thickness`-ресурси у `Resources/Styles/Components.xaml`. Кожен новий екран перевіряється знімком з емулятора у світлій і темній темі перед комітом.

## 6. Безпека та дані

- Мінімізація: збираємо лише те, що є у переліку даних `docs/specs/10-security-privacy.md`. Нова колонка з персональними даними або новий аналітичний івент потребують ADR і оновлення privacy-переліку.
- Жодних сторонніх аналітичних чи рекламних SDK. Sentry лише в регіоні ЄС із `SendDefaultPii=false`.
- Токени лише в SecureStorage/Keychain (`ThisDeviceOnly`), ніколи у файлах App Group, ніколи в логах. Логи без тіл запитів, токенів, IP та email.
- Секрети не потрапляють у репозиторій (sops/age, GitHub Environments). Знайшов секрет у коді — зупинись і повідом.

## 7. Структура репозиторію

```
CLAUDE.md                 цей файл
README.md
docs/                     SPEC.md, specs/, adr/, decisions/, research/, prompts/, glossary.md
design/                   tokens/tokens.json (джерело істини), preview/, mascot/
src/mobile/               Burdock.Application, .Infrastructure, .Presentation, ios-extensions/
src/server/               Burdock.Server.Api + Modules.*
src/shared/               Burdock.Domain (чисті правила, спільні для клієнта й сервера), Burdock.Contracts (DTO, версіоновані)
content/                  pipeline/ (Burdock.ContentPipeline), sources/, own-lists/, review/, packs/ (.gitignore)
infra/                    compose, caddy, pgbackrest, deploy
tests/                    тести за шарами та архітектурні тести
.github/                  workflows, шаблони PR/issue
.claude/skills/           перелік і налаштування скілів для агентів
```

## 8. Робочий процес для агента

Перед задачею:
1. Прочитай цей файл, журнал рішень і розділ 0 специфікації напрямку. Для UI-задачі також 02 і 03.
2. Знайди ідентифікатори вимог (`FR-NN-x`), які закриваєш; згадай їх у PR і в назвах тестів.
3. Якщо задача змінює рішення або додає зовнішню залежність чи новий тип даних, спершу чернетка ADR.

Під час:
4. Тести пишуться разом із кодом: Domain/Application без винятків; UI — знімки станів.
5. Ніяких TODO без issue. Ніяких закоментованих блоків коду.
6. Використовуй `Result`-типи для очікуваних помилок, винятки для неочікуваних (див. 01).

Після:
7. Прогін: `dotnet build` з `TreatWarningsAsErrors`, `dotnet test`, архітектурні тести, лінт локалізації, генерація токенів (якщо змінювались).
8. У PR: що зроблено, які вимоги закрито, знімки екрана (світла/темна), чи потрібне оновлення специфікації/ADR/privacy-переліку.
9. Звітуй чесно: що не вдалося перевірити (наприклад, iOS локально) — скажи прямо.

## 9. Заборонено

- Порушувати правило залежностей шарів або обходити порти «тимчасово».
- Додавати NuGet/npm/Swift-пакети з ліцензіями, відмінними від MIT/Apache-2.0/BSD/ISC/OFL, без ADR.
- Використовувати дані з Wiktionary, Oxford 3000/5000, Cambridge EVP, Octanove, Kelly, VESUM як контент (див. рішення Q37).
- Показувати пасивну «стрічку слів» у Live Activity (лише сесія, запущена користувачем).
- Нараховувати нагороди за дозвіл на сповіщення, за покупку чи за увімкнення аналітики.
- Купівельна валюта, випадкові нагороди, таймери тиску, «ти втратиш серію».
- Комітити секрети, `.p8`, keystore, сертифікати, сирі дампи словників.

## 10. Команди

Потрібні: .NET SDK 10.0.300+ (`global.json`), воркоади `maui-android` і `maui-ios`, JDK 21, Android SDK. `dotnet test` працює в режимі Microsoft.Testing.Platform (увімкнено в `global.json`), тому використовуються `--solution` / `--project`, а не позиційний шлях.

```
dotnet build Burdock.Server.slnx                                   # сервер, контент-пайплайн, спільні шари, тести
dotnet test --solution Burdock.Server.slnx --no-build              # доменні, серверні архітектурні, інтеграційні та контрактні тести (інтеграційні й контрактні піднімають PostgreSQL 18 через Testcontainers, потрібен Docker)
BURDOCK_UPDATE_SNAPSHOTS=1 dotnet test --project tests/Burdock.Server.ContractTests   # оновити знімок контракту docs/api/openapi.v1.json; без змінної той самий тест лише звіряє і падає при розходженні
dotnet test --project tests/Burdock.Application.Tests              # use cases
dotnet test --project tests/Burdock.Architecture.Tests             # правило залежностей мобільних шарів
dotnet test --project tests/Burdock.Infrastructure.Tests           # адаптери net10.0: golden-тести FSRS проти векторів py-fsrs 6.3.2 (tests/fixtures/fsrs)
dotnet test --project tests/Burdock.Presentation.Tests             # ViewModel-и на нейтральному net10.0-head MAUI-проєкту (use cases над in-memory портами)
dotnet test --project tests/Burdock.ContentPipeline.Tests          # стадії контент-пайплайну (audio render: опції, індекс)
dotnet run --project content/pipeline/Burdock.ContentPipeline -- audio render --pack content/seed/uk-en.free-pool.seed.json --out content/seed/audio --python <venv-python>   # аудіо вимови Kokoro-82M (content/pipeline/audio/README.md)
dotnet build src/mobile/Burdock.Presentation -f net10.0-android    # застосунок під Android (єдина локальна платформа)
docker compose -f infra/compose/compose.dev.yaml up -d             # локальний PostgreSQL 18 для API (burdock/burdock-dev, хост-порт 5433, щоб не збігатися з локальною службою PostgreSQL)
dotnet run --project src/server/Burdock.Server.Api                 # API на http://localhost:5210 (health: /health/live, /health/ready; у Development міграції застосовуються при старті)
dotnet run --project src/server/Burdock.Server.Api -- migrate      # лише міграції всіх схем модулів (крок деплою перед запуском контейнера)
dotnet run --project src/server/Burdock.Server.Api -- content publish --index content/generated/uk-en/packs/index.json --attributions content/pipeline/packs/attributions.json   # пакети → сховище (FileSystem/S3) + content.packs; маніфест читає їх з БД
dotnet run --project src/server/Burdock.Server.Api -- pronunciation publish --index content/generated/uk-en/audio/audio-index.json --pack content/generated/uk-en/audio/_all-words.json   # відрендерений словник вимови → сховище (FileSystem/S3) + pronunciation.clips; ідемпотентно, --voices і --parallel за потреби
dotnet ef migrations add <Name> --project src/server/Burdock.Server.Modules.<Module> --startup-project src/server/Burdock.Server.Api --context <Module>DbContext --output-dir Infrastructure/Migrations --namespace Burdock.Server.Modules.<Module>.Infrastructure.Migrations
python content/pipeline/packs/build_packs.py                       # лінт чанків + збірка пакетів у content/generated/uk-en/packs (index.json, report.json); повний цикл у content/README.md
python content/pipeline/audio/bundle_audio.py --index <rendered>/audio-index.json --out content/seed/audio   # кліпи пакета (free-pool) у бандл застосунку, з перевіркою SHA-256
python design/tokens/generate.py --check                           # токени: аліаси, контраст WCAG, лінт hex у XAML, дрейф згенерованих файлів
python design/tokens/generate.py --out all                         # Colors.xaml/Styles.xaml/WidgetThemes, Android colors/dimens, DesignTokens.swift
```

iOS збирається лише в GitHub Actions (`.github/workflows/ci-ios.yml`). Попередження є помилками: збірка з будь-яким warning не проходить.

## 11. Стан проєкту

Фаза 0 закрита 27.09.2026 (усі CI-воркфлоу зелені, включно з iOS-збіркою для симулятора). Фаза 1 «Ядро навчання» триває (28.09.2026): готові домен навчання (картка учня, FSRS через порт, політика оцінювання, планувальник дня), SQLite-сховище, стартовий пакет із 48 слів, екрани «Сьогодні» зі словами дня, картка слова з озвученням, сесія з парами/вибором/набором і підсумок; усе перевірено на Android-емуляторі (знімки в `design/preview/`). 28.09.2026 інтерфейс переведено на мову v2 (ADR 0017): рельєфні `PushButton`, картки без контуру, hero-картка-близнюк віджета, іконки Lucide, власний `TopBar`, процедурний маскот, тональні плитки категорій; статус-бар Android виправлено (issue #1). Того ж дня (ADR 0018) шрифти замінено на Unbounded (дисплей) + Rubik (текст), додано брендову іконку, заставку та анімований екран завантаження S-01 з маскотом. Пізніше того ж дня: бібліотека «Слова» (S-14: пошук, чіпи статусів, вибір рівня й категорії), екран «Я» та «Налаштування» (S-18: інтервал, покази, сповіщення, тихі години, рівень, категорії, темп, утримуваність, тема, гейміфікація) з автозбереженням і тостом; власник вирішив: звертання «ти», онбординг питає всіх (журнал, п. 9 і 11). Онбординг S-02…S-09 реалізовано (без S-07 віджета, що чекає на поверхні): нікнейм за T10, рівень, категорії з лічильником, темп, сповіщення з системним запитом на Android 13+, приватність, перше слово; прапорець завершення в Preferences, назва застосунку на пристрої «Реп’ях». Ранній зріз гейміфікації (спека 07, домен `Burdock.Domain.Rewards`): XP і рівні з титулами, колючки з денними лімітами, щоденна серія з віхами, 15 досягнень, ledger у SQLite; підсумок сесії показує нагороди й черговані святкування (`CelebrationOverlay`, конфеті, хептика), на «Сьогодні» бейджі серії й колючок, у профілі рівень і сітка досягнень; сесія з комбо-чіпом, тремтінням при помилці й хептикою. Увечері 28.09.2026 закрито технічний борг фази 1: golden-тести FSRS (219 векторів py-fsrs 6.3.2 у `tests/fixtures/fsrs`, Fsrs.Sharp 2.0.0 збігається з допуском 1e-6), тести ViewModel-ів (`tests/Burdock.Presentation.Tests`, нейтральний net10.0-head Presentation, спільні фейки в `Burdock.TestKit`), скелетон «Сьогодні» (`controls:Skeleton`, поява після 300 мс), стани focused/loading для `PushButton` (кільце фокуса, Enter/Space на Android, спінер `IsLoading`), міні-тест рівня S-03a (`LevelPlacement` у домені, «Не знаю — перевірити за хвилину» на кроці рівня). Того ж дня власник закрив пункти 10, 12, 13 журналу і додав дев'яту обов'язкову категорію «Військова справа» (`military`, доповнення Q18). Озвучення (ADR 0019): системний TTS підтягнуто (найкращий голос акценту, темп, озвучення прикладу на картці); 30.09.2026 власник підтвердив рушій Kokoro-82M і вибір голосу користувачем: стадія `audio render` (content/pipeline/audio, Python + ffmpeg, CLI `burdock-content audio render`), seed-аудіо 54 слів у 4 голосах (af_heart, am_michael, bf_emma, bm_george) як MauiAsset `content/audio/**`, порт `IPronunciationAudio`, `SpeechService` грає кліп обраного голосу, інакше системний TTS того ж акценту; у S-18 список «Голос» із прослуховуванням, один голос на пристрій. Контент-пайплайн v0 виконано 01.10.2026 без API-ключа, воркфлоу агентів Claude Code (рішення власника «генеруй 10 000 слів»): інвентар CEFR-J v1.6 A1–B2 плюс запропоновані моделлю C1/C2 = 9 806 лексем у 249 чанках, промти `content/pipeline/prompts/*-v1.md`, генерація → незалежна рецензія → виправлення, середній бал 4,91; `build_packs.py` зібрав 61 пакет (9 792 записи) з `index.json`, застосунок імпортує їх при першому запуску (~10 с на емуляторі), бібліотека «Слова» показує 9 792 слова (список замінюється цілком, фільтри рівня й теми виконуються в SQL), підсвітка леми у прикладі за цілим словом. Відкрито: підтвердження комерційного використання CEFR-J (питання 7), рівні C1/C2 — оцінка моделі, 14 записів у черзі для людини. Того ж дня (ADR 0020) на сервері з'явився модуль `Pronunciation` (`src/server/Burdock.Server.Modules.Pronunciation`): адмін-ендпоінт ставить у чергу вимову нових слів, фоновий воркер рендерить їх через сайдкар Kokoro (`content/pipeline/audio/server.py`, образ `infra/kokoro/Dockerfile`) і віддає як незмінні кліпи; у v1 сховище у теці та стан у пам'яті, PostgreSQL і Object Storage у фазі 2; перевірено наживо локально. Повний словник рендериться `render_audio.py` (по процесу на голос, `--threads 2`). 02.10.2026 у застосунок вбудовано аудіо безкоштовного пулу: 600 слів × 4 голоси (9,1 МБ, `content/seed/audio/`, `bundle_audio.py` з перевіркою SHA-256), на емуляторі картка слова з пулу грає кліп через MediaPlayer; повний словник (39 168 кліпів) рендериться на ПК власника у `content/generated/uk-en/audio/` (не комітиться) до появи Object Storage. Issue #4 закрито 02.10.2026: перевірка вбудованого контенту читає лише індекс і версії одним запитом (на емуляторі 4,3–5,7 с → 0,8–1,2 с холодний старт, ~50 мс теплий), план дня зберігає денний знімок нових слів у `daily_plan_cache` (~1 с → 0,25–0,33 с холодний, ~50 мс теплий), планувальник бере спочатку обраний рівень, пул дистракторів сесії звужено до рівнів і тем учня. Фаза 2 «Сервер» розпочата 02.10.2026: спільна персистентність (EF Core 10 + Npgsql, схема на модуль, `IModuleDatabase`, `api migrate`, readiness з перевіркою міграцій, Testcontainers PostgreSQL 18 у тестах), модуль Identity (анонімний акаунт `POST /v1/identity/anonymous` з інсталяційним секретом, OpenIddict 7.7 з грантом `urn:burdock:grant:installation` і ротованими reference refresh-токенами, `GET/PATCH /v1/identity/me`), `GET /v1/config`, політики авторизації та rate limiting за спекою 09 §3.2.1, compose для Hetzner (`infra/compose`), воркфлоу publish-server/deploy-server; модуль Content (`content.packs`, `api content publish` з gzip і SHA-256 у FileSystem/S3-сховище, маніфест з БД за entitlement, тимчасово `Entitlements:DefaultState` = Trial); модуль LearnerProgress (журнал подій `progress.events` з `server_seq` за учнем, `POST`/`GET /v1/progress/events`: ідемпотентність за id, валідація конвертів, пагінація, виключення власної інсталяції); модуль SettingsSync (`GET`/`PUT /v1/settings`, LWW за ключем з детермінованим розв'язанням нічиїх, сильний ETag). 11.10.2026 підключено клієнтську синхронізацію (спека 09 §3.3–3.6): спільні правила `Burdock.Domain.Sync` (типи подій прогресу, LWW налаштувань), порти `ISyncGateway`/`IDeviceCredentials`/`IProgressOutbox`/`ISyncState`/`ISyncableSettings`, use case-и `SyncProgressUseCase` (outbox → push пакетами по 200, pull після курсора без власних подій, ідемпотентне відтворення в картки через планувальник), `SyncSettingsUseCase` (LWW в обидва боки, у спокої один GET), `RefreshContentUseCase` (маніфест з ETag, SHA-256 gz-пакета, імпорт), `SyncUseCase` (акаунт → прогрес → налаштування → контент); use case-и навчання кладуть у outbox `ReviewLog`/`ExposureConfirmed`/`CardIntroduced`; `HttpSyncGateway` (refresh-токен у SecureStorage, access у пам'яті, повтор після 401, installation grant і реєстрація з локальним id учня як запропонованим id акаунта, ротація інсталяції при `installation_conflict`, `sync.offline` при збоях), `sync_outbox`/`sync_state` у SQLite, штампи змінених ключів у Preferences, `SyncCoordinator` у Presentation (поява Shell, resume, після сесії, без накладання); сервер приймає необов'язковий `LearnerId` при реєстрації (409 `learner_id_taken`). Перевірено на емуляторі проти локального API: реєстрація під локальним id учня, токени, push події показу, pull, маніфест 200 → 304. Dev-Postgres у compose на хост-порту 5433 (5432 на машині власника зайнятий локальною службою PostgreSQL 16). 12.10.2026: контракт API зафіксовано у `docs/api/openapi.v1.json` (трансформери `OpenApiContract`, контрактний тест `tests/Burdock.Server.ContractTests` на Testcontainers, оновлення через `BURDOCK_UPDATE_SNAPSHOTS=1`; чернетка ADR 0021 про спільні DTO замість Kiota чекає на власника); end-to-end тест `tests/Burdock.Sync.EndToEnd.Tests` ганяє реальний `HttpSyncGateway` двох інсталяцій одного акаунта проти серверного хоста (прогрес, LWW налаштувань, пакет через маніфест) і виявив дефект точності штампів (виправлено: `SettingsEntry.NormalizeStamp`, мілісекунди на обох боках); сервер інструментовано OpenTelemetry (`AddBurdockObservability`: трасування/метрики/логи, експорт OTLP лише за `OTEL_EXPORTER_OTLP_ENDPOINT`, health-проби не трасуються, тест на відсутність адрес клієнта у спанах). Того ж дня модуль Pronunciation отримав персистентність: схема `pronunciation.clips`, відновлення черги після рестарту, спільне об'єктне сховище `Burdock.Server.Shared.Storage` (FileSystem/S3, тепер і для пакетів Content) і команда `api pronunciation publish`, яка реєструє відрендерений словник; повний рендер завершено (39 168 кліпів × 4 голоси, індекс перезібрано), публікація чекає на бакет Object Storage у власника. 12.10.2026 власник зробив репозиторій публічним (source-available, `LICENSE` «усі права захищені», журнал п. 14): стандартні раннери безкоштовні, усі воркфлоу зелені. 15.10.2026 підготовлено реліз у TestFlight: воркфлоу `release-ios` (тег `mobile/v*`, environment `prod-ios`, підпис із base64-секретів, `altool` з API-ключем, `dry_run` без підпису для перевірки AOT), runbook `docs/runbooks/release-ios.md` із сертифікатами через OpenSSL без Mac, `ITSAppUsesNonExemptEncryption` в Info.plist; 27.10.2026 перша збірка 0.1.0 (build 2, тег `mobile/v0.1.0`) завантажена в TestFlight з CI: знадобилися перепакування `.p12` у legacy-формат PKCS12 і запис застосунку в App Store Connect (обидва випадки описані в runbook); підписана збірка з AOT і завантаження займають ~15 хв. Далі у фазі 2: Entitlements, довантаження кліпів у застосунку, Sentry після рішення власника, колектор/алерти/дашборди після акаунтів Grafana Cloud, `oasdiff` у CI, `ExposureDaily`/`WordListChanged`, деплой на Hetzner. Решта фази 1 (відкриті issue про сідований fuzz FSRS і розбіжність слагів категорій між спекою 08 і застосунком); у фазі 4 — заморозки/пауза/відновлення серії, календар входу, мікрозавдання, каталог косметики; потім фаза 2 (сервер: анонімний акаунт, маніфест, синхронізація).

Локальний цикл: емулятор `burdock_api36` (ANDROID_AVD_HOME=D:	mpndroid-homevd), деплой лише через `dotnet build ... -t:Install` (Fast Deployment; `adb install` APK і `pm clear` ламають запуск), знімки `adb exec-out screencap -p`.
