# Runbook: реліз iOS у TestFlight

Воркфлоу: `.github/workflows/release-ios.yml`. Вимоги: спека 14 FR-14-5, §3.6 («release-ios»), §3.7 (секрети). Mac не потрібен: сертифікати готуються через OpenSSL на Windows, збирає й завантажує GitHub Actions на macOS-раннері.

## Передумови

- Членство в Apple Developer Program активне; Apple Developer Agreement і Paid Apps Agreement в App Store Connect прийняті (інакше завантаження відхиляється з помилкою про угоди).
- Bundle ID `org.burdock.app` зареєстровано в Certificates, Identifiers & Profiles → Identifiers (тип App; додаткові можливості поки не потрібні; App Groups і Push додаються у фазі 3 для віджетів).
- Запис застосунку створено в App Store Connect → My Apps → + (Name «Реп’ях», Primary Language Ukrainian, Bundle ID `org.burdock.app`, SKU `burdock-ios`). Без запису `altool` відповідає `Cannot determine the Apple ID from Bundle ID`, хоча збірка й підпис уже пройшли; через API запис створити не можна.
- Git Bash на Windows (у ньому є `openssl` і `base64`).

## Крок 1. Сертифікат Apple Distribution без Mac (разово, діє рік)

У Git Bash, у порожній теці поза репозиторієм:

```
openssl genrsa -out apple-distribution.key 2048
openssl req -new -key apple-distribution.key -out apple-distribution.csr -subj "/CN=Burdock Apple Distribution/O=<ім’я або ФОП>/C=UA"
```

1. developer.apple.com → Certificates → + → **Apple Distribution** → завантажити `apple-distribution.csr` → Download `distribution.cer`.
2. Зібрати `.p12` (прапорець `-legacy` потрібен, бо keychain macOS не читає типовий формат OpenSSL 3):

```
openssl x509 -in distribution.cer -inform DER -out apple-distribution.pem
openssl pkcs12 -export -legacy -inkey apple-distribution.key -in apple-distribution.pem -name "Apple Distribution" -out apple-distribution.p12
```

Пароль, який попросить `pkcs12`, стане секретом `APPLE_DISTRIBUTION_P12_PASSWORD`. Файли `.key` і `.p12` зберігай офлайн (менеджер паролів): втрата означає новий сертифікат, це не катастрофа, але попередні профілі доведеться перевипустити.

## Крок 2. Provisioning profile (при зміні entitlements або сертифіката)

developer.apple.com → Profiles → + → **App Store Connect** (Distribution) → App ID `org.burdock.app` → сертифікат із кроку 1 → ім’я, наприклад `Burdock App Store` → Download `Burdock_App_Store.mobileprovision`.

## Крок 3. Ключ App Store Connect API (разово)

App Store Connect → Users and Access → Integrations → App Store Connect API → Team Keys → + → ім’я `github-release-ios`, роль **App Manager** → Download `AuthKey_XXXXXXXXXX.p8` (завантажується лише один раз). Запиши **Key ID** (у назві файла) та **Issuer ID** (вгорі сторінки).

## Крок 4. Секрети в GitHub

GitHub → repo → Settings → Environments → New environment `prod-ios` → **Required reviewers**: власник (це і є ручне підтвердження з FR-14-5). Secrets середовища:

| Секрет | Значення |
|---|---|
| `APPLE_DISTRIBUTION_P12_BASE64` | `base64 -w0 apple-distribution.p12` |
| `APPLE_DISTRIBUTION_P12_PASSWORD` | пароль із кроку 1 |
| `APPLE_PROVISIONING_PROFILE_BASE64` | `base64 -w0 Burdock_App_Store.mobileprovision` |
| `APP_STORE_CONNECT_API_KEY_BASE64` | `base64 -w0 AuthKey_XXXXXXXXXX.p8` |
| `APP_STORE_CONNECT_KEY_ID` | Key ID |
| `APP_STORE_CONNECT_ISSUER_ID` | Issuer ID |

Нічого з цього не потрапляє в репозиторій (CLAUDE.md §6, §9). Воркфлоу видаляє тимчасовий keychain, профіль і `.p8` у кроці `if: always()`.

## Крок 5. Запуск

Два способи:

- **Тег** (основний): `git tag mobile/v0.1.0 && git push origin mobile/v0.1.0`. Маркетингова версія береться з тегу, номер збірки = номер запуску воркфлоу (монотонно зростає, тому повторний запуск того самого тегу дає нову збірку).
- **Вручну**: Actions → release-ios → Run workflow; `display_version` за потреби, `dry_run` вимкнено.

Після старту GitHub чекає на підтвердження середовища `prod-ios` (кнопка Review deployments; потрібно бути залогіненим як рецензент; з командного рядка те саме робить `gh api -X POST repos/sanya9b/burdock/actions/runs/<run>/pending_deployments` зі `state: approved`). Далі ≈ 35–50 хв: воркloads, AOT-збірка, підпис, `altool --validate-app`, `altool --upload-app`.

**Сухий прогін.** `dry_run = true` збирає Release для `ios-arm64` без підпису й нічого не завантажує; секрети й підтвердження не потрібні. Використовуй після змін у залежностях чи лінкуванні, щоб зловити проблеми AOT до витрачання підтвердження.

## Перевірка результату

1. Крок «Upload to TestFlight» завершився без помилок; у логах `altool` є `No errors uploading`.
2. App Store Connect → My Apps → Реп’ях → TestFlight → iOS Builds: збірка у стані Processing (5–20 хв), потім Ready to Test. Питання про експортне шифрування не з’являється, бо в Info.plist стоїть `ITSAppUsesNonExemptEncryption = false` (лише HTTPS).
3. Internal Testing → група → додати тестувальників (до 100, без Beta App Review). На телефоні застосунок TestFlight → Install.
4. Артефакти запуску: `.ipa` і `.app.dSYM` (30 днів) для символізації збоїв, поки Sentry не підключено (журнал рішень, п. 8).

## Відкат

- Збірку в TestFlight не видаляють, а завершують: App Store Connect → збірка → Expire Build. Тестувальники лишаються на попередній.
- Повернутися до попередньої версії: перезапустити воркфлоу на старому тегу (`Run workflow` з гілки/тегу) або створити новий тег із попереднього коміту; номер збірки все одно буде більшим.

## Час виконання

Крок 1–4 разово ≈ 1 година. Один реліз ≈ 15 хв CI (виміряно 27.10.2026: підписана збірка з AOT і завантаження) + 5–20 хв обробки Apple.

## Типові помилки

| Симптом | Причина | Дія |
|---|---|---|
| `No Apple Distribution identity in the imported .p12` | `.p12` без приватного ключа або зібраний без `-legacy` | перезібрати `.p12` за кроком 1 |
| `MAC verification failed during PKCS12 import (wrong password?)` | `.p12` у форматі OpenSSL 3 (PBES2/AES-256, MAC sha256): keychain macOS його не читає, навіть якщо пароль правильний | перепакувати: `openssl pkcs12 -in old.p12 -nodes -out bundle.pem`, потім `openssl pkcs12 -export -legacy -inkey bundle.pem -in bundle.pem -certfile AppleWWDRCAG3.pem -out legacy.p12`; оновити секрет |
| `Cannot determine the Apple ID from Bundle ID 'org.burdock.app'` | в App Store Connect немає запису застосунку з цим bundle ID | створити запис (передумови), потім `Re-run failed jobs` і підтвердити деплой |
| `Provisioning profile … doesn't include signing certificate` | профіль створено під інший сертифікат | перевипустити профіль (крок 2) і оновити секрет |
| `ITMS-90xxx` від `altool` | найчастіше іконка, версія або entitlements | текст помилки точно називає ключ; номер збірки має бути більшим за попередній для тієї ж версії |
| `You must first sign the relevant contracts online` | неприйняті угоди в App Store Connect | Agreements, Tax, and Banking → прийняти |
| `Xcode 26.6 not found` | образ раннера змінився | підняти `XCODE_PREFERRED` і `WORKLOAD_SET` разом у `release-ios.yml` і `ci-ios.yml` |
| Збірка падає в AOT/лінкуванні | нова залежність із рефлексією | спершу сухий прогін; далі `TrimmerRootAssembly` або `MtouchLink=None` для діагностики |
| Застосунок падає на старті на пристрої, у симуляторі ні | обрізання коду, відсутні ресурси | crash-лог у TestFlight → символізувати через dSYM з артефактів |

Запасний шлях завантаження, якщо `altool` перестане приймати IPA: на раннері є fastlane, `fastlane pilot upload --ipa <ipa> --api_key_path key.json --skip_waiting_for_build_processing`, де `key.json` = `{ "key_id", "issuer_id", "key" (вміст .p8) }`.

## Коли ескалювати

- Три поспіль невдалі завантаження з різними кодами ITMS → зупинитись, зібрати логи, звірити зі спекою 14 §3.6, не крутити секрети наосліп.
- Підозра на витік секрету (лог із ключем, сторонній доступ до середовища) → відкликати ключ API і сертифікат в Apple, перевипустити, оновити секрети; спека 10 §3.6.
