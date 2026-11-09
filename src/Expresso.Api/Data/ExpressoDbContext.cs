using Expresso.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Expresso.Api.Data;

public class ExpressoDbContext(DbContextOptions<ExpressoDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(product =>
        {
            product.Property(p => p.Name).HasMaxLength(80);
            product.Property(p => p.Price).HasPrecision(10, 2);

            // Enums stored as text so the data stays readable in the database.
            product.Property(p => p.Category).HasConversion<string>().HasMaxLength(20);
            product.Property(p => p.Size).HasConversion<string>().HasMaxLength(1);

            // One product per name and size; NULLS NOT DISTINCT also blocks two "Pão de queijo" without size.
            product.HasIndex(p => new { p.Name, p.Size }).IsUnique().AreNullsDistinct(false);
        });
    }
}
