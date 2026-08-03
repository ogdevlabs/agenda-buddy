using CommunityToolkit.Mvvm.ComponentModel;

namespace MobileApp.Models;

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

    [ObservableProperty]
    private bool _isExpanded;

    public string Initial => string.IsNullOrEmpty(FullName) ? "?" : FullName[0].ToString();
    public string SessionsLabel => IsProvider ? $"{TotalSessions} services" : $"{TotalSessions} sessions";
}
