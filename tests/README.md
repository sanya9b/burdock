# tests/

Планована структура (специфікація 15):

```
Burdock.Domain.Tests/          xunit.v3: FSRS golden-тести, планувальник таймлайну, правила економіки (property-тести)
Burdock.Application.Tests/     use cases з фейковими портами й детермінованим IClock
Burdock.Architecture.Tests/    правило залежностей шарів (блокує злиття)
Burdock.Server.Tests/          Testcontainers PostgreSQL, контрактні тести API
Burdock.Presentation.Tests/    снапшоти станів компонентів, лінт локалізації
Burdock.UITests/               Appium smoke на емуляторі/симуляторі (CI)
```
