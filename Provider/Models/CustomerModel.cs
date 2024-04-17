using System.ComponentModel.DataAnnotations;

namespace Provider.Models;

public class CustomerModel
{
    public int Id { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    [EmailAddress]
    public required string Email { get; set; }
    [Phone]
    public string? Phone { get; set; }
}