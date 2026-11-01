# Скіли та інструменти для ШІ-агентів у цьому проєкті

Власник вимагає високої якості дизайну, тому агенти, які торкаються інтерфейсу, мають працювати з перевіреними дизайн-скілами. Перелік складено за дослідженням від 25.09.2026 (розділ 4 звіту); перед встановленням перевіряйте актуальність за посиланням.

## Обов'язкові до встановлення

| Скіл / інструмент | Джерело | Для чого в цьому проєкті | Обмеження |
|---|---|---|---|
| **dotnet-maui** (плагін `dotnet-agent-skills`) | https://github.com/dotnet/skills/tree/main/plugins/dotnet-maui | Офіційні скіли Microsoft: maui-theming, shell-navigation, DI, CollectionView, продуктивність | Загальні поради; правила нашої дизайн-системи мають пріоритет |
| **maui-skills** (David Ortinau) | https://github.com/davidortinau/maui-skills | ux-first-principles, ux-mobile, accessibility, theming, localization для MAUI | MIT; невеликий обсяг, читати як чекліст |
| **ui-ux-pro-max** | https://github.com/nextlevelbuilder/ui-ux-pro-max-skill | Довідник палітр, пар шрифтів і UX-правил для міркувань про макети | Немає профілю MAUI: використовувати лише як довідник, ніколи не давати йому генерувати код |
| **Excalidraw MCP** | https://github.com/yctimlin/mcp_excalidraw | Діаграми потоків і архітектури, які комітяться поруч зі специфікаціями | MIT |
| **Google Stitch MCP** | https://github.com/google-labs-code/stitch-sdk | Швидкі концепт-екрани для обговорення перед XAML | Вихід HTML, не MAUI; лише для концептів |

Команди встановлення (Claude Code):

```
/plugin marketplace add dotnet/skills
/plugin install dotnet-maui@dotnet-agent-skills
/plugin marketplace add davidortinau/maui-skills
```

## За потреби

| Скіл / інструмент | Коли |
|---|---|
| `frontend-design` з anthropics/skills | Лише для веб-поверхонь: сторінка видалення акаунта, політика приватності, маркетингова сторінка burdock.org |
| Claude Design | Скаффолдинг дизайн-системи та синхронізація з токенами, якщо доступний план |
| Figma MCP | Якщо з'явиться Dev/Full-місце Figma (ліміти викликів для безкоштовних планів занадто малі) |
| maui-ai-design-skill (mallibone) | Генерація Colors.xaml/Styles.xaml; у репозиторії немає файлу ліцензії, копіювати лише після дозволу автора |

## Не використовувати

- Скіли для web-рев'ю на основі Playwright: вони не бачать MAUI. Дизайн-рев'ю робиться знімками з Android-емулятора локально та iOS-симулятора в CI плюс Accessibility Inspector.
- `brand-guidelines` з anthropics/skills: це бренд Anthropic, не наш.

## Встановлені в репозиторії (`.claude/skills/`)

| Скіл | Джерело | Для чого | Примітки |
|---|---|---|---|
| **vector-illustration** | https://github.com/syi0808/svg-skills (MIT), встановлено 01.11.2026 через `npx skills add syi0808/svg-skills --skill vector-illustration illustration-plan -a claude-code --copy` | Метод для процедурних ілюстрацій маскота й косметики (`MascotView`, `MascotView.Outfits.cs`): план семантичних частин → малювання з навмисними перекриттями без волосяних швів → рендер → перевірка за таблицею (повнота частин, силует і пропорції, з'єднання, перекриття, композиція, негативний простір, система ліній і кольору, читабельність у малому розмірі) → не більше трьох раундів правок | Наш носій — C# `GraphicsView`, а не SVG-файли, тому скрипти рендера скіла не використовуються; рендер і перевірка робляться знімками з емулятора у світлій і темній темі та в розмірах `mascot.lg` і `mascot.sm`. Серед скілів для ілюстрацій на skills.sh він мав найзмістовніший метод; лідер за встановленнями (`narumiruna/svg-illustration`, 645) є лише вказівником на правила діаграм |
| **illustration-plan** | той самий пакет | Словник арт-напряму (настрій, мова рендерингу, матеріал, палітра, деталізація) і план частин перед малюванням | Використовується разом із 02 §3.10 |

## Проєктні скіли (плануються)

У цій теці з'являться власні скіли, коли буде код: `burdock-ui-review` (чекліст дизайн-QA з 02), `burdock-tokens` (генерація ресурсів із tokens.json), `burdock-content-review` (рубрика рецензування контенту з 08).
