namespace Burdock.Presentation.Navigation;

/// <summary>Navigation port for ViewModels; they never touch Shell, Page or Window (spec 01 FR-01-11).</summary>
public interface INavigationService
{
    Task GoToAsync(Route route, IReadOnlyDictionary<string, object>? parameters = null, CancellationToken cancellationToken = default);

    Task GoBackAsync(CancellationToken cancellationToken = default);

    Task ShowModalAsync(Route route, IReadOnlyDictionary<string, object>? parameters = null, CancellationToken cancellationToken = default);
}
