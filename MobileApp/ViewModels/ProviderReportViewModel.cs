using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgendaBuddy.Library.Entities;
using MobileApp.Infrastructure;
using MobileApp.Services;

namespace MobileApp.ViewModels;

/// <summary>
/// F-014's provider report route (api-contracts.md §2), never rendered anywhere in the client before
/// F-015-T11 — F-015-T07 only wired the API call (<see cref="IProviderApiService.GetReportAsync"/>).
/// The one requirement this ViewModel exists to satisfy is PRD Requirement 12 / AC13: never a number
/// or a blank field when revenue isn't computable — render the reason instead.
/// </summary>
public partial class ProviderReportViewModel : ObservableObject
{
    private readonly IProviderApiService _providerApiService;

    [ObservableProperty]
    private ProviderReport? _report;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool HasReport => Report is not null;

    /// <summary>
    /// ux-review.md finding 1 / PRD Requirement 12 / AC13: "Revenue isn't available yet —
    /// [revenueUnavailableReason]." — never a number, never blank. <see cref="ProviderReport"/> has no
    /// revenue *amount* field at all today (F-014 removed the only one that existed, ADR D-7), so the
    /// available branch below has nothing real to render yet; it exists so the property's shape does
    /// not have to change the day a real figure is added.
    /// </summary>
    public string RevenueMessage => Report switch
    {
        null => string.Empty,
        // The real reason text (Library/Services/ReportingService.RevenueUnavailable) already ends
        // with a period — TrimEnd avoids doubling it while still guaranteeing exactly one.
        { RevenueAvailable: false } =>
            $"Revenue isn't available yet — {Report.RevenueUnavailableReason?.TrimEnd('.')}.",
        _ => "Revenue is available."
    };

    public ProviderReportViewModel(IProviderApiService providerApiService)
    {
        _providerApiService = providerApiService;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var result = await _providerApiService.GetReportAsync();
            if (result is null)
                ErrorMessage = "Could not load your report — try again.";
            else
                Report = result;
        }
        catch (GatewayServiceUnavailableException ex)
        {
            ErrorMessage = GatewayErrorMapper.Describe(ex.FailedService);
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Could not load your report — check your connection and try again.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));

    partial void OnReportChanged(ProviderReport? value)
    {
        OnPropertyChanged(nameof(HasReport));
        OnPropertyChanged(nameof(RevenueMessage));
    }
}
