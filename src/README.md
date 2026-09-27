# src/

| Тека | Вміст | Специфікація |
|---|---|---|
| `mobile/` | Мобільний застосунок .NET MAUI: `Burdock.Application`, `Burdock.Infrastructure`, `Burdock.Presentation`; Swift-розширення `ios-extensions/BurdockWidgets` | 01, 02-07, 04, 05 |
| `server/` | `Burdock.Server.Api` (хост) та модулі `Burdock.Server.Modules.*` | 09, 10 |
| `shared/` | `Burdock.Domain` (чисті правила, спільні для клієнта й сервера; T23) та `Burdock.Contracts` (DTO, версіоновані) | 01, 09 |

Правило залежностей шарів описане у `CLAUDE.md`, розділ 4, і перевіряється архітектурними тестами (див. `tests/`).
