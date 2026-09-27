# src/server/

Каркас ще не створений. Планована структура (специфікація 09):

```
Burdock.Server.Api/                        хост: Minimal APIs, OpenAPI, версіонування /v1, композиція модулів
Burdock.Server.Shared/                     ядро: Result, помилки (ProblemDetails), IClock, outbox
Burdock.Server.Modules.Identity/           анонімні акаунти, токени (OpenIddict), прив'язка Apple/Google, видалення
Burdock.Server.Modules.Content/            маніфест, пакети, скарги на контент
Burdock.Server.Modules.LearnerProgress/    append-only події прогресу, курсор server_seq, проєкції статистики
Burdock.Server.Modules.SettingsSync/       налаштування LWW
Burdock.Server.Modules.Rewards/            ledger, звірка EconomyRules
Burdock.Server.Modules.Entitlements/       App Store Server API, Play Developer API, вебхуки
Burdock.Server.Modules.Notifications/      APNs для Live Activity (v1), push оновлень контенту (v1.1)
Burdock.Server.Modules.Analytics/          opt-in анонімні події: приймання, allowlist, агрегація, ретенція (13)
```

Кожен модуль має власну схему PostgreSQL і не читає таблиці інших модулів.
