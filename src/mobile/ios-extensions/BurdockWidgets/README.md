# BurdockWidgets: розширення WidgetKit

Віджет «Слово» для головного екрана та екрана блокування (спека 04 §3.1–3.5, дизайн 02 §3.12). Розширення є тонким презентером: читає готову шкалу з App Group `group.org.burdock.app`, яку пише застосунок (`AppGroupWidgetBridge`), і нічого не планує саме.

## Структура

| Шлях | Що це |
|---|---|
| `project.yml` | специфікація XcodeGen; `.xcodeproj`, `Info.plist` і `.entitlements` генеруються на CI й не комітяться |
| `BurdockShared/` | Swift-пакет без WidgetKit: моделі файлів App Group, вибір поточного запису за `now` і курсором, лічильник дня, усічення для екрана блокування; тести `swift test` |
| `BurdockWidgets/` | таргет розширення: `BurdockWidgetsBundle`, `WordWidget` (провайдер і фон), `Views/` (шість сімейств, тема, маскот-гліф), `Intents/NextWordIntent` (iOS 17+), `Resources/Localizable.xcstrings`, `PrivacyInfo.xcprivacy` |
| `BurdockWidgetGallery/` | налагоджувальний застосунок: рендерить усі сімейства у світлій і темній темі в PNG (`Documents/boards`) для перевірки на CI |
| `Shared/DesignTokens.swift` | згенеровано з `design/tokens/tokens.json` (`python design/tokens/generate.py --out all`) |
| `scripts/build-appex.sh` | збирає `.appex` у `build/Release-<sdk>`, звідки його бере `AdditionalAppExtensions` у `Burdock.Presentation.csproj` |
| `scripts/render-gallery.sh` | збирає галерею, запускає на симуляторі й копіює борди |

## Як це збирається

1. `xcodegen generate` → `BurdockWidgets.xcodeproj`.
2. `scripts/build-appex.sh iphonesimulator` (без підпису) або `iphoneos` з параметрами ручного підпису (сертифікат розповсюдження + профіль `Burdock Widgets App Store`).
3. `dotnet build|publish src/mobile/Burdock.Presentation -f net10.0-ios -p:BurdockWidgets=true …` вбудовує `.appex` у `Burdock.app/PlugIns/` і підписує застосунок з `Platforms/iOS/Entitlements.plist` (App Groups).

Без `-p:BurdockWidgets=true` застосунок збирається як раніше (без розширення та без entitlements), тому поточний профіль TestFlight лишається чинним, доки власник не ввімкне App Groups у порталі.

## Контракт даних

Файли `Timeline/word-timeline.v1.json`, `word-cursor.v1.json`, `extension-marker.v1.json` описані у спеці 04 §3.2; Swift-моделі у `BurdockShared/Sources/BurdockShared/WordTimeline.swift` дзеркалять `WidgetTimelineDocument` застосунку. Невідомий `schemaVersion` дає fallback «Реп’яшок спить», а не збій.

## Що не відтворюється на CI

Справжній хостинг WidgetKit (tinted/accented, StandBy, vibrant, бюджет оновлень) перевіряється лише на пристрої через TestFlight за чеклістом спеки 04 §5.4. Галерея показує ті самі view на градієнті теми та на темній плашці екрана блокування.
