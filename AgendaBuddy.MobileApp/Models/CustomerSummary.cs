using CommunityToolkit.Mvvm.ComponentModel;

namespace AgendaBuddy.MobileApp.Models;

public partial class CustomerSummary : ObservableObject
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string LastSession { get; set; } = string.Empty;
    public int TotalSessions { get; set; }
    public bool IsProvider { get; set; }
    public string Availability { get; set; } = string.Empty;

    /// <summary>Only meaningful when <see cref="IsProvider"/> — their own selected Professions.</summary>
    public List<string> Professions { get; set; } = new();

    [ObservableProperty]
    private bool _isExpanded;

    /// <summary>Only meaningful when <see cref="IsProvider"/> — whether the signed-in Customer is subscribed.</summary>
    [ObservableProperty]
    private bool _isSubscribed;

    [ObservableProperty]
    private bool _isBusy;

    public string Initial => string.IsNullOrEmpty(FullName) ? "?" : FullName[0].ToString();
    public string SessionsLabel => IsProvider ? $"{TotalSessions} services" : $"{TotalSessions} sessions";
}
