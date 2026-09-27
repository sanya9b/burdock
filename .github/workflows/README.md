# GitHub Actions

Workflows будуть додані з появою каркаса рішення. Заплановано (специфікація 14):

| Workflow | Раннер | Призначення |
|---|---|---|
| ci-shared.yml | ubuntu | Domain/Application: збірка, тести, архітектурні тести, лінт локалізації |
| ci-android.yml | ubuntu | Збірка AAB, smoke на емуляторі |
| ci-ios.yml | macos-26 | xcodebuild розширення, збірка IPA, знімки симулятора, TestFlight |
| ci-server.yml | ubuntu | Збірка, Testcontainers, публікація образу в ghcr.io |
| ci-content.yml | ubuntu | Валідація контенту, збірка пакетів, публікація на тег |
| deploy-server.yml | ubuntu | Деплой на Hetzner через SSH з міграціями та health-check |
