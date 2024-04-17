using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;

namespace Provider.Infrastructure.Data;

[ExcludeFromCodeCoverage]
public class ProviderContext(DbContextOptions<ProviderContext> options) : DbContext(options)
{
    public DbSet<Models.ProviderModel> Providers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
    }
}