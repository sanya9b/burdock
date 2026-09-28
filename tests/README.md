# tests/

Планована структура (специфікація 15):

```
Burdock.Domain.Tests/          xunit.v3: FSRS golden-тести, планувальник таймлайну, правила економіки (property-тести)
Burdock.Application.Tests/     use cases з фейковими портами й детермінованим IClock
Burdock.Infrastructure.Tests/  адаптери net10.0: golden-тести FSRS проти векторів py-fsrs 6.3.2 (fixtures/fsrs)
Burdock.Architecture.Tests/    правило залежностей шарів (блокує злиття)
Burdock.Server.Tests/          Testcontainers PostgreSQL, контрактні тести API
Burdock.Presentation.Tests/    ViewModel-и через нейтральний net10.0-head MAUI-проєкту: use cases над in-memory портами, фейки платформних сервісів
Burdock.UITests/               Appium smoke на емуляторі/симуляторі (CI)
```
