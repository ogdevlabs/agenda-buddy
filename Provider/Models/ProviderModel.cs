using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;


namespace Provider.Models;

[ExcludeFromCodeCoverage]
public record ProviderModel
{
    public int Id { get; init; }
    [Required]
    public string FirstName { get; init; }
    [Required]
    public string LastName { get; init; }
    [EmailAddress]
    public required string Email { get; set; }
    [Phone]
    public string? Phone { get; set; }
    [Required]
    public string Topic { get; set; }
    public Address? AddressInformation { get; set; }
    public List<Customer>? Customers { get; set; }
}

