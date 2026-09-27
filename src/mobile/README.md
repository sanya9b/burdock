# src/mobile/

Каркас рішення ще не створений. Планована структура (специфікація 01):

```
(Burdock.Domain живе у src/shared/, див. T23)
Burdock.Application/      use cases, порти (IClock, ILexemeRepository, IWidgetBridge, INotificationScheduler, ISyncGateway…)
Burdock.Infrastructure/   SQLite (sqlite-net-pcl), HTTP (IHttpClientFactory + resilience), SecureStorage, платформові адаптери
Burdock.Presentation/     MAUI: XAML, ViewModels (CommunityToolkit.Mvvm), навігація, Resources/Styles (згенеровані з design/tokens)
ios-extensions/
  BurdockWidgets/         Xcode-проєкт: WidgetKit (усі сімейства) + ActivityKit (сесія); читає таймлайн з App Group group.org.burdock.app
```

Локально на Windows запускається лише Android-емулятор. iOS збирається у GitHub Actions (специфікація 14).
