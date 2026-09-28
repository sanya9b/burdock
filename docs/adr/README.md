# Architecture Decision Records (ADR)

ADR фіксує одне значуще рішення: контекст, варіанти, вибір і наслідки. Формат: MADR (скорочений). Мова: українська, ідентифікатори англійською.

## Коли ADR обов'язковий

1. Будь-яка зміна рішення з `docs/decisions/*.md`.
2. Нова зовнішня залежність (NuGet, Swift Package, сервіс, SDK) або зміна ліцензійної політики.
3. Новий тип персональних даних, новий аналітичний івент, нова інтеграція з третьою стороною.
4. Зміна межі між шарами чи модулями, нова технологія зберігання або транспорту.
5. Відмова від вимоги Must у специфікації.

## Нумерація та статуси

- Файл `NNNN-kebab-title.md`, номер послідовний, ніколи не перевикористовується.
- Статуси: `Proposed` → `Accepted` → (`Deprecated` | `Superseded by NNNN`).
- ADR не редагується після прийняття, окрім поля статусу та посилання на наступника. Нова думка = новий ADR.

## Шаблон

```markdown
# NNNN. Назва рішення

| Поле | Значення |
|---|---|
| Статус | Proposed / Accepted / Deprecated / Superseded by NNNN |
| Дата | YYYY-MM-DD |
| Рішення журналу | Qn / Tn (якщо стосується) |
| Специфікації | NN-… |

## Контекст
Яка проблема чи сила змушує вирішувати. Факти з посиланнями.

## Варіанти
1. …  плюси / мінуси
2. …

## Рішення
Що обрано і чому саме це, одним-двома абзацами.

## Наслідки
Позитивні, негативні, що доведеться робити тепер, як перевірити.
```

## Індекс

| № | Назва | Статус | Дата |
|---|---|---|---|
| [0001](0001-dotnet-maui-clean-architecture.md) | .NET MAUI та Clean Architecture як основа мобільного застосунку | Accepted | 2026-09-27 |
| [0002](0002-ios-first-android-shared-codebase.md) | iOS спершу, Android зі спільної кодової бази пізніше | Accepted | 2026-09-27 |
| [0003](0003-minimum-ios-16-conditional-ios-17.md) | Мінімум iOS 16 з умовними функціями iOS 17+ | Accepted | 2026-09-27 |
| [0004](0004-rotation-triggers-time-and-tap.md) | Тригери зміни слова: час і натискання, без розблокування/свайпу | Accepted | 2026-09-27 |
| [0005](0005-live-activity-user-started-session.md) | Live Activity лише як сесія, запущена користувачем | Accepted | 2026-09-27 |
| [0006](0006-anonymous-first-identity.md) | Анонімна ідентичність з опційною прив'язкою Apple/Google | Accepted | 2026-09-27 |
| [0007](0007-server-mandatory-cache-first.md) | Сервер обов'язковий, контент кешується після першого запуску | Accepted | 2026-09-27 |
| [0008](0008-fsrs6-passive-exposure-never-grades.md) | FSRS-6; пасивні покази не оцінюють картку | Accepted | 2026-09-27 |
| [0009](0009-content-without-sharealike.md) | База слів без ShareAlike-джерел | Accepted | 2026-09-27 |
| [0010](0010-hosting-hetzner-postgres.md) | Хостинг Hetzner (ЄС), Docker Compose, PostgreSQL 18 | Accepted | 2026-09-27 |
| [0011](0011-native-subscriptions-server-validation.md) | Нативні підписки з серверною валідацією | Accepted | 2026-09-27 |
| [0012](0012-first-party-analytics-sentry-eu.md) | Власна opt-in аналітика та Sentry EU | Accepted | 2026-09-27 |
| [0013](0013-design-tokens-single-source.md) | Дизайн-токени як єдине джерело істини; Fixel і Lucide | Accepted | 2026-09-27 |
| [0014](0014-monorepo-github-actions.md) | Монорепозиторій на GitHub, trunk-based, macOS-раннери | Accepted | 2026-09-27 |
| [0015](0015-two-counter-exposure-semantics.md) | Два лічильники показів: заплановані та підтверджені | Accepted | 2026-09-27 |
| [0016](0016-cosmetic-rewards-client-granted.md) | Косметичні нагороди, нарахування клієнтом зі звіркою сервером у v1 | Accepted | 2026-09-27 |
| [0017](0017-playful-relief-ui-language.md) | Ігрова мова інтерфейсу: рельєфні кнопки, картки без контуру, власний хедер | Accepted | 2026-09-28 |
| [0018](0018-typography-unbounded-rubik-brand-mark.md) | Типографіка Unbounded + Rubik, брендовий знак і анімований екран завантаження | Accepted | 2026-09-28 |
