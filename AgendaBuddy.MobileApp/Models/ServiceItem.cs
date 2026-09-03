using CommunityToolkit.Mvvm.ComponentModel;
using AgendaBuddy.Library.Entities;

namespace AgendaBuddy.MobileApp.Models;

/// <summary>
/// Client-side shape for a provider's service catalogue entry. Deliberately carries no id — Services.Api
/// does not register <see cref="Library.Tools.ObjectIdJsonConverter"/> (agenda-buddy-do5), so
/// <c>ServiceEntity.Id</c> arrives as the broken multi-field BSON shape; PUT/PATCH match by
/// <see cref="Name"/> server-side, so no id is needed for either write.
/// </summary>
public partial class ServiceItem : ObservableObject
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal? Fee { get; set; }
    public FeeType FeeType { get; set; } = FeeType.Fixed;
    public bool IsActive { get; set; } = true;

    /// <summary>The session length this service books for, in minutes. Optional server-side.</summary>
    public int? DurationMinutes { get; set; }

    /// <summary>The provider's Profession this service is offered under. Required on creation
    /// (AddServicesToProviderCommandHandler rejects a service naming no profession/one the provider
    /// doesn't have); null only on services created before this field existed.</summary>
    public string? ProfessionName { get; set; }

    [ObservableProperty]
    private bool _isEditing;

    /// <summary>Chosen in the booking flow. Display state only — the catalogue itself has no notion of it.</summary>
    [ObservableProperty]
    private bool _isSelected;

    public string FeeLabel => Fee is null ? "No fee set" : $"{Fee:C} ({FeeType})";

    public string DurationLabel => DurationMinutes is null ? "No duration set" : $"{DurationMinutes} min";
}
