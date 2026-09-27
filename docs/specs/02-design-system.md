# 02. Дизайн-система «Реп’ях»

| Поле | Значення |
|---|---|
| Статус | Draft |
| Версія | 0.1 |
| Дата | 2026-09-27 |
| Власник | Власник продукту / Провідний дизайнер продукту |
| Джерела рішень | [Журнал рішень 2026-09-25](../decisions/2026-09-25-interview.md): Q1, Q8/Q19, Q12, Q14/Q32, Q20, Q21, Q25, Q34, Q35, Q36, T4, T5, T6, T11, T17, T19, T20 |
| Пов’язані специфікації | майбутні специфікації віджетів iOS/Android, сповіщень і Live Activity, нагород, підписки, доступності (номери призначає архітектор); машиночитані артефакти: `design/tokens/tokens.json`, `design/preview/index.html`, `design/README.md` |

## 0. Контекст для ШІ-агента

Ти працюєш над інтерфейсом застосунку «Реп’ях» (у коді та англомовних сторах: Burdock) — .NET MAUI 10 застосунок для вивчення англійських слів, що показує слова на віджетах, екрані блокування, у сповіщеннях і Live Activity. Власник вимагає сильного акценту на якості UI/UX. Ця специфікація — контракт для кожного екрана, віджета, сповіщення й анімації.

Перед роботою прочитай: (1) журнал рішень `docs/decisions/2026-09-25-interview.md` (Q8/Q19 стиль і палітра, Q20 маскот, Q34 косметика, T6 шрифт/іконки, T19 Material You, T20 токени); (2) розділ 4 і рядки про режими рендерингу віджетів у розділі 1 дослідницького звіту `docs/research/2026-09-25-research-brief.md`; (3) `design/tokens/tokens.json` — єдине джерело істини для кольорів, типографіки, відступів, радіусів, тіней і руху.

Правила, які не можна порушувати: жодного hex-коду в XAML/Swift/Kotlin/C# поза згенерованими файлами; кожна пара «текст на поверхні» ≥ 4.5:1 (великий текст та UI-межі ≥ 3:1) у світлій і темній темах; лайм `#B4F461` ніколи не є текстом на світлому фоні; сенс ніколи не передається лише кольором; Реп’яшок — максимум один на екран, ніколи не соромить користувача; віджет проєктується «текст і форма спочатку», бо у tinted/clear режимах iOS колір зникає; Fixel для інтерфейсу, системний шрифт для IPA; SF Symbols лише всередині iOS-розширення; апостроф в UI-рядках — U+2019.

Код напрямку: `src/mobile/Burdock.Presentation/Resources/Styles/*` (згенеровані `Colors.xaml`, `Styles.xaml`), `src/mobile/Burdock.Presentation/Controls/*` (компоненти), `src/mobile/ios-extensions/BurdockWidgets/DesignTokens.swift`, `src/mobile/Burdock/Platforms/Android/Resources/values{,-night}/colors.xml`, `design/*`.

Як перевірити результат: `python design/tokens/generate.py --check` (валідність токенів і контрасту) має завершуватись кодом 0; відкрий `design/preview/index.html` у браузері та порівняй з екраном; зроби знімок екрана з Android-емулятора (локально) або iOS-симулятора (артефакт CI) і пройди чеклист розділу 3.18 (20 пунктів); перевір AX5 / Android 200% і Reduce Motion.

## 1. Мета й межі

**Мета.** Дати одну мову дизайну для трьох рендерерів (MAUI, WidgetKit/SwiftUI, Android RemoteViews) і трьох поверхонь (застосунок, віджети, сповіщення), щоб продукт виглядав цілісно, грайливо й «сучасно», лишався читабельним з першого погляду і проходив WCAG 2.2 AA у світлій і темній темах.

**У v1 входить:** принципи; бренд (назва, іконка, тон); кольорова система з примітивами, семантичними наборами light/dark і токенами віджетів; 4 теми віджетів як косметична нагорода (Q34); типографіка Fixel; сітка, форма, тіні, рух, хептика; іконографія; гайд маскота Реп’яшка з 8 станами; інвентар компонентів; макети віджетів iOS (усі родини + StandBy) і Android (2×2, 4×2, 4×4); шаблони сповіщень і Live Activity «сесія» (Q35); правила темної теми; доступність; генератор платформових файлів із `tokens.json`; інструменти дизайну та ШІ-навички; чеклист дизайн-QA.

**Не входить у v1:** теми всього застосунку (лише віджет-теми й системна light/dark); анімований маскот у віджетах; Apple Watch, Mac Catalyst, клавіатурне розширення (Q1, Q12); соціальні поверхні (Q24); реклама (Q14); власні шрифти в Android RemoteViews (технічно неможливо).

**Відкладено:** Lottie-анімації понад базові три (v1.1); іконка з шарами для iOS 26 у повному обсязі (v1.1, після перевірки інструментів на CI); per-instance вибір теми віджета через конфігурацію віджета (v1.1).

## 2. Вимоги

| ID | Вимога | Пріоритет |
|---|---|---|
| FR-02-1 | Усі кольори, шрифтові ролі, відступи, радіуси, тіні й тривалості анімацій визначаються у `design/tokens/tokens.json` (тришарова модель primitive → semantic → component, T20) і потрапляють у код лише через генератор | Must |
| FR-02-2 | Семантичні набори `light` і `dark` існують для кожного токена кольору; застосунок стежить за системною темою через `AppThemeBinding`, користувач може примусово обрати тему в налаштуваннях | Must |
| FR-02-3 | Кожна пара «текст/поверхня» з розділу 3.3.3 має контраст ≥ 4.5:1 (великий текст ≥ 3:1, UI-межі ≥ 3:1) в обох темах; перевірка автоматизована у `generate.py --check` і блокує CI | Must |
| FR-02-4 | Типографічна шкала з 17 ролей на Fixel Text/Display з визначеними межами масштабування Dynamic Type/fontScale; IPA рендериться системним шрифтом | Must |
| FR-02-5 | Інвентар компонентів розділу 3.11 реалізований як багаторазові контроли зі станами default/pressed/disabled/focused/loading/error і без hex-кодів | Must |
| FR-02-6 | Віджети iOS для systemSmall/Medium/Large, accessoryRectangular/Circular/Inline та StandBy виглядають коректно у fullColor, accented (tinted, iOS 18+) і vibrant режимах; сенс не залежить від кольору | Must |
| FR-02-7 | Чотири теми віджета (Цвіт, Луг, Ніч, Сонце) визначені як перекриття `color.widget.*`, кожна проходить контрастну перевірку, застосовується у застосунку через `DynamicResource`, у Swift через `WidgetTheme`, в Android через ресурси кольорів | Should |
| FR-02-8 | Маскот Реп’яшок має 8 статичних станів (SVG + PNG) у 5 розмірах; три Lottie-анімації (свято, сон, задума) з статичним кадром для Reduce Motion | Must (статичні) / Should (Lottie) |
| FR-02-9 | Шаблони сповіщень розділу 3.13 реалізовані з категоріями дій; жодного loss-framing (T17) | Must |
| FR-02-10 | Live Activity «сесія» має макети для екрана блокування та Dynamic Island (compact leading/trailing, minimal, expanded) | Must |
| FR-02-11 | Android-віджети 2×2, 4×2, 4×4 підтримують Material You dynamic color (API 31+) з відкатом на брендові кольори (T19) | Should |
| FR-02-12 | `design/preview/index.html` показує ті самі значення токенів, що й `tokens.json`; розбіжність вважається дефектом | Should |
| FR-02-13 | Іконки: Lucide у застосунку, SF Symbols лише в iOS-розширенні, Material Symbols лише в Android RemoteViews; активний стан таб-бару має заповнений варіант | Must |
| FR-02-14 | Кожна сигнатурна анімація має еквівалент для Reduce Motion і мапу хептики | Should |
| FR-02-15 | Користувач може вимкнути гейміфікаційні прикраси («без гейміфікації»), при цьому компоненти StreakBadge, CurrencyPill, RewardCard приховуються без зламу макета | Could |
| NFR-02-1 | WCAG 2.2 AA для контрасту, розміру цілей (44 pt iOS / 48 dp Android) і не-кольорової індикації | Must |
| NFR-02-2 | Усі екрани працюють при Dynamic Type AX5 та Android fontScale 2.0 без обрізання слова, перекладу чи кнопок (перетікання в стовпчик) | Must |
| NFR-02-3 | Reduce Motion / вимкнені анімації системи вимикають зсуви, пружини й частинки; лишаються лише прозорість і колір | Must |
| NFR-02-4 | Ліцензійна чистота: Fixel (OFL 1.1), Lucide (ISC), Material Symbols (Apache 2.0); SF Symbols не потрапляють у спільні ресурси | Must |
| NFR-02-5 | Растрові ресурси віджета: PNG маскота ≤ 60 KB кожен, не більше 2 масштабів у розширенні; текст ніколи не рендериться як растр | Must |
| NFR-02-6 | XAML без hex-кодів поза `Colors.xaml`: перевірка regex у CI | Must |
| NFR-02-7 | Українські рядки допускають +30 % довжини відносно англійських без обрізання; слово-герой ніколи не обрізається трьома крапками | Must |
| NFR-02-8 | Анімації тримають 60 fps на iPhone 11 / Pixel 6a-класі (припущення про базовий парк пристроїв) | Should |
| NFR-02-9 | Кожен PR з UI-змінами містить знімок екрана (Android локально, iOS з CI) у світлій і темній темах | Should |

### Константи політики дизайну

| Параметр конфігурації | Значення | Пояснення |
|---|---|---|
| `Design.MascotMaxPerScreen` | 1 | Один Реп’яшок на екран |
| `Design.ContentMaxWidthPt` | 640 | Максимальна ширина читабельного контенту на iPad |
| `Design.TouchTargetMinPt` | 44 (iOS) / 48 (Android) | Мінімальна ціль дотику |
| `Design.TypeMaxScale.Body` | 3.1 | Множник для body-ролей (AX5) |
| `Design.TypeMaxScale.Display` | 1.3 | Множник для display-ролей |
| `Widget.FreeBadgeShowAfterPoolExhausted` | true | Бейдж «5/5 сьогодні» лише після вичерпання безкоштовного пулу |
| `Widget.LockScreenHideTranslation` | false | Налаштування «ховати переклад на екрані блокування» (припущення) |
| `Notification.MascotAttachmentPerDay` | 1 | Максимум одне сповіщення з картинкою маскота на день |
| `Rewards.WidgetTheme.UnlockCost` | Луг 120, Ніч 120, Сонце 200 колючок | Припущення; остаточні ціни — у специфікації нагород |
| `Motion.Duration.Fast/Base/Slow/Celebrate` | 120/200/320/600 мс | Див. 3.8 |

## 3. Проєктування

### 3.1 Принципи дизайну

1. **Слова чіпляються.** Кожна поверхня має одне головне слово, яке читається за 300 мс: найбільший розмір, найвищий контраст, без конкурентів. Усе інше (транскрипція, переклад, приклад, лічильники) підпорядковане.
2. **Віджет — головна сцена.** Проєктуємо спочатку віджет і сповіщення, потім екран застосунку. Якщо рішення красиве в застосунку, але ламається у tinted-режимі або на екрані блокування, воно відхиляється.
3. **Грайливо, але спокійно.** Дух Duolingo (маскот, нагороди, святкування) у сучасних тонах: багато повітря, тональні поверхні, м’які радіуси, одна яскрава заливка на екран, жодного «ярмарку». Свято — подія, а не фон.
4. **Однією рукою.** Головні дії у нижній третині екрана; вгорі — читання. Мінімальна ціль 44 pt / 48 dp, між цілями ≥ 8 pt.
5. **Поважна гейміфікація.** Серія, колючки й Реп’яшок підбадьорюють і ніколи не соромлять і не лякають втратою (T17). Стан «сум» маскота не використовується для помилок користувача. Є режим «без гейміфікації».
6. **Українська типографіка з гордістю.** Fixel із повною кирилицею, правильні апостроф ’ і лапки «», літери ґ є ї у кожному тесті рендерингу, жодного фальшивого курсиву.
7. **Доступно за замовчуванням.** Контраст, Dynamic Type до AX5, VoiceOver/TalkBack-мітки з мовними тегами, Reduce Motion, індикація іконкою + текстом. Доступність — критерій приймання, а не бекліг.

### 3.2 Бренд

**Назва.** В інтерфейсі, сповіщеннях і маркетингу для України — «Реп’ях» (апостроф U+2019). У коді, bundle ID (`org.burdock.app`, Q27), англомовних сторінках сторів і файлах — `Burdock`. Маскот — «Реп’яшок»; валюта — «колючки» (з малої, як звичайне слово); серія — «Серія». Терміни збираються в `docs/glossary.md`.

**Логотип та іконка застосунку.** Силует реп’яшка: коло з 16 короткими заокругленими гачками по периметру й двома великими очима. Одна форма працює від 1024 px до 29 px: на малих розмірах гачки зливаються у «зубчастий» контур, очі лишаються читабельними. Фон іконки — градієнт `violet.600 → violet.800`; тіло `violet.400`; гачки `lime.400`; очі `neutral.0` з зіницями `neutral.950`. Концепт шарів для iOS 26 (припущення щодо інструментів; реалізація v1.1): шар 1 фон-градієнт, шар 2 тіло з гачками, шар 3 очі й відблиск; у tinted-варіанті шари 2-3 стають монохромними, у dark-варіанті фон `violet.900 → neutral.950`. Android: adaptive icon з тим самим переднім шаром і однотонним фоном `violet.600`, monochrome-шар для тематичних іконок Android 13+.

**Тон голосу.** Дружній, короткий, конкретний, без вигуків із трьома знаками оклику і без «ти втратиш». Звертання на «ти». Реп’яшок говорить від першої особи лише в онбордингу й порожніх станах; системні повідомлення — нейтральні. Емодзі в інтерфейсі не використовуються (замість них — маскот та іконки).

### 3.3 Кольорова система

Палітра «Реп’ях-цвіт» (Q8/Q19) задає п’ять хроматичних якорів і два фони. Якірні значення збережені без змін як конкретні ступені примітивних рамп; решту ступенів побудовано в OKLCH зі збереженням відтінку та згасанням насиченості до країв; ступені, що несуть текстові ролі (`violet.600`, `lime.600/700`, `neutral.400/500`), додатково підігнані під цільову яскравість, щоб гарантувати AA. Обчислення — скриптом за формулою WCAG 2.2 (відносна яскравість sRGB), результати нижче не округлені «на око».

#### 3.3.1 Примітивні рампи

| Рампа | 50 | 100 | 200 | 300 | 400 | 500 | 600 | 700 | 800 | 900 | 950 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| `violet` (фіолет, якір 500) | #F9F9FF | #ECECFF | #D5D4FF | #B8B4FE | #9A8EFE | **#7C5CFF** | #6D49EB | #5137AE | #3E2C87 | #2C2160 | #1E183E |
| `lime` (лайм, якір 400) | #F3FFE8 | #EAFFD5 | #D7FFAD | #C4FB84 | **#B4F461** | #92CB42 | #689C00 | #4F7700 | #3E5C0E | #283D07 | #162404 |
| `coral` (корал, якір 400) | #FFF8F8 | #FFE9E7 | #FFCBC7 | #FFA29E | **#FF6B6B** | #DC5556 | #B74244 | #913334 | #702727 | #501C1C | #351313 |
| `sunny` (сонячний, якір 400) | #FFFAEF | #FFF3DC | #FFE8BA | #FFD889 | **#FFC53D** | #D7A21A | #AE8207 | #866302 | #644900 | #453100 | #2A1D01 |
| `sky` (блакитний, якір 400) | #F3FCFF | #DFF6FF | #B7ECFF | #7CDDFF | **#4CC9F0** | #31A9CC | #1F89A8 | #126B83 | #0D5064 | #0A3745 | #08232C |

| `neutral` (теплувато-фіолетовий сірий) | 0 | 50 | 100 | 200 | 300 | 400 | 500 | 600 | 700 | 800 | 900 | 950 | 1000 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| | #FFFFFF | **#F7F6FC** | #EDEBF5 | #DBD9E3 | #BAB9C3 | #8C8B95 | #6A6873 | #585661 | #45434E | #33313B | #25242D | **#15131F** | #0C0B14 |

Нейтральна рампа має відтінок фіолету (H ≈ 293°) і дуже низьку насиченість, тому сірі «належать» бренду, а не виглядають чужими; `neutral.50` і `neutral.950` — це фони з журналу рішень без змін.

#### 3.3.2 Семантичні токени (light / dark)

| Токен | Light | Dark | Призначення |
|---|---|---|---|
| `color.brand.primary` | violet.600 #6D49EB | violet.400 #9A8EFE | Заливка інтерактивних елементів із текстом; ≥ 4.5:1 з onPrimary |
| `color.brand.onPrimary` | #FFFFFF | neutral.950 #15131F | Текст/іконки на primary |
| `color.brand.primaryVivid` | violet.500 #7C5CFF | violet.500 #7C5CFF | Фірмовий фіолет без змін: іконки, ілюстрації, кільце прогресу, дисплейний текст ≥ 24 pt |
| `color.brand.primaryContainer` | violet.100 #ECECFF | violet.900 #2C2160 | М’яка підкладка вибраного |
| `color.brand.onPrimaryContainer` | violet.800 #3E2C87 | violet.200 #D5D4FF | Текст на primaryContainer |
| `color.brand.accent` | lime.400 #B4F461 | lime.400 #B4F461 | Лаймова заливка (бейджі, «нове», підсвітка) |
| `color.brand.onAccent` | lime.950 #162404 | lime.950 #162404 | Темне чорнило на лаймі |
| `color.brand.accentText` | lime.700 #4F7700 | lime.300 #C4FB84 | Лайм як текст/іконка на поверхні |
| `color.surface.default` | neutral.50 #F7F6FC | neutral.950 #15131F | Фон екрана |
| `color.surface.elevated` | neutral.0 #FFFFFF | neutral.900 #25242D | Картки, аркуші |
| `color.surface.sunken` | neutral.100 #EDEBF5 | neutral.1000 #0C0B14 | Поля вводу, групи, скелетони |
| `color.surface.overlay` | #0C0B148F | #000000A3 | Затемнення під BottomSheet/Dialog |
| `color.surface.brandTint` | violet.50 #F9F9FF | violet.950 #1E183E | Hero-зони, онбординг |
| `color.text.primary` | neutral.950 #15131F | neutral.50 #F7F6FC | Основний текст |
| `color.text.secondary` | neutral.600 #585661 | neutral.300 #BAB9C3 | Підписи |
| `color.text.tertiary` | neutral.500 #6A6873 | neutral.400 #8C8B95 | Підказки, метадані |
| `color.text.inverse` | neutral.50 #F7F6FC | neutral.950 #15131F | Текст на інверсних тостах |
| `color.text.link` | violet.700 #5137AE | violet.300 #B8B4FE | Посилання, текстові кнопки |
| `color.text.disabled` | neutral.400 #8C8B95 | neutral.600 #585661 | Неактивний текст (звільнений від контрасту) |
| `color.border.default` | neutral.200 #DBD9E3 | neutral.800 #33313B | Роздільники, контур карток |
| `color.border.strong` | neutral.400 #8C8B95 | neutral.400 #8C8B95 | Контур полів вводу (≥ 3:1) |
| `color.border.focus` | violet.600 #6D49EB | violet.300 #B8B4FE | Кільце фокуса 3 pt |
| `color.feedback.success` / `Container` / `onContainer` | lime.700 #4F7700 / lime.100 #EAFFD5 / lime.800 #3E5C0E | lime.300 #C4FB84 / lime.900 #283D07 / lime.200 #D7FFAD | Успіх |
| `color.feedback.error` / `Container` / `onContainer` | coral.600 #B74244 / coral.100 #FFE9E7 / coral.800 #702727 | coral.300 #FFA29E / coral.900 #501C1C / coral.200 #FFCBC7 | Помилка |
| `color.feedback.warning` / `Container` / `onContainer` | sunny.700 #866302 / sunny.100 #FFF3DC / sunny.800 #644900 | sunny.300 #FFD889 / sunny.900 #453100 / sunny.200 #FFE8BA | Попередження |
| `color.feedback.info` / `Container` / `onContainer` | sky.700 #126B83 / sky.100 #DFF6FF / sky.800 #0D5064 | sky.300 #7CDDFF / sky.900 #0A3745 / sky.200 #B7ECFF | Інформація |
| `color.reward.gold` / `goldText` / `goldContainer` | sunny.400 #FFC53D / sunny.800 #644900 / sunny.100 #FFF3DC | sunny.400 #FFC53D / sunny.300 #FFD889 / sunny.900 #453100 | Нагороди |
| `color.reward.currency` / `currencyText` / `currencyContainer` | lime.600 #689C00 / lime.800 #3E5C0E / lime.100 #EAFFD5 | lime.400 #B4F461 / lime.300 #C4FB84 / lime.900 #283D07 | Колючки |
| `color.reward.heart` | coral.400 #FF6B6B | coral.400 #FF6B6B | Декоративне серце «улюблене» (завжди з підписом) |
| `color.streak.active` / `text` / `container` | sunny.500 #D7A21A / sunny.800 #644900 / sunny.100 #FFF3DC | sunny.400 #FFC53D / sunny.300 #FFD889 / sunny.900 #453100 | Серія |
| `color.streak.frozen` / `frozenText` / `inactive` | sky.600 #1F89A8 / sky.800 #0D5064 / neutral.300 #BAB9C3 | sky.400 #4CC9F0 / sky.300 #7CDDFF / neutral.700 #45434E | Заморозка, неактивний день |
| `color.quiz.correct` / `correctContainer` / `correctBorder` | lime.700 #4F7700 / lime.100 #EAFFD5 / lime.600 #689C00 | lime.300 #C4FB84 / lime.900 #283D07 / lime.500 #92CB42 | Правильна відповідь |
| `color.quiz.incorrect` / `incorrectContainer` / `incorrectBorder` | coral.600 #B74244 / coral.100 #FFE9E7 / coral.500 #DC5556 | coral.300 #FFA29E / coral.900 #501C1C / coral.500 #DC5556 | Неправильна відповідь |
| `color.quiz.selected` / `selectedContainer` / `selectedBorder` | violet.700 #5137AE / violet.100 #ECECFF / violet.600 #6D49EB | violet.200 #D5D4FF / violet.900 #2C2160 / violet.400 #9A8EFE | Обраний варіант до перевірки |
| `color.widget.background` / `backgroundEnd` | violet.600 #6D49EB / violet.800 #3E2C87 | violet.900 #2C2160 / neutral.950 #15131F | Градієнт фону (контраст рахується зі світлішим краєм) |
| `color.widget.word` / `ipa` / `translation` / `example` | #FFFFFF / violet.100 #ECECFF / violet.100 / violet.100 | neutral.50 #F7F6FC / violet.200 #D5D4FF / violet.200 / violet.300 #B8B4FE | Текст віджета |
| `color.widget.accent` / `onAccent` | lime.400 #B4F461 / lime.950 #162404 | lime.400 / lime.950 | Індикатор рівня, підкреслення |
| `color.widget.chipBackground` / `chipText` | violet.800 #3E2C87 / violet.100 #ECECFF | violet.800 / violet.100 | Чіп рівня та категорії |
| `color.widget.button` / `onButton` | #FFFFFF / violet.700 #5137AE | violet.300 #B8B4FE / violet.950 #1E183E | Кнопка «Наступне» |
| `color.widget.divider` | violet.500 #7C5CFF | violet.800 #3E2C87 | Роздільник у systemLarge |
| `color.widget.badgeFree` / `onBadgeFree` | violet.800 #3E2C87 / violet.100 #ECECFF | violet.800 / violet.100 | Бейдж «5/5 сьогодні» |

Чому `brand.primary` у світлій темі — це `violet.600`, а не `#7C5CFF`: білий на `#7C5CFF` дає **4.35:1**, темне чорнило на ньому — **4.22:1**; обидва провалюють 4.5:1 для звичайного тексту. Фірмовий hex збережено як `violet.500` = `color.brand.primaryVivid` для всього, що не є дрібним текстом; кнопки та посилання беруть сусідній ступінь того ж відтінку. Аналогічно `#FF6B6B` (корал) дає лише **2.78:1** з білим і не може нести текст помилки — для тексту використовується `coral.600`.

#### 3.3.3 Таблиця контрасту (обчислено, WCAG 2.2)

Поріг: текст 4.5:1, великий текст (≥ 24 pt або ≥ 19 pt Bold) 3:1, UI-межі 3:1. Позначка «7:1» — досягнуто рівень AAA. Разом 122 пари, **122 pass, 0 fail, 75 із них ≥ 7:1**.

| Тема | Передній план | Фон | Hex | Тип | Коеф. | AA | 7:1 |
|---|---|---|---|---|---|---|---|
| light | `text.primary` | `surface.default` | #15131F / #F7F6FC | текст | **17.07** | pass | так |
| light | `text.primary` | `surface.elevated` | #15131F / #FFFFFF | текст | **18.34** | pass | так |
| light | `text.primary` | `surface.sunken` | #15131F / #EDEBF5 | текст | **15.55** | pass | так |
| light | `text.secondary` | `surface.default` | #585661 / #F7F6FC | текст | **6.70** | pass | |
| light | `text.secondary` | `surface.elevated` | #585661 / #FFFFFF | текст | **7.20** | pass | так |
| light | `text.secondary` | `surface.sunken` | #585661 / #EDEBF5 | текст | **6.10** | pass | |
| light | `text.tertiary` | `surface.default` | #6A6873 / #F7F6FC | текст | **5.09** | pass | |
| light | `text.tertiary` | `surface.elevated` | #6A6873 / #FFFFFF | текст | **5.47** | pass | |
| light | `text.tertiary` | `surface.sunken` | #6A6873 / #EDEBF5 | текст | **4.64** | pass | |
| light | `text.link` | `surface.default` | #5137AE / #F7F6FC | текст | **7.79** | pass | так |
| light | `text.link` | `surface.elevated` | #5137AE / #FFFFFF | текст | **8.37** | pass | так |
| light | `text.link` | `surface.sunken` | #5137AE / #EDEBF5 | текст | **7.10** | pass | так |
| light | `text.inverse` | `text.primary` (тост) | #F7F6FC / #15131F | текст | **17.07** | pass | так |
| light | `brand.onPrimary` | `brand.primary` | #FFFFFF / #6D49EB | текст | **5.52** | pass | |
| light | `brand.primary` | `surface.default` | #6D49EB / #F7F6FC | текст | **5.14** | pass | |
| light | `brand.primary` | `surface.elevated` | #6D49EB / #FFFFFF | текст | **5.52** | pass | |
| light | `brand.primaryVivid` | `surface.default` | #7C5CFF / #F7F6FC | великий | **4.04** | pass | |
| light | `brand.primaryVivid` | `surface.elevated` | #7C5CFF / #FFFFFF | великий | **4.35** | pass | |
| light | `brand.onPrimaryContainer` | `brand.primaryContainer` | #3E2C87 / #ECECFF | текст | **9.40** | pass | так |
| light | `brand.onAccent` | `brand.accent` | #162404 / #B4F461 | текст | **12.46** | pass | так |
| light | `brand.accentText` | `surface.default` | #4F7700 / #F7F6FC | текст | **4.92** | pass | |
| light | `brand.accentText` | `surface.elevated` | #4F7700 / #FFFFFF | текст | **5.29** | pass | |
| light | `feedback.success` | `surface.default` | #4F7700 / #F7F6FC | текст | **4.92** | pass | |
| light | `feedback.success` | `surface.elevated` | #4F7700 / #FFFFFF | текст | **5.29** | pass | |
| light | `feedback.onSuccessContainer` | `feedback.successContainer` | #3E5C0E / #EAFFD5 | текст | **7.21** | pass | так |
| light | `feedback.error` | `surface.default` | #B74244 / #F7F6FC | текст | **5.04** | pass | |
| light | `feedback.error` | `surface.elevated` | #B74244 / #FFFFFF | текст | **5.42** | pass | |
| light | `feedback.onErrorContainer` | `feedback.errorContainer` | #702727 / #FFE9E7 | текст | **8.99** | pass | так |
| light | `feedback.warning` | `surface.default` | #866302 / #F7F6FC | текст | **5.14** | pass | |
| light | `feedback.warning` | `surface.elevated` | #866302 / #FFFFFF | текст | **5.53** | pass | |
| light | `feedback.onWarningContainer` | `feedback.warningContainer` | #644900 / #FFF3DC | текст | **7.66** | pass | так |
| light | `feedback.info` | `surface.default` | #126B83 / #F7F6FC | текст | **5.65** | pass | |
| light | `feedback.info` | `surface.elevated` | #126B83 / #FFFFFF | текст | **6.08** | pass | |
| light | `feedback.onInfoContainer` | `feedback.infoContainer` | #0D5064 / #DFF6FF | текст | **7.99** | pass | так |
| light | `reward.goldText` | `reward.goldContainer` | #644900 / #FFF3DC | текст | **7.66** | pass | так |
| light | `reward.goldText` | `surface.default` | #644900 / #F7F6FC | текст | **7.83** | pass | так |
| light | `reward.currencyText` | `reward.currencyContainer` | #3E5C0E / #EAFFD5 | текст | **7.21** | pass | так |
| light | `reward.currencyText` | `surface.default` | #3E5C0E / #F7F6FC | текст | **7.13** | pass | так |
| light | `streak.text` | `streak.container` | #644900 / #FFF3DC | текст | **7.66** | pass | так |
| light | `streak.text` | `surface.default` | #644900 / #F7F6FC | текст | **7.83** | pass | так |
| light | `streak.frozenText` | `surface.default` | #0D5064 / #F7F6FC | текст | **8.32** | pass | так |
| light | `quiz.correct` | `quiz.correctContainer` | #4F7700 / #EAFFD5 | текст | **4.98** | pass | |
| light | `quiz.incorrect` | `quiz.incorrectContainer` | #B74244 / #FFE9E7 | текст | **4.66** | pass | |
| light | `quiz.selected` | `quiz.selectedContainer` | #5137AE / #ECECFF | текст | **7.18** | pass | так |
| light | `quiz.correctBorder` | `surface.elevated` | #689C00 / #FFFFFF | UI | **3.31** | pass | |
| light | `quiz.incorrectBorder` | `surface.elevated` | #DC5556 / #FFFFFF | UI | **3.83** | pass | |
| light | `quiz.selectedBorder` | `surface.elevated` | #6D49EB / #FFFFFF | UI | **5.52** | pass | |
| light | `border.strong` | `surface.default` | #8C8B95 / #F7F6FC | UI | **3.13** | pass | |
| light | `border.strong` | `surface.elevated` | #8C8B95 / #FFFFFF | UI | **3.36** | pass | |
| light | `border.focus` | `surface.default` | #6D49EB / #F7F6FC | UI | **5.14** | pass | |
| light | `border.focus` | `surface.elevated` | #6D49EB / #FFFFFF | UI | **5.52** | pass | |
| light | `widget.word` | `widget.background` | #FFFFFF / #6D49EB | великий | **5.52** | pass | |
| light | `widget.translation` | `widget.background` | #ECECFF / #6D49EB | текст | **4.74** | pass | |
| light | `widget.ipa` | `widget.background` | #ECECFF / #6D49EB | текст | **4.74** | pass | |
| light | `widget.example` | `widget.background` | #ECECFF / #6D49EB | текст | **4.74** | pass | |
| light | `widget.onAccent` | `widget.accent` | #162404 / #B4F461 | текст | **12.46** | pass | так |
| light | `widget.chipText` | `widget.chipBackground` | #ECECFF / #3E2C87 | текст | **9.40** | pass | так |
| light | `widget.onButton` | `widget.button` | #5137AE / #FFFFFF | текст | **8.37** | pass | так |
| light | `widget.onBadgeFree` | `widget.badgeFree` | #ECECFF / #3E2C87 | текст | **9.40** | pass | так |
| light | `widget.accent` | `widget.background` | #B4F461 / #6D49EB | UI | **4.22** | pass | |
| light | `widget.button` | `widget.background` | #FFFFFF / #6D49EB | UI | **5.52** | pass | |
| dark | `text.primary` | `surface.default` | #F7F6FC / #15131F | текст | **17.07** | pass | так |
| dark | `text.primary` | `surface.elevated` | #F7F6FC / #25242D | текст | **14.28** | pass | так |
| dark | `text.primary` | `surface.sunken` | #F7F6FC / #0C0B14 | текст | **18.20** | pass | так |
| dark | `text.secondary` | `surface.default` | #BAB9C3 / #15131F | текст | **9.45** | pass | так |
| dark | `text.secondary` | `surface.elevated` | #BAB9C3 / #25242D | текст | **7.90** | pass | так |
| dark | `text.secondary` | `surface.sunken` | #BAB9C3 / #0C0B14 | текст | **10.07** | pass | так |
| dark | `text.tertiary` | `surface.default` | #8C8B95 / #15131F | текст | **5.45** | pass | |
| dark | `text.tertiary` | `surface.elevated` | #8C8B95 / #25242D | текст | **4.56** | pass | |
| dark | `text.tertiary` | `surface.sunken` | #8C8B95 / #0C0B14 | текст | **5.81** | pass | |
| dark | `text.link` | `surface.default` | #B8B4FE / #15131F | текст | **9.61** | pass | так |
| dark | `text.link` | `surface.elevated` | #B8B4FE / #25242D | текст | **8.03** | pass | так |
| dark | `text.link` | `surface.sunken` | #B8B4FE / #0C0B14 | текст | **10.24** | pass | так |
| dark | `text.inverse` | `text.primary` (тост) | #15131F / #F7F6FC | текст | **17.07** | pass | так |
| dark | `brand.onPrimary` | `brand.primary` | #15131F / #9A8EFE | текст | **6.70** | pass | |
| dark | `brand.primary` | `surface.default` | #9A8EFE / #15131F | текст | **6.70** | pass | |
| dark | `brand.primary` | `surface.elevated` | #9A8EFE / #25242D | текст | **5.61** | pass | |
| dark | `brand.primaryVivid` | `surface.default` | #7C5CFF / #15131F | великий | **4.22** | pass | |
| dark | `brand.primaryVivid` | `surface.elevated` | #7C5CFF / #25242D | великий | **3.53** | pass | |
| dark | `brand.onPrimaryContainer` | `brand.primaryContainer` | #D5D4FF / #2C2160 | текст | **9.84** | pass | так |
| dark | `brand.onAccent` | `brand.accent` | #162404 / #B4F461 | текст | **12.46** | pass | так |
| dark | `brand.accentText` | `surface.default` | #C4FB84 / #15131F | текст | **15.27** | pass | так |
| dark | `brand.accentText` | `surface.elevated` | #C4FB84 / #25242D | текст | **12.77** | pass | так |
| dark | `feedback.success` | `surface.default` | #C4FB84 / #15131F | текст | **15.27** | pass | так |
| dark | `feedback.success` | `surface.elevated` | #C4FB84 / #25242D | текст | **12.77** | pass | так |
| dark | `feedback.onSuccessContainer` | `feedback.successContainer` | #D7FFAD / #283D07 | текст | **10.68** | pass | так |
| dark | `feedback.error` | `surface.default` | #FFA29E / #15131F | текст | **9.53** | pass | так |
| dark | `feedback.error` | `surface.elevated` | #FFA29E / #25242D | текст | **7.97** | pass | так |
| dark | `feedback.onErrorContainer` | `feedback.errorContainer` | #FFCBC7 / #501C1C | текст | **9.59** | pass | так |
| dark | `feedback.warning` | `surface.default` | #FFD889 / #15131F | текст | **13.48** | pass | так |
| dark | `feedback.warning` | `surface.elevated` | #FFD889 / #25242D | текст | **11.28** | pass | так |
| dark | `feedback.onWarningContainer` | `feedback.warningContainer` | #FFE8BA / #453100 | текст | **10.34** | pass | так |
| dark | `feedback.info` | `surface.default` | #7CDDFF / #15131F | текст | **11.92** | pass | так |
| dark | `feedback.info` | `surface.elevated` | #7CDDFF / #25242D | текст | **9.97** | pass | так |
| dark | `feedback.onInfoContainer` | `feedback.infoContainer` | #B7ECFF / #0A3745 | текст | **10.00** | pass | так |
| dark | `reward.goldText` | `reward.goldContainer` | #FFD889 / #453100 | текст | **9.12** | pass | так |
| dark | `reward.goldText` | `surface.default` | #FFD889 / #15131F | текст | **13.48** | pass | так |
| dark | `reward.currencyText` | `reward.currencyContainer` | #C4FB84 / #283D07 | текст | **9.93** | pass | так |
| dark | `reward.currencyText` | `surface.default` | #C4FB84 / #15131F | текст | **15.27** | pass | так |
| dark | `streak.text` | `streak.container` | #FFD889 / #453100 | текст | **9.12** | pass | так |
| dark | `streak.text` | `surface.default` | #FFD889 / #15131F | текст | **13.48** | pass | так |
| dark | `streak.frozenText` | `surface.default` | #7CDDFF / #15131F | текст | **11.92** | pass | так |
| dark | `quiz.correct` | `quiz.correctContainer` | #C4FB84 / #283D07 | текст | **9.93** | pass | так |
| dark | `quiz.incorrect` | `quiz.incorrectContainer` | #FFA29E / #501C1C | текст | **7.16** | pass | так |
| dark | `quiz.selected` | `quiz.selectedContainer` | #D5D4FF / #2C2160 | текст | **9.84** | pass | так |
| dark | `quiz.correctBorder` | `surface.elevated` | #92CB42 / #25242D | UI | **7.92** | pass | так |
| dark | `quiz.incorrectBorder` | `surface.elevated` | #DC5556 / #25242D | UI | **4.00** | pass | |
| dark | `quiz.selectedBorder` | `surface.elevated` | #9A8EFE / #25242D | UI | **5.61** | pass | |
| dark | `border.strong` | `surface.default` | #8C8B95 / #15131F | UI | **5.45** | pass | |
| dark | `border.strong` | `surface.elevated` | #8C8B95 / #25242D | UI | **4.56** | pass | |
| dark | `border.focus` | `surface.default` | #B8B4FE / #15131F | UI | **9.61** | pass | так |
| dark | `border.focus` | `surface.elevated` | #B8B4FE / #25242D | UI | **8.03** | pass | так |
| dark | `widget.word` | `widget.background` | #F7F6FC / #2C2160 | великий | **13.08** | pass | так |
| dark | `widget.translation` | `widget.background` | #D5D4FF / #2C2160 | текст | **9.84** | pass | так |
| dark | `widget.ipa` | `widget.background` | #D5D4FF / #2C2160 | текст | **9.84** | pass | так |
| dark | `widget.example` | `widget.background` | #B8B4FE / #2C2160 | текст | **7.36** | pass | так |
| dark | `widget.onAccent` | `widget.accent` | #162404 / #B4F461 | текст | **12.46** | pass | так |
| dark | `widget.chipText` | `widget.chipBackground` | #ECECFF / #3E2C87 | текст | **9.40** | pass | так |
| dark | `widget.onButton` | `widget.button` | #1E183E / #B8B4FE | текст | **8.76** | pass | так |
| dark | `widget.onBadgeFree` | `widget.badgeFree` | #ECECFF / #3E2C87 | текст | **9.40** | pass | так |
| dark | `widget.accent` | `widget.background` | #B4F461 / #2C2160 | UI | **10.75** | pass | так |
| dark | `widget.button` | `widget.background` | #B8B4FE / #2C2160 | UI | **7.36** | pass | так |

Довідкові (не текстові) пари, які пояснюють правила: `lime.400` на `surface.default` — 1.22 (тому лайм у світлій темі завжди в контейнері з темним чорнилом або з контуром `lime.700`); `neutral.950` на `sunny.400` — 11.62, на `sky.400` — 9.54, на `lime.400` — 14.02 (яскраві якорі чудово працюють як заливки бейджів із темним текстом); `sunny.400` і `sky.400` як текст на білому — 1.58 і 1.92 (ніколи).

Спостереження щодо 7:1: у темній темі 7:1 досягається майже всюди (світлі відтінки на тональних поверхнях), у світлій — для основного тексту, посилань і всіх «onContainer» пар. Пари `text.tertiary` та `feedback.*` на світлих поверхнях свідомо лишаються в діапазоні 4.6-6.1: подальше затемнення робить палітру «брудною». Це відповідає рекомендації HIG цілитись у 7:1 саме в темному режимі.

#### 3.3.4 Правила використання лайму

Лайм — акцент енергії, не колір тексту. Дозволено: заливка бейджа/чіпа з чорнилом `brand.onAccent`; підкреслення або точка-індикатор шириною ≥ 3 pt поруч із текстом; гачки маскота; заливка кільця прогресу на темному фоні; текст `brand.accentText` (`lime.700` у світлій, `lime.300` у темній). Заборонено: `lime.400` як текст на будь-якій світлій поверхні; лайм як єдиний носій сенсу («зелений = правильно» без іконки і слова).

#### 3.3.5 Теми віджета як нагорода

Тема — набір перекриттів лише для `color.widget.*` (обидва режими). Тема «Цвіт» безкоштовна й типова; решта відкриваються за колючки (Q34). Мінімальні коефіцієнти нижче обчислені для світлішого краю градієнта (гірший випадок) і для кінця градієнта; всі 88 пар тем проходять AA.

| Тема | Ключ | Light: фон → кінець / слово / переклад | Dark: фон → кінець / слово / переклад | Акцент (light / dark) | Мін. контраст (light / dark) |
|---|---|---|---|---|---|
| Цвіт | `tsvit` | #6D49EB → #3E2C87 / #FFFFFF / #ECECFF | #2C2160 → #15131F / #F7F6FC / #D5D4FF | lime.400 / lime.400 | 4.74 (переклад) / 7.36 |
| Луг | `luh` | #EAFFD5 → #D7FFAD / #162404 / #3E5C0E | #162404 → #15131F / #EAFFD5 / #C4FB84 | violet.600 / lime.400 | 5.20 (акцент як UI) / 12.46 |
| Ніч | `nich` | #15131F → #1E183E / #F7F6FC / #BAB9C3 | #0C0B14 → #1E183E / #F7F6FC / #BAB9C3 | sky.300 / sky.300 | 5.45 (приклад) / 5.81 |
| Сонце | `sontse` | #FFE8BA → #FFC53D / #2A1D01 / #644900 | #2A1D01 → #15131F / #FFF3DC / #FFD889 | coral.600 / sunny.400 | 4.52 (акцент як UI) / 10.34 |

Мапа на платформи: у застосунку (екран «Вигляд віджета» з живим прев’ю) кожна тема — окремий `ResourceDictionary` `Resources/Styles/WidgetThemes/{Tsvit,Luh,Nich,Sontse}.xaml` з ключами `WidgetBackground`, `WidgetBackgroundEnd`, `WidgetWord`, …; прев’ю читає їх через `DynamicResource`, а перемикання теми — заміна словника у `Application.Current.Resources.MergedDictionaries`. У WidgetKit тема — `enum WidgetTheme: String` у `DesignTokens.swift` з `Color(light:dark:)`, обраний ключ пишеться в App Group. В Android — префікс `widget_theme_{key}_*` у `values/colors.xml` та `values-night/colors.xml`; провайдер підставляє їх у RemoteViews, якщо користувач вимкнув Material You для віджета (T19: за замовчуванням динамічні кольори мають пріоритет).

### 3.4 Типографіка

**Гарнітури.** Fixel Text (набірні розміри ≤ 20 pt) і Fixel Display (заголовки й слово-герой) від MacPaw, OFL 1.1, повна українська кирилиця, 9 накреслень; у застосунок вбудовуються статичні TTF: Text Regular/Medium/SemiBold/Bold і Display SemiBold/Bold (6 файлів, aliases `FixelText`, `FixelTextMedium`, `FixelTextSemiBold`, `FixelTextBold`, `FixelDisplaySemiBold`, `FixelDisplayBold`). Перевірено на файлі `FixelText-Regular.ttf` (2026-09-27): 519 кодових точок, є ґ Ґ є ї ’ « » — … ₴ №, є OpenType-функції `tnum`, `pnum`, `lnum`, `onum`, `case`, `locl`, `ss01`. Немає: блоку IPA Extensions (з 23 базових символів IPA присутні лише ð, ŋ, æ), U+02BC (модифікаторний апостроф) та U+2713 (галочка).

**Наслідки.** (1) IPA завжди рендериться системним шрифтом (San Francisco / Roboto) через роль `type.word.ipa`; змішувати Fixel і системний шрифт в одному рядку заборонено — транскрипція завжди окремим рядком. (2) Апостроф у UI-рядках і словниковому контенті нормалізується до U+2019; U+02BC і U+0027 замінюються на етапі збірки ресурсів (`.resx`) та контент-пайплайну. (3) Галочки та інші символи — іконками Lucide, не гліфами. (4) Табличні цифри для статистики й таймерів вмикаються через `tnum` платформовим обробником (`UIFontDescriptor` feature settings на iOS, `TextView.FontFeatureSettings = "tnum"` на Android) — реалізується як прикріплена властивість `Typography.TabularNumbers="True"`.

**Запасні гарнітури.** Inter (OFL, Cyrillic) для веб-прев’ю та документації, коли Fixel недоступний; системний шрифт (SF Pro / Roboto) для IPA, віджетів Android (RemoteViews не підтримують власні шрифти) і як аварійний відкат, якщо ресурс шрифту не завантажився. Golos Text та інші шрифти російських словолитень виключені політикою (див. звіт, розділ 4).

#### Шкала

| Роль | Гарнітура / вага | Розмір / інтерліньяж (pt) | Трекінг | Макс. масштаб | Використання |
|---|---|---|---|---|---|
| `type.display.lg` | Display 700 | 40 / 46 | −0.02 em | 1.3× | Число серії на екрані свята, hero онбордингу |
| `type.display.md` | Display 700 | 32 / 38 | −0.015 em | 1.3× | Заголовок PaywallCard, підсумок сесії |
| `type.headline.lg` | Display 600 | 28 / 34 | −0.01 em | 1.5× | Заголовки екранів |
| `type.headline.md` | Display 600 | 24 / 30 | −0.005 em | 1.5× | Заголовки секцій, BottomSheet |
| `type.headline.sm` | Text 600 | 20 / 26 | 0 | 1.6× | Заголовок Dialog, TopBar large |
| `type.title.lg` | Text 600 | 18 / 24 | 0 | 1.8× | Назва картки, TaskRow |
| `type.title.md` | Text 600 | 16 / 22 | 0 | 1.8× | Назва SettingRow, вкладки |
| `type.title.sm` | Text 600 | 14 / 20 | +0.005 em | 2.0× | Підзаголовки списків |
| `type.body.lg` | Text 400 | 17 / 24 | 0 | 3.1× (AX5) | Основний текст, переклад у WordCard |
| `type.body.md` | Text 400 | 15 / 22 | 0 | 3.1× | Приклади речень, описи |
| `type.body.sm` | Text 400 | 13 / 18 | +0.005 em | 3.1× | Пояснення в налаштуваннях |
| `type.label.lg` | Text 600 | 16 / 20 | +0.005 em | 2.0× | Кнопки |
| `type.label.md` | Text 500 | 14 / 18 | +0.01 em | 2.0× | Чіпи, вкладки таб-бару, CurrencyPill |
| `type.label.sm` | Text 500 | 12 / 16 | +0.02 em | 2.0× | Бейджі, підписи календаря |
| `type.word.hero` | Display 700 | 34 / 40 | −0.01 em | 1.6× | Слово у WordCard і quiz |
| `type.word.ipa` | Системний 400 | 15 / 20 | 0 | 2.0× | Транскрипція |
| `type.numeric.stat` | Display 700 + `tnum` | 28 / 32 | 0 | 1.5× | Статистика, лічильники, таймер сесії |

**Dynamic Type / FontAutoScaling.** `FontAutoScalingEnabled="True"` для всіх ролей; кожна роль має «стелю» (колонка «Макс. масштаб»), яку реалізує прикріплена властивість `Typography.Role="BodyLg"` — вона читає системний множник, обмежує його стелею ролі і виставляє `FontSize`/`LineHeight`. Body-ролі ростуть до AX5 повністю; display і слово-герой — обмежено, щоб слово лишалось на одному-двох рядках; при множнику ≥ 1.5 горизонтальні пари (слово + дії) перетікають у стовпчик. Мінімальний розмір тексту в застосунку 12 pt, у віджетах — 11 pt (звіт, розділ 4).

**Українські правила.** Апостроф ’ (U+2019); лапки «» (внутрішні „“); тире — з нерозривним пробілом перед ним у коротких рядках; дефіс без пробілів; неперенесення однолітерних прийменників «в», «у», «з», «і» (нерозривний пробіл додається у `.resx`); тестовий рядок для кожного шрифтового ресурсу: «Ґудзик є їжаку — щастя’ 0123456789 ₴»; жодного синтетичного курсиву (Fixel не має Italic, наголос — вага або колір); множина за українськими правилами (1 день / 2 дні / 5 днів) через плюральні ресурси.

**Англійське слово.** Слово-герой завжди у нижньому регістрі так, як у лемі (власні назви — як є), без автокапіталізації; частина мови українською скорочено («ім.», «дієсл.», «прикм.») з повною формою в accessibility-мітці; артикль «to» для дієслів не додається (лема чиста).

### 3.5 Простір, сітка, цілі дотику

| Токен | pt | Типове використання |
|---|---|---|
| `space.1` | 4 | Внутрішній зазор іконка-текст у бейджах |
| `space.2` | 8 | Між елементами в рядку, між чіпами |
| `space.3` | 12 | Внутрішні відступи чіпів, малих карток |
| `space.4` | 16 | Поля екрана на iPhone, внутрішні відступи карток |
| `space.5` | 20 | Внутрішні відступи WordCard |
| `space.6` | 24 | Між секціями, поля екрана на iPad |
| `space.7` | 32 | Верх/низ великих блоків |
| `space.8` | 40 | Відступ під TopBar до першого hero |
| `space.9` | 48 | Порожні стани навколо маскота |
| `space.10` | 56 | Висота TabBar без safe area |
| `space.11` | 64 | Відступ до кнопки дії внизу довгих екранів |
| `space.12` | 80 | Hero-зона онбордингу |

**Сітка.** iPhone (compact width): поля 16 pt, 4 колонки, міжколонник 12 pt; списки на всю ширину, картки з полями. iPad (regular width): поля 24 pt, 8 колонок, міжколонник 16 pt; контент читання (WordCard, quiz, налаштування) центрується з максимальною шириною 640 pt (`Design.ContentMaxWidthPt`); двопанельний режим у ландшафті: ліворуч список (мінімум 320 pt), праворуч деталь; вертикальна орієнтація iPad — одна колонка з тим самим максимумом. Реалізація: `OnIdiom`/`OnSizeChanged`-тригери на кореневому `Grid`, а не окремі сторінки для iPad. Slide Over / Split View на iPad повертають compact-ширину — макет має спрацьовувати за шириною вікна, а не за типом пристрою.

**Safe areas.** Контент поважає всі safe areas; TabBar і нижні кнопки дії додають `space.4` над home indicator; на екранах із Dynamic Island TopBar не розміщує елементи вище 8 pt від верхньої safe area.

**Цілі дотику.** 44×44 pt (iOS) / 48×48 dp (Android) — мінімум для всього, що приймає дотик, включно з іконками в TopBar і кнопкою «Наступне» у віджеті (візуально 28 pt, зона дотику 44 pt). Між сусідніми цілями ≥ 8 pt. Це вище за юридичний мінімум WCAG 2.5.8 (24 px).

### 3.6 Форма

| Токен | pt | Використання |
|---|---|---|
| `radius.sm` | 8 | Чіпи, бейджі, поля вводу, кнопки label.md |
| `radius.md` | 12 | Кнопки, QuizOption, TaskRow, SettingRow-групи |
| `radius.lg` | 16 | WordCard, RewardCard, PaywallCard, Dialog |
| `radius.xl` | 24 | BottomSheet (верхні кути), hero-блоки |
| `radius.full` | 999 | CurrencyPill, StreakBadge, круглі кнопки, аватар |

**Стиль картки.** `surface.elevated` + `radius.lg` + elevation 1 + внутрішні відступи `space.5`; у темній темі додається контур 1 pt `border.default`. Вкладені картки не мають тіні (лише `surface.sunken`).

**Мотив «прилиплий реп’яшок».** Один кут головної картки екрана (за замовчуванням верхній правий) може мати «гачок»: маскот розміром `mascot.sm` (56 pt), що на 12 pt виходить за межу картки, або, у компактному варіанті, 3 короткі зубчики-гачки у контурі кута. Правила: лише на одній картці екрана; ніколи на QuizOption, полях вводу, PaywallCard; ніколи в accessory-віджетах; зубчики — суто декоративні (не мають accessibility-ролі). Дуже помірно: мета — впізнаваність, а не орнамент.

### 3.7 Висота (elevation)

| Рівень | Light | Dark | Де |
|---|---|---|---|
| 0 | без тіні | без тіні | Фон, вбудовані елементи |
| 1 | `0 1 2 #15131F0F` + `0 2 8 #15131F0F` | `0 1 2 #00000066` + поверхня `surface.elevated` + контур `border.default` | Картки, TaskRow |
| 2 | `0 4 16 #15131F1A` | `0 4 16 #00000080` + `surface.elevated` | BottomSheet, плаваюча кнопка, Toast |
| 3 | `0 12 32 #15131F29` | `0 12 32 #00000099` + `surface.elevated` | Dialog, підняті картки під час перетягування (MatchPair) |

Тінь має колір чорнила з альфою, не сірого; у темній темі тінь майже невидима, тому підйом передають світліша поверхня і контур. Реалізація в MAUI: `Border` із `Shadow` (Brush = токен), не `Frame`.

### 3.8 Рух і хептика

| Токен | Значення | Використання |
|---|---|---|
| `motion.duration.fast` | 120 мс | Натискання, зміна стану чіпа, підсвітка |
| `motion.duration.base` | 200 мс | Поява/зникнення, перемикання вкладок, розкриття перекладу |
| `motion.duration.slow` | 320 мс | BottomSheet, переходи екранів, «прилипання» |
| `motion.duration.celebrate` | 600 мс | Свято серії, випадіння нагороди |
| `motion.easing.standard` | cubic-bezier(0.2, 0, 0, 1) | Типова |
| `motion.easing.decelerate` | (0, 0, 0, 1) | Поява елементів |
| `motion.easing.accelerate` | (0.3, 0, 1, 1) | Зникнення |
| `motion.easing.overshoot` | (0.34, 1.56, 0.64, 1) | «Прилипання» реп’яшка, пружні бейджі |
| `motion.spring.stickResponse/Damping` | 0.35 / 0.7 | Параметри SwiftUI-пружини для Live Activity й віджет-прев’ю |

**Сигнатурні анімації.**

| Назва | Що відбувається | Reduce Motion | Хептика (iOS / Android) |
|---|---|---|---|
| Прилипання | Реп’яшок в’їжджає з-за краю картки за `slow` + `overshoot`, масштаб 1.1 → 1.0, «чіпляється» до кута WordCard | Поява прозорістю за `base`, без зсуву й масштабу | немає |
| Розкриття перекладу | Переклад з’являється знизу на 8 pt із прозорістю за `base`; слово не рухається | Тільки прозорість | Light impact / `CLOCK_TICK` |
| Правильно | Контур QuizOption заливається `quiz.correctContainer`, галочка «домальовується» штрихом за `base`; підпис «Правильно» | Миттєва зміна кольору + іконка + текст | Notification success / `CONFIRM` |
| Неправильно | Два горизонтальні зсуви ±6 pt по `fast`; обраний варіант стає `incorrect`, правильний підсвічується `correct` з підписом «Правильна відповідь» | Без зсуву; лише кольори, іконки, текст | Notification error / `REJECT` |
| Свято серії | Вогник StreakBadge масштаб 1 → 1.2 → 1, до 12 частинок `reward.gold` за `celebrate`, число серії лічильником | Статичний бейдж + текст «Серія: 7 днів», без частинок | Medium impact ×2 / `LONG_PRESS` |
| Випадіння нагороди | RewardCard перевертається (rotateY 90° → 0°) за `slow`, лічильник колючок «дораховує» табличними цифрами за `celebrate` | Поява прозорістю, число одразу кінцеве | Light impact / `CLOCK_TICK` |
| Натискання кнопки | Масштаб 0.97 + затемнення заливки 8 % за `fast` | Тільки затемнення | Light impact (лише primary) / нічого |

Обмеження: жодних миготінь частіше 3 разів на секунду; жодних автоматичних анімацій довше 5 с; частинки ніколи не перекривають текст; на Android поважаємо `ANIMATOR_DURATION_SCALE = 0`, на iOS — `UIAccessibility.isReduceMotionEnabled`; обидва читаються через `IMotionPreferences` у Presentation-шарі. Хептика вимикається разом із системним налаштуванням і має власний перемикач у налаштуваннях.

### 3.9 Іконографія

| Контекст | Набір | Розміри | Штрих | Примітки |
|---|---|---|---|---|
| Застосунок (MAUI) | Lucide (ISC) через IconFont.Maui.SourceGenerator | 16 (бейджі, інлайн), 20 (кнопки, рядки), 24 (TopBar, TabBar) | 2 px на 16-20, 1.75 px на 24 | Один набір на всі платформи в застосунку |
| TabBar активний стан | Власні заповнені дублікати 5 іконок табів, намальовані на сітці Lucide 24 px | 24 | — | Lucide не має fill-набору; активний таб = заповнена іконка + підпис `label.md` 600 + колір `brand.primary` |
| iOS WidgetKit / Live Activity | SF Symbols | системні | — | Лише всередині розширення (ліцензія Apple); ніколи в спільних ресурсах |
| Android RemoteViews / сповіщення | Material Symbols (Apache 2.0) як vector drawable | 20-24 dp | — | Small icon сповіщення — монохромний силует реп’яшка `ic_stat_burdock` |
| Бейджі досягнень | Власні двоколірні ілюстрації на сітці 48 px | 48, 96 | контур 2 px | Кольори лише з рамп `sunny`, `violet`, `lime`; форма завжди відрізняється між бейджами (не лише колір); поряд завжди назва |

Правила: іконка без підпису має `SemanticProperties.Description`; декоративні іконки — `SemanticProperties.HeadingLevel` не використовується і `IsInAccessibleTree="False"`; колір іконки успадковується від тексту поруч.

### 3.10 Маскот Реп’яшок

**Характер.** Малий, круглий, пухнастий реп’яшок-квітка: доброзичливий, допитливий, трохи незграбний, «чіпляється» до слів, бо хоче, щоб вони лишились з тобою. Ніколи не сердиться на користувача.

**Пропорції та стиль.** Тіло — коло діаметром 1 u; 16-20 гачків-зубчиків по периметру довжиною 0.10-0.14 u із заокругленими кінчиками, злегка нерівномірні; очі — два овали висотою 0.28 u і шириною 0.22 u на 45 % висоти, відстань між ними 0.12 u; зіниці великі (0.6 висоти ока) `neutral.950` з одним білим відблиском угорі ліворуч; брови — короткі дуги, головний носій емоції; рот — мала дуга або крапка, з’являється лише у виражених станах; рум’янець `coral.200` у станах радості, свята й гордості. Контур відсутній; форма тримається на кольорі й м’якому внутрішньому затіненні. Палітра: тіло `violet.400` зі світлом `violet.300` і тінню `violet.600`; гачки `lime.400` з кінчиками `lime.600`; очі `neutral.0`; аксесуари — з будь-якої рампи, але максимум два кольори на аксесуар.

**Емоційні стани.**

| Стан | Файл | Ознаки | Коли використовувати | Коли заборонено |
|---|---|---|---|---|
| Радість | `mascot_joy` | Примружені очі-дуги, рум’янець | Правильна відповідь, завершена мікросесія, вітання вранці | Поряд із помилкою |
| Підбадьорення | `mascot_cheer` | Один гачок піднятий як «рука», відкриті очі | Порожній стан «ще не вчив сьогодні», сповіщення про повторення, друга спроба у quiz | Як реакція на помилку без тексту допомоги |
| Здивування | `mascot_surprise` | Круглі очі, підняті брови, малий рот-«о» | Новий рівень, перший віджет додано, незвичне слово | Системні помилки |
| Сум | `mascot_sad` | Опущені брови, погляд убік, гачки трохи опущені | Лише технічні стани: офлайн, синхронізація не вдалась, контент не завантажився | **Ніколи** для неправильної відповіді, пропущеного дня чи втраченої серії |
| Сон / тихі години | `mascot_sleep` | Заплющені очі, «z» з гачка, нічний ковпак опційно | Екран тихих годин, віджет уночі (лише в застосунку-прев’ю), стан «на сьогодні все» | Як докір за неактивність |
| Свято | `mascot_party` | Конфеті `reward.gold`, паперовий ковпачок, широка усмішка | Віха серії, тижневий календар, відкрита тема віджета | Частіше ніж раз на день у сповіщеннях |
| Задума | `mascot_think` | Погляд угору, гачок біля «підборіддя», три крапки | Завантаження, підбір слів, порожній результат пошуку | Довше 5 с без індикатора прогресу |
| Гордість | `mascot_proud` | Груди вперед (коло злегка витягнуте вгору), закриті очі, медаль опційно | Підсумок сесії, статистика тижня, «Вивчено» | На PaywallCard (не тиснемо гордістю) |

**Вбрання як косметика (Q34).** Аксесуари — окремі шари (`mascot_outfit_{id}`), вирівняні по тому ж полотну, накладаються на будь-який стан: ковпак, шарф, окуляри, вінок, навушники. Стани × вбрання не рендеряться попарно: тіло-стан + шар-вбрання складаються в рантаймі (у застосунку — два `Image` в `Grid`; у віджетах вбрання не показується у v1).

**Розміщення та розміри.** `mascot.xs` 32 pt (чіп, рядок таблиці лідерів у майбутньому), `sm` 56 pt (кут WordCard, Toast), `md` 96 pt (порожні стани в списках, RewardCard), `lg` 160 pt (EmptyState на весь екран, підсумок), `xl` 240 pt (онбординг, свято). Правила: один маскот на екран; ніколи не перекриває текст або ціль дотику; у віджетах лише `xs`/`sm` і лише у systemMedium/Large (не в accessory-родинах і не в StandBy); у сповіщеннях — лише як large icon (Android) або attachment (iOS) для нагород.

**Формати.** Джерело — SVG (`design/mascot/src/`), експорти PNG @2x/@3x (iOS) і mdpi-xxxhdpi (Android) для кожного стану й розміру; у MAUI використовується `MauiImage` з SVG-джерелом (генерує растри під платформи). Анімації — Lottie JSON через SkiaSharp.Extended.UI.Maui 3.0.0 (`SKLottieView`): у v1 три файли (`mascot_party.json`, `mascot_sleep.json`, `mascot_think.json`), кожен ≤ 150 KB, петля ≤ 3 с, без градієнтних мешів і растрів; при Reduce Motion показується статичний PNG того ж стану. У WidgetKit і RemoteViews — тільки PNG.

**Do / Don’t.** Так: показувати маскота там, де користувач чекає (порожньо, нагорода, завантаження); давати йому одну дію-жест; поважати тишу (у тихі години лише `sleep`). Ні: маскот у Dialog про видалення даних; маскот замість іконки помилки; більше одного маскота; розтягування пропорцій; обведення контуром; нові кольори поза рампами; використання у PaywallCard як «сумного», щоб підштовхнути до покупки.

**Бриф для генерації зображення (UA).** «Маскот мобільного застосунку для вивчення слів на ім’я Реп’яшок: маленька кругла пухнаста квітка реп’яха з 16-20 короткими заокругленими гачками по колу, дуже великими виразними очима з білим відблиском, без контуру, м’яке об’ємне затінення, плоский сучасний стиль з легкою текстурою. Кольори: тіло бузково-фіолетовий #9A8EFE зі світлом #B8B4FE і тінню #6D49EB, гачки лаймові #B4F461 з кінчиками #689C00, очі білі із зіницями #15131F, рум’янець #FFCBC7. Емоція: {радість | підбадьорення | здивування | сум | сон | свято | задума | гордість}. Персонаж по центру на прозорому фоні, вигляд трохи знизу, доброзичливий і допитливий, без тексту, без додаткових предметів, квадратне полотно 2048×2048.»

**Image prompt brief (EN).** "Mascot for a vocabulary-learning mobile app named Burdock: a small round fluffy burdock flower head with 16-20 short rounded hooks around its circumference, very large expressive eyes with a single white highlight, no outline, soft volumetric shading, flat modern style with a subtle texture. Colours: body lavender-violet #9A8EFE with highlight #B8B4FE and shadow #6D49EB, hooks lime #B4F461 with tips #689C00, eyes white with pupils #15131F, blush #FFCBC7. Emotion: {joy | encouragement | surprise | sadness | sleep | celebration | thinking | pride}. Character centred on a transparent background, viewed slightly from below, friendly and curious, no text, no extra props, square canvas 2048×2048."

### 3.11 Інвентар компонентів

Загальні правила для всіх компонентів: стани default / pressed / disabled / focused / loading / error задаються через `VisualStateManager`; pressed = масштаб 0.97 + затемнення 8 % (`fast`); disabled = прозорість 0.38 і `text.disabled`, без зміни макета; focused = кільце 3 pt `border.focus` зі зміщенням 2 pt (видиме для клавіатури, Switch Control, Full Keyboard Access); loading = Skeleton у розмірах компонента або спінер `brand.primary` замість підпису; error = контур `feedback.error` + повідомлення `body.sm` під елементом + іконка. Усі кольори лише через `{AppThemeBinding}` з `Colors.xaml` або `DynamicResource` (віджет-теми).

| Компонент | Анатомія | Варіанти | Ключові токени | MAUI-реалізація |
|---|---|---|---|---|
| Button | контейнер `radius.md`, висота 48 (lg) / 40 (md), іконка 20 ліворуч опційно, підпис `label.lg` | primary (заливка `brand.primary`, текст `onPrimary`), secondary (контур 1.5 pt `brand.primary`, текст `brand.primary`), tertiary (без контуру, текст `text.link`), destructive (заливка `feedback.error`, текст `#FFFFFF` — 5.42:1), icon (44×44 коло, іконка 24) | brand.*, feedback.error, border.focus | Власний `Border` + `Label` + `Image` у `ContentView` `BurdockButton` (не платформовий `Button` — заради станів і хептики) |
| Chip / FilterChip | `radius.sm`, висота 36, підпис `label.md`, іконка 16 опційно, галочка при виборі | вибір рівня (A1-C2), категорії (8), фільтри; single/multi | primaryContainer / onPrimaryContainer (вибрано), surface.sunken + border.default (не вибрано) | Syncfusion.Maui.Toolkit `SfChipGroup` зі стилями токенів або власний `Border` |
| WordCard (hero) | зверху чіп рівня + категорії, слово `word.hero`, IPA `word.ipa` + кнопка озвучення 44, частина мови `label.md` `text.tertiary`, переклад `body.lg` (прихований до дотику «Показати переклад» у режимі «спочатку згадай»), приклад `body.md` `text.secondary` з підсвіткою слова вагою 600, рядок метаданих «Показано 3 рази (ще 2 заплановано)» (T5) `label.sm`, дії: «Знаю», «Повторити пізніше», «Улюблене» | compact (список), full (екран слова), quiz-prompt (без перекладу) | surface.elevated, text.*, brand.primaryVivid (іконка звуку), reward.heart | `ContentView` `WordCard` на `Grid`; переклад розкривається `VisualState` «Revealed»; мотив «прилиплий реп’яшок» опційно |
| QuizOption | `radius.md`, мін. висота 56, текст `body.lg` зліва, індикатор-коло 24 справа | text (переклад/слово), з літерою-гарячою клавішею на iPad | quiz.selected*/correct*/incorrect*, surface.elevated | `Border` з `VisualStateManager`: Default, Selected, Correct, Incorrect, Disabled |
| TypedAnswerField | поле `radius.sm` висота 52, підказка «Введи слово англійською», лічильник підказок, кнопка «Підказати літеру» | default, near-miss (Damerau-Levenshtein ≤ 1 — контур `feedback.warning` і текст «Майже! Перевір написання»), correct, incorrect | border.strong/focus, feedback.* | `Entry` у `Border`; `Keyboard="Text"` з `IsSpellCheckEnabled=False`, `IsTextPredictionEnabled=False`; латинська розкладка не примусова (не можна на iOS) |
| MatchPair tile | плитка `radius.md` 100×64 мінімум, текст `title.md` по центру | ліва колонка EN, права UK; стани: idle, selected, matched (зникає прозорістю), mismatch (2 зсуви) | quiz.*, surface.elevated | `CollectionView` 2 колонки з власним `DataTemplate`; рівень 3 не має тіні під час вибору, підняття лише при перетягуванні (не у v1) |
| ProgressRing | коло 48-120, штрих 6-10, значення в центрі `numeric.stat` | денний прогрес, прогрес сесії, прогрес рівня | brand.primaryVivid (заливка), surface.sunken (трек), reward.gold (100 %) | `GraphicsView` з `IDrawable`; анімація значення за `base` |
| StreakBadge | пілюля `radius.full` висота 32, іконка вогника 20, число `label.md` 600 | active, frozen (іконка сніжинки, `streak.frozen*`), inactive (без числа, «Почни серію») | streak.* | `Border` + `HorizontalStackLayout`; ховається у режимі «без гейміфікації» |
| CurrencyPill (колючки) | пілюля висота 32, іконка реп’яшка-монети 20, число `label.md` табличними цифрами | static, animated (+N), pressed → веде до крамниці косметики | reward.currency* | `Border`; лічильник анімується `celebrate` |
| DailyCalendarTile | плитка 44×56 `radius.sm`, день тижня `label.sm`, крапка/галочка стану | done, today, missed (нейтрально, без червоного), frozen, future | streak.active/inactive/frozen, surface.sunken | `CollectionView` 7 колонок; «missed» — лише контур, без coral |
| TaskRow (мікрозавдання) | рядок 64, іконка 24 у колі `surface.sunken`, назва `title.md`, прогрес `label.sm` + тонка смуга 4 pt, нагорода праворуч у CurrencyPill-міні | active, done (галочка + прозорість 0.7), locked (Pro) | brand.primary, reward.currency* | `Grid` у `DataTemplate`; вся площа — одна ціль дотику |
| RewardCard | картка `radius.lg`, підкладка `reward.goldContainer`, маскот `md`, заголовок `headline.md`, підпис `body.md`, кнопка «Забрати» | daily, weekly, milestone, cosmetic-unlock (з прев’ю теми віджета) | reward.* | `Border`; анімація «випадіння нагороди»; після «Забрати» стає TaskRow-станом done |
| PaywallCard | заголовок `display.md`, три переваги з іконками, перемикач Місяць/Рік (SegmentedControl) з бейджем знижки `brand.accent`, ціна `headline.md`, кнопка primary «Спробувати тиждень безкоштовно», дрібний текст умов `body.sm` `text.secondary` з посиланнями | inline (у закритій функції), fullscreen | brand.*, surface.brandTint | `ContentView`; без таймерів, без «тільки сьогодні», без сумного маскота; кнопка закриття завжди видима й 44 pt |
| SettingRow | рядок 56, назва `title.md`, значення `body.md` `text.secondary`, елемент керування праворуч | switch, navigation (шеврон), value, destructive (текст `feedback.error`) | surface.elevated, border.default | `Grid` у групах `Border radius.md`; `Switch` платформовий з `OnColor=brand.primary` |
| Stepper / Slider (темп) | заголовок, поточне значення `numeric.stat` з одиницями, «−/+» 44 або повзунок з поділками | слова/день (3-30), інтервал (15 хв - 4 год), покази (1-10), сповіщення (0-8) (Q30) | brand.primary, surface.sunken | `Slider` для інтервалів з дискретними кроками + текстове значення; `Stepper` власний з двома icon-кнопками |
| TimeRangePicker (тихі години) | дві плитки «З» / «До» з часом `headline.sm`, між ними дуга-ілюстрація доби, підпис «22:00 - 08:00 · 10 год» | default, overnight | brand.primaryContainer, text.* | Дві `TimePicker` за плитками; ілюстрація `GraphicsView` |
| Toast / Snackbar | пілюля-прямокутник `radius.md`, іконка 20, текст `body.md` `text.inverse` на `text.primary`, дія опційно | info, success, error, undo (5 с) | text.inverse/primary, feedback.* | CommunityToolkit.Maui `Snackbar`/`Toast` зі стилем; над TabBar на `space.4` |
| BottomSheet | ручка 36×4 `border.strong`, заголовок `headline.md`, контент, кнопки внизу | detent 40 % / 90 %, з клавіатурою | surface.elevated, surface.overlay | Syncfusion.Maui.Toolkit `SfBottomSheet`; фокус потрапляє у заголовок |
| Dialog | картка `radius.lg` ширина ≤ 320, заголовок `headline.sm`, текст `body.md`, кнопки tertiary + primary/destructive | confirm, destructive, info | surface.elevated, surface.overlay | CommunityToolkit.Maui `Popup`; destructive-кнопка праворуч, фокус на безпечній дії |
| EmptyState | маскот `lg`, заголовок `headline.md`, пояснення `body.md`, одна кнопка | no-words-today, offline (`mascot_sad`), no-results (`mascot_think`), first-run (`mascot_cheer`) | surface.default, text.* | `ContentView` з параметром `MascotState`; Lottie лише для `think` |
| Skeleton / Shimmer | прямокутники `radius.sm` кольору `surface.sunken` з мерехтінням | text-lines, card, word-card | surface.sunken | Syncfusion `SfShimmer`; при Reduce Motion — статичний |
| TabBar | 5 вкладок: Сьогодні, Слова, Перевірка, Нагороди, Налаштування; іконка 24 + підпис `label.md` | стандартний, компактний (ландшафт iPhone — без підписів не дозволяється; підписи лишаються) | brand.primary (активна), text.secondary (неактивна), surface.elevated | Shell `TabBar` з `Shell.TabBarBackgroundColor` тощо з токенів; активна іконка — заповнений варіант |
| TopBar | заголовок `headline.sm` (або large `headline.lg` при прокрутці вгору), кнопка «назад» 44, до двох дій праворуч | default, large, transparent (над hero) | surface.default, text.primary | Shell `TitleView` або власний рядок у `Grid`; на iPad центрований заголовок |
| SegmentedControl | контейнер `surface.sunken` `radius.sm`, сегменти з підписом `label.md`, активний сегмент `surface.elevated` + elevation 1 | 2-4 сегменти | surface.*, text.* | Syncfusion `SfSegmentedControl` |
| Onboarding page indicator | крапки 8 pt з проміжком `space.2`, активна — капсула 24×8 `brand.primary` | 3-5 сторінок | brand.primary, border.strong | `IndicatorView` з `IndicatorTemplate` |

### 3.12 Віджети (головна сцена)

**Пріоритет змісту на будь-якому віджеті:** слово > транскрипція > переклад > приклад > метадані (рівень/категорія, лічильник) > дії. Якщо місця бракує, елементи зникають з кінця списку, слово ніколи не обрізається (зменшення до 70 % розміру дозволене).

**Спільні правила iOS.** Фон — `containerBackground` (iOS 17+) з градієнтом `widget.background → widget.backgroundEnd`, щоб система могла прибрати його у StandBy і tinted-режимі; на iOS 16 — звичайний `ZStack` (Q25). Ієрархія тримається на розмірі, вазі й інтервалах, а не на кольорі: у accented (tinted, iOS 18+) та vibrant (екран блокування) режимах усе стає монохромним із системним тінтом, у прозорому режимі iOS 26 фон зникає (звіт, розділи 1 і 4). Маркуємо `.widgetAccentable()` слово й кнопку «Наступне», решта лишається первинним шаром; растрові зображення маскота отримують `widgetAccentedRenderingMode` (iOS 18+), а у v1 у systemSmall маскот відсутній. Шрифт — системний (Fixel у розширенні не вбудовується у v1, щоб не збільшувати пам’ять розширення; переоцінити у v1.1). Розміри тексту нижче — для стандартного Dynamic Type; при Larger Text транскрипція зникає першою, потім приклад.

**Кнопка «Наступне слово».** iOS 17+: `Button(intent: NextWordIntent())` у нижньому правому куті, візуально коло 28 pt `widget.button` з SF Symbol `arrow.right` у `widget.onButton`, зона дотику 44 pt (Q36; інтерактивність гарантована і не витрачає бюджет оновлень). iOS 16: та сама зона без стилю кнопки, дотик відкриває застосунок. В accessory-родинах кнопки немає — дотик відкриває застосунок (припущення: інтерактивні елементи в accessory-родинах не використовуємо у v1).

**Deep links.** systemSmall — один `widgetURL`: `burdock://word/{lexemeId}?src=widget.small`. systemMedium/Large — `Link` по зонах: слово → `burdock://word/{lexemeId}`, «Перевірити» → `burdock://quiz?src=widget`, список нещодавніх → `burdock://word/{id}`. Accessory → `burdock://word/{lexemeId}?src=lockscreen`. Кожен `src` потрапляє в `exposureConfirmed` (T5).

**Бейдж безкоштовного режиму.** Віджет ніколи не є рекламою: слово «Pro» на віджеті не з’являється. Після вичерпання денного пулу з 5 слів (Q14) systemMedium/Large показують пілюлю «5/5 сьогодні» (`widget.badgeFree`/`onBadgeFree`) замість лічильника; systemSmall і accessory-родини не показують нічого додаткового. Пояснення і пропозиція Pro живуть у застосунку.

**Тема віджета** застосовується з App Group (ключ `widgetTheme`), значення `tsvit|luh|nich|sontse`; у tinted/vibrant режимах тема не має ефекту (система фарбує сама) — це очікувано і не вважається дефектом.

**Приватність.** Слово й переклад показуються без редагування; серія, прогрес і лічильники позначені `.privacySensitive()` і на екрані блокування редагуються (звіт, розділ 5). Налаштування «Ховати переклад на екрані блокування» (за замовчуванням вимкнене, припущення) замінює переклад на «Торкнись, щоб згадати».

#### iOS: родини

```
systemSmall (2×2)                    systemMedium (4×2)
┌────────────────────┐               ┌──────────────────────────────────────────┐
│ A2 · Подорожі      │ chip 11 pt    │ serendipity              A2 · Подорожі   │ word 26 Bold
│                    │               │ /ˌsɛrənˈdɪpəti/ · ім.                     │ ipa 13 / pos 12
│ serendipity        │ word 22 Bold  │ щасливий випадок                         │ translation 17 Medium
│ /ˌsɛrənˈdɪpəti/    │ ipa 12        │ «She found the café by pure serendipity.»│ example 13
│ щасливий випадок   │ transl. 15 Md │                                          │
│                    │               │ ▸ Перевірити         3/5 сьогодні   (→)  │ link 13 · counter 12 · button
│ 3/5 сьогодні   (→) │ 12 · button   └──────────────────────────────────────────┘
└────────────────────┘

systemLarge (4×4)                                   accessoryRectangular
┌──────────────────────────────────────────┐        ┌──────────────────────┐
│ Слово години                A2 · Подорожі│ 12     │ ✦ serendipity        │ headline Bold
│ serendipity                              │ 30 B   │ щасливий випадок     │ body 13
│ /ˌsɛrənˈdɪpəti/ · іменник                 │ 15/13  │ /ˌsɛrənˈdɪpəti/      │ caption 11 (перше зникає)
│ щасливий випадок                         │ 20 Md  └──────────────────────┘
│ «She found the café by pure serendipity.»│ 15
│ ─────────────────────────────────────    │        accessoryInline
│ Нещодавні                                │ 12     ✦ serendipity — щасливий випадок
│ • wander          блукати                │ 15/15  (переклад обрізається першим)
│ • itinerary       маршрут                │
│ • layover         пересадка              │        accessoryCircular
│ ─────────────────────────────────────    │        ╭────────╮
│ [ Перевірити 3 слова ]  3/5 сьогодні (→) │        │  seren │ слово ≤ 7 літер 13 Bold,
└──────────────────────────────────────────┘        │   EN   │ інакше — гліф реп’яшка
                                                    ╰────────╯
```

**StandBy (iOS 17+).** Показує systemSmall у збільшеному вигляді на чорному тлі, фон контейнера система прибирає: слово має бути ≥ 24 pt ефективно (масштабування виконує система), лаймові заливки не використовуються (стануть білими), акцент рівня — контуром чіпа. Тема «Ніч» спроєктована як еталон для StandBy; нічний режим StandBy (червоний тінт) обробляється системою.

**Кольорова поведінка за режимами.**

| Режим | Фон | Слово | Переклад / IPA | Чіп, кнопка | Перевірка |
|---|---|---|---|---|---|
| fullColor | градієнт теми | `widget.word` | `widget.translation` / `widget.ipa` | заливки теми | таблиця 3.3.3 і 3.3.5 |
| accented / tinted (iOS 18+) | системний тінт-скло | білий (accentable) | білий 70-80 % (система) | кнопка accentable, чіп — контур | ієрархія читається у чорно-білому знімку |
| vibrant (екран блокування) | прозорий | системний | системний | немає заливок | текст ≥ 11 pt, без тонких штрихів < 1.5 pt |
| clear (iOS 26) | прозорий/скло | білий | білий | контури | те саме, що для vibrant; знімок на світлих і темних шпалерах |

#### Android (2×2, 4×2, 4×4)

```
4×2                                                2×2
┌───────────────────────────────────────────┐      ┌────────────────────┐
│ serendipity                 A2 · Подорожі │      │ serendipity        │ 22 sp bold
│ /ˌsɛrənˈdɪpəti/ · ім.                      │      │ щасливий випадок   │ 14 sp
│ щасливий випадок                          │      │            (→)     │
│ [Перевірити]              3/5       (→)   │      └────────────────────┘
└───────────────────────────────────────────┘      4×4 = 4×2 + приклад + 3 нещодавні + кнопка «Перевірити»
```

Material You (API 31+): фон `system_accent1_600`/`system_neutral1_*` за темою системи, текст `system_accent1_50`, кнопка `system_accent2_200` — через тематизовані атрибути в XML-макеті; Android 12 і новіші користувачі бачать віджет у кольорах шпалер (T19). Відкат (API 26-30 або вимкнені динамічні кольори): `color_widget_*` з `values/colors.xml` і `values-night/colors.xml`, тобто активна тема віджета. RemoteViews не підтримують власні шрифти — типографіка: Roboto / `sans-serif-medium`, слово 22/26/30 sp bold, переклад 14/16/18 sp, IPA 12/13/14 sp; текст ніколи не рендериться у Bitmap (звіт: жорсткий ліміт пам’яті растрів у RemoteViews на цільовому API 37); маскот — лише один PNG ≤ 96 dp у 4×4. Радіус фону збігається із системним радіусом віджетів (API 31+, припущення щодо конкретного ресурсу — перевірити). Кнопка «Наступне» — `PendingIntent` на `BroadcastReceiver`, 48 dp ціль. Віджет має проходити Android Widget Quality Tier 1 (звіт, розділ 4).

### 3.13 Сповіщення та Live Activity

**Спільні правила сповіщень.** Заголовок — завжди англійське слово (чіпляється з першого погляду); підзаголовок — транскрипція і частина мови; тіло — переклад і приклад або «підказка згадати», якщо ввімкнено «ховати переклад»; до 3 на день за замовчуванням, тихі години 22:00-08:00 (Q30); жодного loss-framing, жодних «ти втратиш серію» (T17); дії — не більше трьох; текст тіла ≤ 120 символів; апостроф U+2019; емодзі не використовуються. iOS: іконка сповіщення — іконка застосунку (система), зображення маскота лише як attachment у сповіщеннях нагород (≤ 1/день). Android: канали «Слова», «Повторення», «Сесія», «Нагороди»; small icon — монохромний силует реп’яшка `ic_stat_burdock`; large icon — PNG стану маскота; стиль `BigTextStyle`.

| ID | Тригер | Заголовок | Підзаголовок | Тіло | Дії | Маскот |
|---|---|---|---|---|---|---|
| `word.default` | розклад показів | serendipity | /ˌsɛrənˈdɪpəti/ · іменник | щасливий випадок · «She found the café by pure serendipity.» | Знаю · Наступне | немає |
| `word.hidden` | розклад + «ховати переклад» | serendipity | /ˌsɛrənˈdɪpəti/ · іменник | Пам’ятаєш переклад? Торкнись, щоб перевірити. | Показати переклад · Наступне | немає |
| `review.due` | ≥ 5 карток до повторення, після тихих годин | 5 слів чекають на повторення | — | Три хвилини — і вони стануть твоїми. Почнемо? | Почати · Пізніше | Android large icon: підбадьорення |
| `streak.gentle` | 20:00, день ще не зараховано, ≤ 1/день | Сьогодні ще без сесії | — | Коротка сесія — і день у серії. Якщо не сьогодні, заморозка вже є. | Почати | немає |
| `reward.granted` | денна/тижнева нагорода | +15 колючок | — | День 4 тижневого календаря. Дякуємо, що вчишся. | Переглянути | attachment / large icon: свято |
| `session.ended` | завершення Live Activity | Сесію завершено · 12 слів | — | Найкраще зачепилось: serendipity. Перевіримо завтра. | Переглянути | немає |
| `morning.word` | перше слово після тихих годин | wander | /ˈwɒndə/ · дієслово | блукати · Доброго ранку! | Знаю · Наступне | немає |

Дія «Знаю» фіксує підтверджений показ (T5) і не оцінює картку FSRS (T4). Дія «Показати переклад» оновлює те саме сповіщення (той самий ідентифікатор), а не створює нове.

**Live Activity «сесія» (Q35).** Запускається лише користувачем на 1-8 год; показує поточне слово, прогрес і кнопку завершення; оновлення — за інтервалом зміни слова. Візуально: темний системний фон, текст білий/`neutral.200`, акцент `brand.primaryVivid` для прогресу і гліфа реп’яшка (SF Symbol-подібний власний символ у розширенні), кнопки «Знаю» і «Завершити» (iOS 17+ `Button(intent:)`, на iOS 16 — лише перегляд і завершення із застосунку).

```
Екран блокування / банер
┌────────────────────────────────────────────────────┐
│ ✦ Сесія слів · залишилось 1 год 12 хв         A2   │ caption 13
│ serendipity                    /ˌsɛrənˈdɪpəti/     │ 24 Bold · ipa 13
│ щасливий випадок                                   │ 17
│ ●●●●○○○○○○○○  4/12                [Знаю] [Завершити]│ progress · buttons 44 pt
└────────────────────────────────────────────────────┘

Dynamic Island
compact leading: ✦        compact trailing: serendipity (≤ 12 символів) або «4/12»
minimal: ✦ (з кільцем прогресу)
expanded:
┌────────────────────────────────────────────────────┐
│ ✦ 4/12                                   1:12 год  │ leading / trailing
│ serendipity  ·  /ˌsɛrənˈdɪpəti/                     │ center
│ щасливий випадок                                   │
│ [Знаю]                                [Завершити]  │ bottom
└────────────────────────────────────────────────────┘
```

### 3.14 Темна тема

Темна тема — не інверсія. Поверхні тональні (`neutral.950 → 900 → 1000`), підйом виражається світлішою поверхнею й контуром, а не тінню; фірмові кольори світлішають і трохи десатуруються (`brand.primary` = `violet.400`, посилання `violet.300`), а яскраві якорі (`lime.400`, `sunny.400`, `coral.400`) лишаються заливками з темним чорнилом; текст помилок/успіху — 300-ті ступені. Ілюстрації та маскот не мають окремої темної версії, лише фон-ореол `surface.brandTint`. Реалізація: `Colors.xaml` містить пару ключів `*Light`/`*Dark` для кожного семантичного токена; `Styles.xaml` використовує `{AppThemeBinding Light={StaticResource ColorTextPrimaryLight}, Dark={StaticResource ColorTextPrimaryDark}}`; `Application.UserAppTheme` за замовчуванням `Unspecified` (стежить за системою), у налаштуваннях — Системна / Світла / Темна; `<UseMaterial3>true</UseMaterial3>` на Android із прибраними шаблонними перевизначеннями кольорів (звіт, розділ 4). Віджети iOS отримують `Color(light:dark:)` з `DesignTokens.swift` і додатково реагують на режим рендерингу; Android — `values-night`.

### 3.15 Доступність

- **Контраст:** таблиця 3.3.3 — обов’язкова перевірка в CI; будь-який новий токен додається лише з парою і коефіцієнтом.
- **Dynamic Type до AX5 / fontScale 2.0:** ролі мають стелі (3.4); при множнику ≥ 1.5 рядки «слово + дії», «чіп + чіп», «ціна + кнопка» перетікають у стовпчик; TabBar зберігає підписи; слово-герой переносить рядок, не обрізається; iOS Larger Text критерії App Store Connect виконуються (звіт, розділ 4).
- **VoiceOver / TalkBack:** WordCard читається як одна група: «serendipity, транскрипція sɛrənˈdɪpəti, іменник, переклад: щасливий випадок, показано 3 рази. Дії: озвучити, знаю, повторити пізніше»; QuizOption — «варіант 2 з 4, щасливий випадок, не вибрано»; після перевірки — «правильно» або «неправильно, правильна відповідь: …». Англійські слова позначаються мовою `en` (iOS: атрибут мови мовлення в accessibility-мітці через обробник; Android: `LocaleSpan`/`TextLocale` через обробник) — прикріплена властивість `Speech.Language="en-US"` (за замовчуванням GA, користувач може обрати RP у налаштуваннях вимови). IPA не озвучується як текст: замість неї — вимова слова системним TTS (T7).
- **Порядок фокуса:** TopBar → зміст зверху вниз → основна дія → TabBar; у quiz фокус після відповіді переходить на пояснення, потім на «Далі».
- **Reduce Motion:** таблиця 3.8; Lottie замінюється статичним кадром; Shimmer статичний; автоматичні каруселі не використовуються взагалі.
- **Колір не самотній:** правильно/неправильно = колір + іконка + слово; серія = число + іконка; заморозка = сніжинка + текст; рівень = літера-код (A2), не лише колір чіпа.
- **Цілі дотику:** 3.5; перевіряються у Accessibility Inspector та Android Accessibility Scanner на знімках з емулятора.
- **Час:** без таймерів у вправах за замовчуванням; таймер сесії Live Activity лише інформаційний.
- **Мова інтерфейсу:** українська типова (Закон 2704-VIII, звіт розділ 5), `NeutralLanguage=uk`.

### 3.16 Імплементація токенів

**Джерело істини:** `design/tokens/tokens.json` у форматі W3C Design Tokens (DTCG): `$type`/`$value`, аліаси у формі `{color.primitive.violet.600}`, режими — окремі гілки `color.semantic.light` та `color.semantic.dark` (обрано замість `$extensions.modes`, бо Style Dictionary, XAML-генератор і Swift споживають явні набори на режим), розв’язані hex дубльовані в `$extensions["org.burdock.tokens"].resolved` для інструментів без підтримки аліасів; теми віджета — `color.theme.widget.{key}.{light|dark}`; типографіка, відступи, радіуси, тіні, рух — у власних гілках з одиницями `pt`/`ms`.

**Генератор `design/tokens/generate.py`** (Python 3.13, лише стандартна бібліотека, кросплатформовий; майбутній .NET-інструмент може замінити його з тим самим інтерфейсом):

| Команда | Дія |
|---|---|
| `generate.py --check` | Валідація JSON і аліасів; перерахунок усіх пар контрасту з розділу 3.3.3 і тем; код виходу 1, якщо будь-яка пара нижче порога або є hex поза примітивами. Запускається в CI на кожен PR |
| `generate.py --out xaml` | `src/mobile/Burdock.Presentation/Resources/Styles/Colors.xaml` (примітиви `Violet500`, семантика `ColorBrandPrimaryLight`/`ColorBrandPrimaryDark`, теми `WidgetThemes/*.xaml` з ключами без суфікса для `DynamicResource`), `Styles.xaml` між маркерами `<!-- generated:start/end -->` (базові стилі Label/Button/Border з `AppThemeBinding`, ресурси `Space4`, `RadiusMd`, `TypeBodyLg`, `MotionBase`) |
| `generate.py --out swift` | `src/mobile/ios-extensions/BurdockWidgets/DesignTokens.swift`: `enum DesignTokens { enum Primitive; enum Semantic (Color(light:dark:)); enum WidgetTheme: String { var background, backgroundEnd, word, … }; enum Spacing; enum Radius; enum Motion }` |
| `generate.py --out android` | `src/mobile/Burdock/Platforms/Android/Resources/values/colors.xml`, `values-night/colors.xml` (`color_brand_primary`, `primitive_violet_500`, `widget_theme_luh_background`), `values/dimens.xml` (`space_4`, `radius_md`) |
| `generate.py --out css` | Блок CSS custom properties між маркерами у `design/preview/index.html` |
| `generate.py --report` | Markdown-таблиці контрасту для вставки у цю специфікацію |

**Конвенції іменування.**

| JSON-шлях | XAML | Swift | Android | CSS |
|---|---|---|---|---|
| `color.primitive.violet.500` | `Violet500` | `DesignTokens.Primitive.violet500` | `@color/primitive_violet_500` | `--color-primitive-violet-500` |
| `color.semantic.light.brand.primary` | `ColorBrandPrimaryLight` | `DesignTokens.Semantic.brandPrimary` (light/dark всередині) | `@color/color_brand_primary` (у `values`) | `--color-brand-primary` (у `[data-theme="light"]`) |
| `color.theme.widget.luh.light.widget.background` | `WidgetBackground` у `WidgetThemes/Luh.xaml` | `WidgetTheme.luh.background` | `@color/widget_theme_luh_background` | `.wtheme-luh --color-widget-background` |
| `spacing.4` | `Space4` (`x:Double`) | `DesignTokens.Spacing.s4` | `@dimen/space_4` | `--spacing-4` |
| `typography.scale.body.lg` | стиль `TypeBodyLg` | `DesignTokens.Type.bodyLg` (Font) | `@style/TypeBodyLg` | `.type-body-lg` |

**Як додати токен.** (1) Додати примітив лише якщо жоден ступінь рампи не підходить (потрібне обґрунтування в PR). (2) Додати семантичний токен в обидва режими з `$description`. (3) Додати пари контрасту у список перевірки генератора, якщо токен несе текст або межу. (4) `generate.py --check && generate.py --out all`. (5) Оновити прев’ю і цю специфікацію (таблиці 3.3.2/3.3.3). (6) У PR — знімки світлої й темної теми.

**Чеклист рев’ю токенів:** назва описує роль, а не колір; є обидва режими; пари контрасту проходять; немає дубліката з іншою назвою; використання задокументоване; згенеровані файли не редагувалися вручну (CI порівнює хеш).

### 3.17 Інструменти дизайну та ШІ-навички

| Інструмент | Для чого | Межі |
|---|---|---|
| `dotnet/skills` → плагін `dotnet-maui` (maui-theming, shell-navigation, DI, CollectionView) | Каркас тем і стилів MAUI, рецепти `AppThemeBinding`/`ResourceDictionary` | Офіційний, але загальний: значення токенів беруться лише з `tokens.json` |
| `davidortinau/maui-skills` (ux-first-principles, ux-mobile, accessibility, theming, localization) | Рев’ю UX і доступності XAML, чеклисти для PR | MIT; не знає наших токенів — подавати як контекст |
| `nextlevelbuilder/ui-ux-pro-max` | Лише довідник міркувань (палітри, пари шрифтів, 119 UX-правил) під SwiftUI/Compose-профілі | Не має MAUI-стеку; **ніколи не генерує код** для репозиторію |
| Google Stitch (MCP `https://stitch.googleapis.com/mcp`, `@google/stitch-sdk`) | Концепт-екрани і варіанти композиції в HTML для обговорення | Вивід — HTML, не XAML; кольори підмінюються токенами перед перенесенням |
| Excalidraw MCP (`yctimlin/mcp_excalidraw`) | Потоки екранів і діаграми, що комітяться поряд зі специфікаціями | Не для піксельних макетів |
| Claude Design | Каркас дизайн-системи, `/design-sync` між специфікацією та прев’ю | Бета; результати проходять `generate.py --check` |
| Figma + iOS/iPadOS 26 kit, Material 3 kit (за потреби) | Детальні макети, коли HTML-прев’ю недостатньо | Figma MCP: одна Dev-ліцензія (200 викликів/день); без неї — Stitch + прев’ю + XAML |
| `mallibone/maui-ai-design-skill` | Потенційно генерація Colors.xaml/WCAG-таблиць | Немає файлу ліцензії — не використовувати до згоди автора |
| Material Theme Builder | Одноразовий експорт Material You ролей для Android-віджетів | Репозиторій архівований — експортувати один раз у `design/tokens/android-m3.json` |

**Цикл рев’ю за знімками.** Локально (Windows): Android-емулятор API 36, `adb exec-out screencap -p > shot.png` у світлій/темній темі й fontScale 2.0, автоматизовано в `tools/screenshots.ps1`; знімок додається до PR. У CI (macOS): збірка + `xcrun simctl io booted screenshot` для iPhone (compact) та iPad (regular), світла/темна, AX5, плюс знімки віджет-галереї симулятора для systemSmall/Medium/Large; артефакти прикріплюються до PR. Рев’ю виконує ШІ-агент за чеклистом 3.18 і людина — за 5 хвилин на екран. Playwright-подібні веб-інструменти MAUI не бачать; Accessibility Inspector (macOS) і Accessibility Scanner (Android) — для цілей дотику та міток.

### 3.18 Чеклист дизайн-QA екрана та політика дизайн-боргу

Кожен екран/віджет перед злиттям:

1. Одне головне слово/дія читається за перший погляд.
2. Жодного hex-коду в XAML/Swift/Kotlin; лише токени.
3. Усі текстові пари з таблиці 3.3.3; нові пари додано до генератора.
4. Світла й темна теми на знімках; темна не інвертована (тональні поверхні).
5. AX5 / fontScale 2.0: нічого не обрізано, ряди перетікають у стовпчик.
6. Мінімум 12 pt тексту в застосунку, 11 pt у віджеті.
7. Цілі дотику 44/48 і проміжок 8 pt.
8. Фокус видимий (кільце 3 pt) і порядок логічний.
9. VoiceOver/TalkBack: мітки, групи, мовний тег `en` для англійських слів.
10. Сенс не лише кольором: іконка + текст.
11. Reduce Motion перевірено; жодних миготінь > 3/с.
12. Українська типографіка: ’ «» — ґ є ї, без фальшивого курсиву, плюральні форми.
13. IPA системним шрифтом, окремим рядком.
14. Один Реп’яшок максимум, у дозволеному стані, не перекриває контент.
15. Безпечні зони й Dynamic Island враховані.
16. iPad: ширина контенту ≤ 640 pt, двопанельний режим працює у ландшафті та Split View.
17. Стани default/pressed/disabled/focused/loading/error присутні.
18. Порожній стан і стан помилки спроєктовані, а не «нічого».
19. Тексти без loss-framing і без «тільки сьогодні»; кнопка закриття Paywall видима.
20. Віджет перевірено у fullColor, tinted, vibrant (екран блокування) і на світлих/темних шпалерах; слово не обрізане.

**Політика дизайн-боргу.** Будь-яке свідоме відхилення від цієї специфікації фіксується задачею з міткою `design-debt` із посиланням на токен/компонент і чеклист-пункт; не більше 5 відкритих задач на реліз, перегляд щотижня; борг, що впливає на доступність (пункти 3, 5, 7, 9, 10, 11), не може бути відкладений за реліз; нові екрани з hex-кодами відхиляються CI без обговорення; кожен третій спринт містить «день дизайн-боргу».

## 4. Рішення та компроміси

| Питання | Варіанти | Обрано і чому |
|---|---|---|
| Фірмовий `#7C5CFF` як заливка кнопок | (a) залишити, миритися з 4.35:1; (b) збільшити текст кнопок до «великого»; (c) сусідній ступінь `violet.600` для тексту-носіїв, `#7C5CFF` для всього іншого | (c): AA без компромісів, відтінок збережений, `#7C5CFF` лишається помітним у іконках, кільці прогресу, ілюстраціях і дисплейному тексті |
| Побудова рамп | HSL-зсуви; Material HCT; OKLCH | OKLCH: рівномірна сприймана яскравість і збереження відтінку; якорі точні; критичні ступені підігнані під цільову яскравість |
| Формат режимів у `tokens.json` | `$extensions.modes` в одному токені; окремі гілки light/dark | Окремі гілки: простіше для генераторів XAML/Swift/Android і для diff у PR; резолюція аліасів однакова |
| Шрифт віджетів iOS | Вбудувати Fixel у розширення; системний шрифт | Системний у v1: пам’ять розширення і tinted-режим; Fixel — у v1.1 після вимірювання |
| IPA-гарнітура | Шукати шрифт з IPA + кирилицею; системний шрифт для IPA | Системний: перевірено, що Fixel не має IPA; SF/Roboto мають повний блок |
| Активний таб | Товщий штрих Lucide; заповнені власні іконки | Власні заповнені 5 іконок: чіткіша різниця стану без опори на колір |
| Теми віджета | Лише світла/темна; 4 теми як нагорода | 4 теми: реалізують Q34 дешево (лише `widget.*`), кожна перевірена на контраст |
| Маскот у сповіщеннях | Скрізь; лише нагороди | Лише нагороди (≤ 1/день): сповіщення слів мають лишатися інформативними (звіт, розділ 7.46 і T17) |
| Червоне для пропущеного дня календаря | coral; нейтральний контур | Нейтральний контур: пропуск — не помилка (T11, етична гейміфікація) |
| Компонентні бібліотеки | Telerik/DevExpress; MIT/Apache | CommunityToolkit.Maui + Syncfusion.Maui.Toolkit + SkiaSharp (звіт, розділ 4; політика ліцензій) |

ADR для затвердження: `docs/adr/ADR-000X-brand-primary-violet-600.md` (компроміс `#7C5CFF`), `ADR-000Y-token-format-modes.md`.

## 5. Критерії приймання

1. **Given** `tokens.json` **When** `python design/tokens/generate.py --check` **Then** код виходу 0 і звіт «122 pass, 0 fail» для семантичних пар та «88 pass» для тем віджета.
2. **Given** будь-який XAML у `src/mobile` **When** CI шукає `#[0-9A-Fa-f]{6,8}` поза `Colors.xaml`/`WidgetThemes` **Then** збігів немає.
3. **Given** iPhone із Dynamic Type AX5 **When** відкрито WordCard зі словом «responsibility» і перекладом «відповідальність» **Then** слово й переклад видимі повністю, кнопки під ними в стовпчик, нічого не обрізано.
4. **Given** увімкнений Reduce Motion **When** користувач відповідає неправильно **Then** зсуву картки немає; є зміна кольору, іконка і текст «Правильна відповідь: …».
5. **Given** віджет systemSmall у tinted-режимі iOS 18 **When** зроблено знімок **Then** слово, переклад і кнопка розрізняються розміром/вагою, на знімку в градаціях сірого ієрархія збережена.
6. **Given** екран блокування iOS **When** показано accessoryRectangular **Then** слово й переклад видимі, серія/лічильники редаговані (`privacySensitive`).
7. **Given** тема «Луг» вибрана у застосунку **When** віджет перезавантажено **Then** systemMedium використовує значення `color.theme.widget.luh` відповідного режиму; прев’ю в застосунку показує ті самі кольори через `DynamicResource`.
8. **Given** Android 12+ з увімкненими динамічними кольорами **When** додано віджет 4×2 **Then** фон і текст беруться з системних ролей акценту; на Android 8-11 — з `color_widget_*`.
9. **Given** VoiceOver українською **When** фокус на слові «serendipity» **Then** слово вимовляється англійським голосом (мовний тег), далі йде український переклад.
10. **Given** сповіщення `word.hidden` **When** натиснуто «Показати переклад» **Then** оновлено те саме сповіщення з перекладом, нове не створене.
11. **Given** будь-який екран **When** перераховано екземпляри маскота **Then** ≤ 1; на PaywallCard стан ≠ сум.
12. **Given** `design/preview/index.html` **When** відкрито у браузері без мережі **Then** сторінка рендериться з Inter/системним шрифтом, перемикач Light/Dark працює, коефіцієнти контрасту збігаються з таблицею 3.3.3 з точністю до 0.01.
13. **Given** iPad у ландшафті **When** відкрито «Слова» **Then** список і деталь показані двома панелями, ширина деталі ≤ 640 pt; у Split View 1/3 — одна колонка.

## 6. Ризики

| Ризик | Ймовірність | Вплив | Пом’якшення |
|---|---|---|---|
| Fixel у MAUI на iOS/Android рендерить кернінг/апостроф інакше, ніж у прев’ю | Середня | Середній | Тестовий рядок «Ґудзик є їжаку — щастя’» у першому екрані UI-тесту; знімки в CI |
| Tinted/clear режими iOS роблять тему віджета непомітною і користувач вважає нагороду «зламаною» | Висока | Середній | Пояснення на екрані вибору теми: «У tinted-режимі iOS фарбує віджет сам»; прев’ю режимів у застосунку |
| Material You перефарбовує Android-віджет так, що тема-нагорода не видна | Висока | Низький | Перемикач «Використовувати кольори системи» у налаштуваннях віджета; за замовчуванням увімкнений (T19) |
| Обмеження масштабу display-ролей (1.3×) сприйматиметься як порушення Larger Text | Низька | Високий | Body-ролі ростуть до AX5; display лише для декоративних чисел; заголовки 1.5×; перевірка за критеріями App Store Connect |
| `tnum` через обробники не спрацює на одній із платформ | Середня | Низький | Відкат: моноширинні цифри системного шрифту для статистики |
| Розширення WidgetKit перевищує пам’ять через PNG маскота | Середня | Високий | ≤ 2 масштаби, ≤ 60 KB кожен, без маскота в systemSmall; вимірювання в CI |
| Lottie-файли з зовнішніх інструментів містять непідтримувані фічі SkiaSharp | Середня | Низький | Перевірка відтворення на емуляторі в PR; статичний PNG як відкат |
| Соло-розробник не має часу на 8 станів маскота у високій якості | Висока | Середній | Генерація за брифом 3.10 + ручне доведення; спочатку 4 стани (радість, підбадьорення, задума, свято), решта до релізу |

## 7. Відкриті питання

| Питання | Хто вирішує | До коли |
|---|---|---|
| Остаточні ціни тем віджета в колючках (120/120/200 — припущення) | Власник продукту разом зі специфікацією нагород | До фіналізації економіки нагород |
| Чи вбудовувати Fixel у WidgetKit-розширення у v1.1 (пам’ять, tinted) | Архітектор після вимірювань на CI | v1.1 |
| Інструмент і формат багатошарової іконки iOS 26 у CI без локального Xcode | Архітектор | До подачі в App Store |
| Налаштування «ховати переклад на екрані блокування» — потрібне у v1? | Власник продукту | До завершення специфікації віджетів |
| Хто малює/доводить маскота (генерація + ілюстратор?) і бюджет | Власник продукту | До початку роботи над онбордингом |
| Чи потрібна користувачу примусова тема застосунку (Світла/Темна) у v1, чи достатньо системної | Власник продукту | Під час специфікації налаштувань |
| Перевірка ресурсу системного радіуса віджета Android і поведінки на Samsung | Розробник Android | Перший Android-реліз |

## 8. Джерела

- WCAG 2.2, Understanding Contrast (Minimum): https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html
- WCAG 2.2, Timing Adjustable: https://www.w3.org/WAI/WCAG22/Understanding/timing-adjustable.html
- Apple HIG, Dark Mode (ціль 7:1 для власних кольорів): https://developer.apple.com/design/human-interface-guidelines/dark-mode
- Apple HIG, SF Symbols (ліцензія лише для платформ Apple): https://developer.apple.com/design/human-interface-guidelines/sf-symbols
- Apple HIG, Live Activities: https://developer.apple.com/design/human-interface-guidelines/live-activities
- WidgetKit, widgetAccentedRenderingMode (iOS 18+): https://developer.apple.com/documentation/widgetkit/widgetaccentedrenderingmode
- WidgetKit, accessoryRectangular: https://developer.apple.com/documentation/widgetkit/widgetfamily/accessoryrectangular
- WidgetKit, StandBy: https://developer.apple.com/documentation/widgetkit/widgetlocation/standby
- WidgetKit, Keeping a widget up to date: https://developer.apple.com/documentation/widgetkit/keeping-a-widget-up-to-date
- WidgetKit, Adding interactivity to widgets and Live Activities: https://developer.apple.com/documentation/widgetkit/adding-interactivity-to-widgets-and-live-activities
- ActivityKit, Displaying live data with Live Activities: https://developer.apple.com/documentation/activitykit/displaying-live-data-with-live-activities
- Apple Platform Security, WidgetKit security (privacySensitive): https://support.apple.com/guide/security/widgetkit-security-secbb0a1f9b4/web
- App Store Connect, Larger Text evaluation criteria: https://developer.apple.com/help/app-store-connect/manage-app-accessibility/larger-text-evaluation-criteria/
- UserNotifications, Handling notifications and actions: https://developer.apple.com/documentation/usernotifications/handling-notifications-and-notification-related-actions
- App Store Review Guidelines: https://developer.apple.com/app-store/review/guidelines/
- Android Widget Quality guidelines: https://developer.android.com/docs/quality-guidelines/widget-quality
- Android, Advanced app widgets: https://developer.android.com/develop/ui/views/appwidgets/advanced
- Android 17 behaviour changes (ліміт растрів RemoteViews): https://developer.android.com/about/versions/17/behavior-changes-17
- Microsoft, How to build Android widgets with .NET MAUI: https://devblogs.microsoft.com/dotnet/how-to-build-android-widgets-with-dotnet-maui/
- Microsoft, How to build iOS widgets with .NET MAUI: https://devblogs.microsoft.com/dotnet/how-to-build-ios-widgets-with-dotnet-maui/
- .NET MAUI, Theming (AppThemeBinding, ResourceDictionary): https://learn.microsoft.com/en-us/dotnet/maui/user-interface/theming
- .NET MAUI, Material Design (UseMaterial3): https://learn.microsoft.com/en-us/dotnet/maui/user-interface/material-design
- .NET MAUI, Localization: https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/localization?view=net-maui-10.0
- .NET MAUI, Text-to-Speech: https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/device-media/text-to-speech
- Fixel (MacPaw, OFL 1.1): https://github.com/MacPaw/Fixel; веб-шрифти для прев’ю (перевірено HTTP 200, 2026-09-27): https://cdn.jsdelivr.net/gh/MacPaw/Fixel@main/fonts/webfonts/FixelText-Regular.woff2
- Lucide (ISC): https://github.com/lucide-icons/lucide; IconFont.Maui: https://github.com/jfversluis/IconFont.Maui.Template
- Syncfusion.Maui.Toolkit (MIT): https://www.syncfusion.com/net-maui-toolkit; CommunityToolkit.Maui 15.0.1: https://github.com/CommunityToolkit/Maui/releases/tag/15.0.1
- dotnet/skills, плагін dotnet-maui: https://github.com/dotnet/skills/tree/main/plugins/dotnet-maui
- davidortinau/maui-skills: https://github.com/davidortinau/maui-skills
- nextlevelbuilder/ui-ux-pro-max: https://github.com/nextlevelbuilder/ui-ux-pro-max-skill
- yctimlin/mcp_excalidraw: https://github.com/yctimlin/mcp_excalidraw
- Google Stitch SDK: https://github.com/google-labs-code/stitch-sdk
- Claude Design: https://support.claude.com/en/articles/14604416-get-started-with-claude-design
- Figma MCP rate limits: https://developers.figma.com/docs/figma-mcp-server/rate-limits-access/
- Material Theme Builder (архівовано): https://github.com/material-foundation/material-theme-builder
- mallibone/maui-ai-design-skill (без ліцензії): https://github.com/mallibone/maui-ai-design-skill
- Duolingo, Improving the streak (етичне обрамлення): https://blog.duolingo.com/improving-the-streak/
- Game Accessibility Guidelines: https://gameaccessibilityguidelines.com/full-list/
