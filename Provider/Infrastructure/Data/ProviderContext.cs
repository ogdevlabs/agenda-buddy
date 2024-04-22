using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Provider.Models;

namespace Provider.Infrastructure.Data;

[ExcludeFromCodeCoverage]
public class ProviderContext(DbContextOptions<ProviderContext> options) : DbContext(options)
{
    public DbSet<ProviderModel>? Providers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProviderModel>()
            .HasIndex(u => new {u.FirstName, u.LastName, u.Email, u.Topic})
            .IsUnique();
    }
}