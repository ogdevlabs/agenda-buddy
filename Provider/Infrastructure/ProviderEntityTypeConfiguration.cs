using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Provider.Infrastructure;

public class ProviderEntityTypeConfiguration : IEntityTypeConfiguration<Models.Provider>
{
    public void Configure(EntityTypeBuilder<Models.Provider> builder)
    {
        builder.ToTable("Provider");

        builder.Property(ci => ci.FirstName)
            .IsRequired(true)
            .HasMaxLength(50);

        builder.Property(ci => ci.LastName)
            .IsRequired(true)
            .HasMaxLength(50);

        builder.Property(ci => ci.Email)
            .IsRequired()
            .HasMaxLength(150);

        builder.HasIndex(ci => ci.Email)
            .HasDatabaseName("Provider_email")
            .IsUnique();

        builder.HasOne(ci => ci.Customers)
            .WithMany();
    }
}