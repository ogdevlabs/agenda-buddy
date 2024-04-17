using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace Provider.Models;

[ExcludeFromCodeCoverage]
public record Provider
{
    public int Id { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    [EmailAddress]
    public required string Email { get; set; }
    [Phone]
    public string? Phone { get; set; }
    public Address? AddressInformation { get; set; }
    public List<Customer.Models.Customer>? Customers { get; set; }
}

