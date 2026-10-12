# Реп'ях (Burdock)

Україномовний застосунок для вивчення англійських слів, які «чіпляються» до вас на віджетах, екрані блокування та у сповіщеннях, а потім закріплюються короткими вправами. .NET MAUI (iPhone та iPad спершу, Android далі), ASP.NET Core на сервері, Clean Architecture, сильний акцент на дизайні.

> Статус (12.10.2026): фаза 1 «Ядро навчання» завершена (екрани «Сьогодні», картка слова, сесія, бібліотека, онбординг, рання гейміфікація, 9 792 слова у вбудованих пакетах, озвучення Kokoro-82M), фаза 2 «Сервер» триває: Identity, Content, LearnerProgress, SettingsSync і Pronunciation на PostgreSQL, клієнтська синхронізація, OpenAPI-контракт у `docs/api/`, OpenTelemetry. Деплой на Hetzner ще попереду. Деталі в [CLAUDE.md](CLAUDE.md), розділ 11.

## З чого почати

| Хочу… | Читати |
|---|---|
| Зрозуміти продукт за 10 хвилин | [docs/SPEC.md](docs/SPEC.md) |
| Працювати над задачею як розробник або ШІ-агент | [CLAUDE.md](CLAUDE.md), потім розділ 0 потрібної специфікації в [docs/specs/](docs/specs/) |
| Дізнатися, чому вирішили саме так | [docs/decisions/2026-09-25-interview.md](docs/decisions/2026-09-25-interview.md), [docs/adr/](docs/adr/) |
| Перевірити факт про платформу, бібліотеку чи ліцензію | [docs/research/2026-09-25-research-brief.md](docs/research/2026-09-25-research-brief.md) |
| Побачити дизайн | [docs/specs/02-design-system.md](docs/specs/02-design-system.md), відкрити `design/preview/index.html` у браузері |
| Побачити план | [docs/specs/17-roadmap.md](docs/specs/17-roadmap.md) |

## Структура

```
docs/        специфікації, ADR, журнал рішень, дослідження, глосарій, промти
design/      дизайн-токени (джерело істини), HTML-прев'ю, маскот
src/mobile/  Burdock.Application / Infrastructure / Presentation, Swift-розширення віджетів
src/server/  Burdock.Server.Api + модулі (Identity, Content, LearnerProgress, Rewards, Entitlements, SettingsSync, Notifications, Analytics)
src/shared/  Burdock.Domain (спільний для клієнта й сервера, T23), Burdock.Contracts
content/     контент-пайплайн, джерела, власні списки
infra/       Docker Compose, Caddy, pgBackRest, деплой
tests/       тести за шарами, архітектурні тести
```

## Ключові рішення

- iOS 16+, iPad з першого дня; Android API 26+ зі спільної бази, реліз через 1-2 місяці після iOS.
- Слово змінюється за інтервалом, кнопкою «наступне» у віджеті (iOS 17+) або при відкритті застосунку. Live Activity лише як сесія, яку запускає користувач.
- Анонімний акаунт без email; Apple/Google лише для синхронізації. Сервер обов'язковий, контент кешується.
- Freemium: тріал 7 днів через магазин, далі 5 слів на день безкоштовно, Pro місяць/рік.
- Власна база слів без ShareAlike-джерел; переклади й приклади генерує ШІ з рецензуванням.
- Hetzner (ЄС), PostgreSQL, Sentry EU, жодних сторонніх аналітичних SDK.
- Палітра «Реп'ях-цвіт», шрифти Unbounded і Rubik, іконки Lucide, маскот Реп'яшок.

## Мова

Документація українською, код англійською. Деталі у [CLAUDE.md](CLAUDE.md), розділ 3.

## Ліцензія

Source-available, усі права захищені: репозиторій можна читати й вивчати, але не використовувати без письмового дозволу, див. [LICENSE](LICENSE). Ліцензії використаних джерел контенту й бібліотек перелічені у `content/sources/README.md` та в екрані «Джерела та ліцензії» застосунку.
