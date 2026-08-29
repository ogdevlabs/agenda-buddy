namespace AgendaBuddy.MobileApp.Models;

/// <summary>Minimal editable profile fields shared by Customer and Provider accounts.</summary>
public class ProfileInfo
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}
