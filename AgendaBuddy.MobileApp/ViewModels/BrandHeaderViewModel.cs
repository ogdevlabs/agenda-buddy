using CommunityToolkit.Mvvm.ComponentModel;
using AgendaBuddy.MobileApp.Services;

namespace AgendaBuddy.MobileApp.ViewModels;

/// <summary>
/// Who is signed in, for the brand header's second line.
/// </summary>
/// <remarks>
/// <para>
/// A singleton, so the name is fetched once per account rather than on every navigation — the header is
/// on every page, and a per-page profile call would be a round trip per tap.
/// </para>
/// <para>
/// The JWT carries only the email and role, so the name has to come from the profile API. The email is
/// what renders while that call is in flight and what stays if it never succeeds: the header decorates
/// every page, so it must always show something rather than a blank or the word "null".
/// </para>
/// <para>
/// This deliberately does not live on <see cref="IUserSessionService"/>, which the API services already
/// depend on — putting profile calls there would make the dependency circular.
/// </para>
/// </remarks>
public partial class BrandHeaderViewModel : ObservableObject
{
    private readonly IUserSessionService _session;
    private readonly IProviderApiService _providerApiService;
    private readonly ICustomerApiService _customerApiService;

    /// <summary>The account <see cref="DisplayName"/> was successfully resolved for, so it is not refetched.</summary>
    private string _resolvedFor = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _roleLabel = string.Empty;

    public bool HasUser => !string.IsNullOrEmpty(DisplayName);

    public bool HasRole => !string.IsNullOrEmpty(RoleLabel);

    public BrandHeaderViewModel(
        IUserSessionService session,
        IProviderApiService providerApiService,
        ICustomerApiService customerApiService)
    {
        _session = session;
        _providerApiService = providerApiService;
        _customerApiService = customerApiService;
    }

    /// <summary>
    /// Brings the header in line with the current session. Idempotent and cheap once the name is known;
    /// a signed-out session clears it, and a different account refetches.
    /// </summary>
    public async Task RefreshAsync(CancellationToken ct = default)
    {
        if (!await TryRefreshSessionAsync())
        {
            Clear();
            return;
        }

        var email = _session.Email;
        if (string.IsNullOrEmpty(email))
        {
            Clear();
            return;
        }

        RoleLabel = FormatRole(_session.Role);

        if (string.Equals(_resolvedFor, email, StringComparison.OrdinalIgnoreCase))
            return;

        if (string.IsNullOrEmpty(DisplayName) || !string.Equals(DisplayName, email, StringComparison.OrdinalIgnoreCase))
            DisplayName = email;

        var name = await FetchNameAsync(email, ct);
        if (string.IsNullOrWhiteSpace(name))
            return;

        // Only remember the account once a name actually arrived, so an offline first attempt retries.
        DisplayName = name;
        _resolvedFor = email;
    }

    private void Clear()
    {
        _resolvedFor = string.Empty;
        DisplayName = string.Empty;
        RoleLabel = string.Empty;
    }

    private async Task<bool> TryRefreshSessionAsync()
    {
        try
        {
            await _session.RefreshAsync();
            return true;
        }
        catch (Exception)
        {
            // A token that cannot be decoded is indistinguishable from signed out as far as the header goes.
            return false;
        }
    }

    private async Task<string?> FetchNameAsync(string email, CancellationToken ct)
    {
        try
        {
            var profile = _session.IsProvider
                ? await _providerApiService.GetProfileAsync(email, ct)
                : _session.IsCustomer
                    ? await _customerApiService.GetProfileAsync(email, ct)
                    : null;

            if (profile is null)
                return null;

            var name = $"{profile.FirstName} {profile.LastName}".Trim();
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }
        catch (Exception)
        {
            // Any failure at all falls back to the email. A decorative header must never break the page
            // it sits on, and accounts with no profile row are a known state (agenda-buddy-fg5).
            return null;
        }
    }

    private static string FormatRole(string role) => role.ToLowerInvariant() switch
    {
        "provider" => "Provider",
        "customer" => "Customer",
        _ => string.Empty,
    };

    partial void OnDisplayNameChanged(string value) => OnPropertyChanged(nameof(HasUser));

    partial void OnRoleLabelChanged(string value) => OnPropertyChanged(nameof(HasRole));
}
