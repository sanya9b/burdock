using Burdock.Application.UseCases.Content;
using Burdock.Presentation.Resources.Strings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace Burdock.Presentation.ViewModels;

/// <summary>S-01 Bootstrap (spec 03): prepares the local content before the Shell appears; the page owns the animation and the hand-over.</summary>
public sealed partial class BootstrapViewModel(BootstrapContentUseCase bootstrap, ILogger<BootstrapViewModel> logger) : ObservableObject
{
    [ObservableProperty]
    private string _statusText = AppResources.Bootstrap_Preparing;

    [ObservableProperty]
    private bool _isBusy = true;

    [ObservableProperty]
    private bool _hasError;

    /// <summary>Raised on the UI thread once the content is ready.</summary>
    public event EventHandler? Completed;

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        HasError = false;
        IsBusy = true;
        StatusText = AppResources.Bootstrap_Preparing;
        try
        {
            await bootstrap.ExecuteAsync(cancellationToken).ConfigureAwait(true);
            IsBusy = false;
            Completed?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException)
        {
            IsBusy = false;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogFailed(ex);
            IsBusy = false;
            HasError = true;
            StatusText = AppResources.Bootstrap_Error;
        }
    }

    [RelayCommand]
    private Task RetryAsync(CancellationToken cancellationToken) => RunAsync(cancellationToken);

    [LoggerMessage(Level = LogLevel.Error, Message = "Bootstrap failed")]
    private partial void LogFailed(Exception exception);
}
