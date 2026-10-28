# GitHub Actions

Воркфлоу репозиторію (спека 14). Репозиторій публічний, тому стандартні раннери, включно з macOS, безкоштовні.

| Workflow | Тригер | Раннер | Призначення |
|---|---|---|---|
| ci-server.yml | push/PR у `src/server`, `src/shared`, серверні тести | ubuntu | збірка сервера, Domain + серверні архітектурні, інтеграційні (Testcontainers PostgreSQL 18) і контрактні (OpenAPI-знімок) тести |
| ci-mobile-shared.yml | push/PR у спільні й мобільні шари | ubuntu + workload maui-android | токени дизайну, Domain/Application/Infrastructure/Presentation тести, архітектурні, end-to-end синхронізація двох інсталяцій проти API |
| ci-android.yml | push/PR у мобільний код | ubuntu | збірка Android (Release, debug key) |
| ci-ios.yml | push/PR у мобільний код | macos-26 | два паралельні завдання: тести `BurdockShared` (`swift test`), `.appex` для пристрою без підпису, рендер-борди галереї віджетів (артефакт `widget-render-boards`); збірка iOS для симулятора (Debug) з вбудованим розширенням (`-p:BurdockWidgets=true`) і перевіркою `PlugIns/BurdockWidgets.appex` |
| release-ios.yml | тег `mobile/v*`, вручну; `dry_run` без секретів; `widgets` або змінна `BURDOCK_WIDGETS` вмикають розширення | macos-26, environment `prod-ios` | підписана Release-збірка `ios-arm64`, IPA і dSYM, завантаження в TestFlight через App Store Connect API; з віджетами спершу `xcodebuild` підписує `.appex` профілем `APPLE_WIDGETS_PROVISIONING_PROFILE_BASE64`; runbook `docs/runbooks/release-ios.md` |
| publish-server.yml | push у `main` серверних шляхів | ubuntu | образ `ghcr.io/sanya9b/burdock-api` через `PublishContainer` |
| deploy-server.yml | вручну | ubuntu | деплой на Hetzner через SSH: міграції, compose, health-check |

Заплановано: `ci-content` (валідація контенту на зміни в `content/**`), `release-android` (AAB і Internal track).
