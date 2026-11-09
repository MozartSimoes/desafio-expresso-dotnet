using Expresso.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Expresso.Api.Data;

public class ExpressoDbContext(DbContextOptions<ExpressoDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();

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

        modelBuilder.Entity<Order>(order =>
        {
            order.Property(o => o.CustomerName).HasMaxLength(60);
            order.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
            order.Property(o => o.Total).HasPrecision(10, 2);
            order.HasIndex(o => new { o.Status, o.CreatedAt });

            // Items are reachable only through the order, which controls how they are added.
            order.HasMany(o => o.Items).WithOne().HasForeignKey("OrderId").OnDelete(DeleteBehavior.Cascade);
            order.Navigation(o => o.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<OrderItem>(item =>
        {
            item.Property(i => i.ProductName).HasMaxLength(80);
            item.Property(i => i.Size).HasConversion<string>().HasMaxLength(1);
            item.Property(i => i.UnitPrice).HasPrecision(10, 2);
            item.Property(i => i.Notes).HasMaxLength(140);
            item.Ignore(i => i.Subtotal);

            // A product that was ever sold cannot be deleted; it is marked unavailable instead.
            item.HasOne<Product>().WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
