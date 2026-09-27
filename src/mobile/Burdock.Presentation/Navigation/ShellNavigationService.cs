namespace Burdock.Presentation.Navigation;

/// <summary>Shell-backed navigation. Parameters travel through <see cref="IQueryAttributable"/> on the target page or ViewModel.</summary>
public sealed class ShellNavigationService : INavigationService
{
    private static readonly IReadOnlyDictionary<string, object> NoParameters = new Dictionary<string, object>(StringComparer.Ordinal);

    public Task GoToAsync(Route route, IReadOnlyDictionary<string, object>? parameters = null, CancellationToken cancellationToken = default)
        => Navigate(route.Path, parameters, cancellationToken);

    public Task GoBackAsync(CancellationToken cancellationToken = default)
        => Navigate("..", null, cancellationToken);

    public Task ShowModalAsync(Route route, IReadOnlyDictionary<string, object>? parameters = null, CancellationToken cancellationToken = default)
    {
        // Modal presentation is declared per page (Shell.PresentationMode="ModalAnimated"); the route alone is enough.
        return Navigate(route.Path, parameters, cancellationToken);
    }

    private static Task Navigate(string path, IReadOnlyDictionary<string, object>? parameters, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var shell = Shell.Current ?? throw new InvalidOperationException("Shell is not ready; navigation is only available after the window is created.");
        var animate = !ReduceMotion.IsEnabled;
        return shell.Dispatcher.DispatchAsync(() =>
            shell.GoToAsync(new ShellNavigationState(path), animate, new Dictionary<string, object>(parameters ?? NoParameters, StringComparer.Ordinal)));
    }
}

/// <summary>Accessibility switch: custom transitions are disabled when the OS asks to reduce motion (spec 02 section 3.9).</summary>
internal static class ReduceMotion
{
    // Platform detection is wired in Platforms/ in phase 1; until then animations stay on.
    public static bool IsEnabled { get; set; }
}
