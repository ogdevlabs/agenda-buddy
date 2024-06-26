using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace Customer.Models;

[ExcludeFromCodeCoverage]
public class Customer
{
    public int Id { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }

    [EmailAddress] public required string Email { get; set; }

    [Phone] public string? Phone { get; set; }
}