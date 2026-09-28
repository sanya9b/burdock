using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;

namespace Burdock.Presentation.Services;

/// <summary>Native action sheet for short pick lists (level, category); returns the chosen option or null when cancelled.</summary>
public interface IChoiceSheet
{
    Task<string?> ShowAsync(string title, string cancel, IReadOnlyList<string> options, CancellationToken cancellationToken);
}

/// <summary>Toast / Snackbar component (spec 02 section 3.11): short confirmations such as «Збережено».</summary>
public interface IToastService
{
    Task ShowAsync(string message, CancellationToken cancellationToken);
}

public sealed class ShellChoiceSheet : IChoiceSheet
{
    public async Task<string?> ShowAsync(string title, string cancel, IReadOnlyList<string> options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        var windows = Microsoft.Maui.Controls.Application.Current?.Windows;
        var page = Shell.Current?.CurrentPage ?? (windows is { Count: > 0 } ? windows[0].Page : null);
        if (page is null)
        {
            return null;
        }

        var picked = await page.DisplayActionSheetAsync(title, cancel, null, options.ToArray()).ConfigureAwait(true);
        return picked is null || string.Equals(picked, cancel, StringComparison.Ordinal) ? null : picked;
    }
}

public sealed class CommunityToast : IToastService
{
    public Task ShowAsync(string message, CancellationToken cancellationToken) => Toast.Make(message, ToastDuration.Short).Show(cancellationToken);
}
