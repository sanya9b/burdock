# design/ — дизайн-система «Реп’ях» (Burdock)

Папка містить машиночитані артефакти дизайн-системи. Специфікація-джерело: [`docs/specs/02-design-system.md`](../docs/specs/02-design-system.md). Рішення продукту: [`docs/decisions/2026-09-25-interview.md`](../docs/decisions/2026-09-25-interview.md).

## Що тут лежить

| Шлях | Зміст | Хто редагує |
|---|---|---|
| `tokens/tokens.json` | Єдине джерело істини для кольорів (примітиви, семантика light/dark, теми віджета), типографіки, відступів, радіусів, тіней, руху. Формат W3C Design Tokens (`$type`/`$value`, аліаси `{color.primitive.violet.600}`, розв’язані hex у `$extensions["org.burdock.tokens"].resolved`) | Дизайнер, через PR з оновленими таблицями у специфікації |
| `tokens/generate.py` | Генератор платформових файлів і перевірка контрасту (зараз це зародок: скрипт обчислює OKLCH-рампи, контрасти WCAG і записує tokens.json, tables.md, vars.css; CLI з --check/--out за інтерфейсом розділу 3.16 специфікації буде добудовано у фазі 0) | Розробник |
| `preview/index.html` | Самодостатнє HTML-прев’ю без збірки: рампи, семантичні кольори з обчисленим контрастом, типографіка, компоненти, віджети iOS/Android у світлому, темному й tinted режимах, сповіщення, Live Activity, перемикач Light/Dark | Генератор оновлює блок CSS-змінних між маркерами; розмітку — дизайнер |
| `mascot/` | Джерела маскота Реп’яшка (SVG), експорти PNG, Lottie JSON | Дизайнер / ілюстратор |

## Регенерація платформових файлів

```bash
python design/tokens/generate.py --check          # валідація JSON, аліасів і контрасту (CI, код виходу 1 при провалі)
python design/tokens/generate.py --out all        # усі цілі нижче
python design/tokens/generate.py --out xaml       # src/mobile/Burdock.Presentation/Resources/Styles/Colors.xaml, Styles.xaml, WidgetThemes/*.xaml
python design/tokens/generate.py --out swift      # src/mobile/ios-extensions/BurdockWidgets/DesignTokens.swift
python design/tokens/generate.py --out android    # src/mobile/Burdock.Presentation/Platforms/Android/Resources/values{,-night}/colors.xml, values/dimens.xml
python design/tokens/generate.py --out css        # блок змінних у design/preview/index.html між <!-- tokens:start --> і <!-- tokens:end -->
python design/tokens/generate.py --report         # Markdown-таблиці контрасту для специфікації
```

Правила: згенеровані файли не редагуються вручну (CI порівнює їх із результатом генератора); новий токен додається в обидва режими з `$description` і, якщо він несе текст або межу, — з парою контрасту у списку перевірки генератора; жодного hex-коду в XAML/Swift/Kotlin поза згенерованими файлами.

Потрібен лише Python 3.13 зі стандартною бібліотекою (Windows, macOS, Linux). Порядок іменування: `color.semantic.light.brand.primary` → XAML `ColorBrandPrimaryLight`, Swift `DesignTokens.Semantic.brandPrimary`, Android `@color/color_brand_primary`, CSS `--color-brand-primary`.

## Прев’ю

Відкрити `design/preview/index.html` у будь-якому сучасному браузері (подвійний клік, без сервера). Сторінка завантажує Fixel Text/Display з jsDelivr (`https://cdn.jsdelivr.net/gh/MacPaw/Fixel@main/fonts/webfonts/*.woff2`, OFL 1.1) і Inter з Google Fonts як запасний варіант; без мережі рендериться системним шрифтом, усе інше працює офлайн. Перемикач «Світла / Темна» у шапці змінює `data-theme` на `<html>`; блок «Семантичні кольори» завжди показує обидві теми поруч із коефіцієнтами контрасту, обчисленими в браузері за формулою WCAG 2.2 — вони мають збігатися з таблицею 3.3.3 специфікації.

Для рев’ю реальних екранів прев’ю не замінює знімків: Android-емулятор локально (`adb exec-out screencap -p`), iOS-симулятор у CI (`xcrun simctl io booted screenshot`), у світлій і темній темі та при AX5 / fontScale 2.0.

## Маскот: де лежать ресурси

```
design/mascot/
  src/                     SVG-джерела: один файл на стан + окремі шари вбрання
    mascot_joy.svg
    mascot_cheer.svg
    mascot_surprise.svg
    mascot_sad.svg
    mascot_sleep.svg
    mascot_party.svg
    mascot_think.svg
    mascot_proud.svg
    outfit_hat_party.svg   шари вбрання на тому ж полотні 1024×1024
  png/                     експорти для платформ (див. іменування нижче)
  lottie/                  mascot_party.json, mascot_sleep.json, mascot_think.json (≤ 150 KB, петля ≤ 3 с)
  brief.md                 бриф для генерації (копія з розділу 3.10 специфікації) і журнал ітерацій
```

У застосунку MAUI використовуються SVG через `MauiImage` (растри генеруються на збірці). PNG потрібні лише для WidgetKit-розширення, Android RemoteViews і сповіщень.

## Іменування ресурсів

Шаблон: `mascot_{state}_{size}@{scale}.png`, де

- `state` ∈ `joy | cheer | surprise | sad | sleep | party | think | proud`;
- `size` — токен розміру: `xs` (32 pt), `sm` (56 pt), `md` (96 pt), `lg` (160 pt), `xl` (240 pt);
- `scale` — `1x | 2x | 3x` для iOS. Для Android той самий файл кладеться у `drawable-mdpi | -hdpi | -xhdpi | -xxhdpi | -xxxhdpi` без суфікса масштабу.

Приклади: `mascot_cheer_md@3x.png` (iOS, 288×288 px), `drawable-xxhdpi/mascot_cheer_md.png` (Android, 288×288 px). Вбрання: `outfit_{id}_{size}@{scale}.png` на тому ж полотні, що й стан. Іконки застосунку: `appicon_master_1024.png`, шари `appicon_layer_{bg|body|eyes}.svg`; монохромний силует для сповіщень Android: `ic_stat_burdock.xml` (vector drawable, лише альфа). Файли теми віджета в XAML: `WidgetThemes/{Tsvit|Luh|Nich|Sontse}.xaml`.

## Налаштування експорту

- SVG: полотно 1024×1024, персонаж займає 80 % висоти, вирівняний по центру; без растрів, без градієнтних мешів (Lottie і MauiImage їх не підтримують); кольори лише з рамп `tokens.json` (`violet.300/400/600`, `lime.400/600`, `neutral.0/950`, `coral.200`).
- PNG: sRGB, 8 біт, з альфою, без вбудованого профілю (sRGB мається на увазі), оптимізовано (oxipng/pngquant без втрати кольорів обличчя); у WidgetKit-розширення потрапляють лише `sm` і `xs` у 2x/3x, ≤ 60 KB кожен.
- Lottie: експорт із Bodymovin/аналога, версія 5.x JSON, 30 fps, без експресій і растрів, ≤ 150 KB; перевірка відтворення у `SKLottieView` (SkiaSharp.Extended.UI.Maui 3.0.0) на емуляторі.
- Іконка застосунку: майстер 1024×1024 без прозорості й без заокруглення (система додає маску); адаптивна іконка Android — передній шар з безпечною зоною 66 %, фон однотонний `violet.600`, monochrome-шар для тематичних іконок.
- Шрифти: у застосунок вбудовуються статичні TTF Fixel (Text Regular/Medium/SemiBold/Bold, Display SemiBold/Bold) з OFL-файлом ліцензії у `src/mobile/Burdock.Presentation/Resources/Fonts/`; IPA завжди системним шрифтом (Fixel не має блоку IPA — перевірено 2026-09-27).
