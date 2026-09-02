namespace AgendaBuddy.MobileApp.Models;

/// <summary>Minimal editable profile fields shared by Customer and Provider accounts.</summary>
public class ProfileInfo
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Optional contact number. The only fallback channel either party has when a session is about to be
    /// missed, which is why registration asks for it rather than leaving it to be discovered later.
    /// </summary>
    public string PhoneNumber { get; set; } = string.Empty;
}
