# infra/ — інфраструктура «Реп'яха»

Тут живе все, що потрібно, щоб підняти сервер «Реп'яха» на Hetzner Cloud (Q17: ЄС, Docker Compose, PostgreSQL, Caddy) і супроводжувати його. Політики й обґрунтування описані в специфікаціях; цей README лише пояснює, що і де лежить.

Джерела істини: [09-backend](../docs/specs/09-backend.md) (§3.12–3.13: топологія, конфігурація, бекапи, масштабування), [10-security-privacy](../docs/specs/10-security-privacy.md) (§3.4: секрети, хостові контролі, ротація ключів, ранбук інцидентів), 14-devops-ci (пайплайни деплою).

## Що тут буде (планована структура)

```text
infra/
  README.md                     # цей файл
  compose/
    compose.yaml                # базові сервіси: caddy, api, postgres, pgbackrest
    compose.prod.yaml           # override для prod: образи за digest, ліміти ресурсів, політики логів
    compose.staging.yaml        # override для staging: sandbox-середовища магазинів, APNs sandbox
    compose.local.yaml          # локально: лише postgres (+ опційно api зі свіжої збірки)
  caddy/
    Caddyfile                   # api.burdock.org, api.staging.burdock.org; TLS, заголовки безпеки, /health/live
  postgres/
    Dockerfile                  # FROM postgres:18.<minor>@sha256:… + pgbackrest
    conf.d/                     # archive_command, shared_buffers, log_statement, max_connections
    init/                       # 00-roles.sql (burdock_app, burdock_ro, burdock_admin), 01-schemas.sql
  pgbackrest/
    pgbackrest.conf             # repo1 = Hetzner Object Storage (інша локація), aes-256-cbc, retention full=5 / diff=14
    crontab                     # full: неділя 02:00 UTC; diff: щодня 02:00; check: щогодини
  secrets/
    .sops.yaml                  # правила шифрування: age-одержувачі окремо для staging і prod
    prod.env.enc                # усі секрети prod (див. 10 §3.4.6), лише зашифровано
    staging.env.enc
    README.md                   # як додати, прочитати, ротувати секрет
  scripts/
    bootstrap-vm.sh             # користувач deploy, Docker, chrony, unattended-upgrades, fail2ban, каталоги
    deploy.sh                   # cosign verify → pull за digest → `api migrate` → up -d → healthcheck → rollback
    migrate.sh                  # окремий запуск міграцій (expand/contract) і CREATE INDEX CONCURRENTLY
    backup-verify.sh            # `pgbackrest info` + алерт при віці бекапу > 36 год (`alert.backupMaxAgeHours`, 13 §3.14)
    restore-drill.sh            # щомісячний тест відновлення у staging із фіксацією RTO
    check-rotations.sh          # перевірка дат ротації ключів за розкладом 10 §3.4.13
    chaos/                      # kill-postgres.sh, apns-stub-5xx.sh, clock-skew.sh, fill-disk.sh
  hetzner/
    server.md                   # тип VM, локація, томи, Hetzner Backups; порядок створення через hcloud CLI
    firewall.md                 # правила Hetzner Cloud Firewall: 80/443 звідусіль, 22 лише з allowlist
    object-storage.md           # бакети: burdock-backups-<loc>, burdock-content-<loc>; політики доступу
  cdn/
    bunny.md                    # pull zone cdn.burdock.org → origin Object Storage; кеш; прапорець token auth
```

Terraform/Kubernetes у v1 не використовуються (одна VM, один compose-проєкт). Якщо з'явиться друга VM (крок 2–3 шляху масштабування в 09 §3.13), інфраструктурний код переноситься в `hetzner/` як `hcloud`-скрипти або Terraform окремим ADR.

## Середовища

| Середовище | Де | Домен API | Магазини / APNs | Секрети | Деплой |
|---|---|---|---|---|---|
| `local` | робоча станція розробника | `http://localhost:8080` | заглушки (WireMock) і Testcontainers | `dotnet user-secrets`, одноразові паролі Testcontainers | `docker compose -f compose/compose.yaml -f compose/compose.local.yaml up` |
| `staging` | окрема мала VM Hetzner у тій самій локації (припущення; альтернатива — той самий хост в іншому compose-проєкті, відхилена заради ізоляції prod) | `api.staging.burdock.org` | App Store Sandbox, Play testing track, APNs sandbox | `secrets/staging.env.enc` | автоматично з `main` через GitHub Environment `staging` |
| `prod` | VM Hetzner Falkenstein або Helsinki (локація — відкрите питання 09 §7) | `api.burdock.org` | production (prod також приймає sandbox-транзакції для App Review) | `secrets/prod.env.enc` | лише з тегу `v*` через GitHub Environment `prod` з обов'язковим підтвердженням власника |

Правила іменування: compose-проєкт `burdock-<env>`, контейнери `burdock-<env>-<service>`, томи `burdock-<env>-pgdata`, `burdock-<env>-dpkeys`, `burdock-<env>-caddy`; бакети `burdock-<purpose>-<location>`; змінні середовища застосунку з префіксом `Burdock__` (наприклад `Burdock__Auth__AccessTokenLifetime`), змінна `ASPNETCORE_ENVIRONMENT` ∈ {`Development`, `Staging`, `Production`}.

## Мінімальна топологія запуску

```mermaid
flowchart LR
  subgraph Internet
    App[Застосунок iOS/Android]
    Mon[Зовнішній монітор /health/live]
    GH[GitHub Actions: збірка, cosign, deploy по SSH]
    Owner[Ноутбук власника: SSH + тунель psql]
  end
  subgraph VM["Hetzner Cloud VM (ЄС) · Hetzner Firewall: 80/443 всім, 22 allowlist"]
    subgraph Compose["docker compose: burdock-prod"]
      Caddy[caddy :80/:443<br/>TLS, заголовки]
      Api[api :8080<br/>non-root, read-only rootfs]
      PG[(postgres:18<br/>том pgdata)]
      BR[pgbackrest cron<br/>WAL-архів, full/diff]
    end
    Age[/etc/burdock/age.key]
  end
  subgraph EU["Сторонні сервіси в ЄС"]
    OS[(Hetzner Object Storage, інша локація:<br/>бекапи · пакети контенту)]
    CDN[Bunny CDN cdn.burdock.org]
    Obs[Grafana Cloud EU · Sentry EU]
  end
  subgraph External["Необхідні зовнішні процесори"]
    APNS[APNs]
    Stores[App Store Server API / Play Developer API / Pub/Sub]
    IdP[Apple ID · Google]
  end

  App -->|HTTPS| Caddy --> Api --> PG
  App -->|пакети| CDN --> OS
  PG --> BR -->|S3, AES-256| OS
  Api -->|OTLP, винятки без PII| Obs
  Api <--> APNS
  Api <--> Stores
  Api <--> IdP
  Mon --> Caddy
  GH -->|ssh deploy| VM
  Owner -->|ssh| VM
  Age -.->|sops -d при деплої| Api
```

Мережа: назовні відкриті лише 80/443 (Caddy) і 22 (SSH, allowlist). `api` і `postgres` не публікують портів; `/health/ready` доступний лише всередині docker-мережі. Kestrel слухає HTTP на 8080 лише в docker-мережі; TLS термінує Caddy.

## Потік деплою (коротко)

1. CI збирає `dotnet publish /t:PublishContainer`, публікує образ у GHCR, підписує cosign (keyless), фіксує digest у артефакті релізу.
2. `deploy.sh` на VM (через SSH з GitHub Environment): перевіряє підпис і digest → `docker compose pull` → `docker compose run --rm api migrate` (expand-крок) → `docker compose up -d api` → чекає `/health/ready` → при невдачі повертає попередній digest.
3. Contract-крок міграції (видалення старих колонок) виконується лише в наступному релізі (09 NFR-09-7).
4. Секрети: `sops -d secrets/prod.env.enc` у `tmpfs`, передаються compose через `env_file`, файл видаляється після старту.

## Бекапи і відновлення

- pgBackRest: безперервний WAL-архів, повний бекап щотижня, диференційний щодня, репозиторій зашифрований (`aes-256-cbc`), 5 повних бекапів ≈ 35 днів (число публікується користувачам у політиці приватності).
- Hetzner Backups VM як другий рівень (без томів).
- `restore-drill.sh` щомісяця відновлює останній бекап у staging і запускає smoke-тести; ціль RTO ≤ 2 год, RPO ≤ 5 хв (09 NFR-09-4). Результат записується у `docs/runbooks/restore-drill-log.md`.

## Чого тут немає

- Секретів у відкритому вигляді (тільки `*.enc`; push protection GitHub блокує випадкові коміти).
- Конфігурації застосунку, що є політикою (ліміти, TTL): вона в `appsettings.*.json` сервера і в таблиці констант 09 §3.15; тут лише інфраструктурні значення.
- Скриптів для Azure: міграція на Azure Container Apps + PostgreSQL Flexible Server (Q17) можлива без зміни коду, але оформлюється окремим ADR, коли буде потрібна.
