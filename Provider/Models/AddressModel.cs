using System.Diagnostics.CodeAnalysis;

namespace Provider.Models;

[ExcludeFromCodeCoverage]
public class AddressModel
{
    public int Id { get; set; }
    public required string AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public required string City { get; set; }
    public required string State { get; set; }
    public required string ZipCode { get; set; }
}