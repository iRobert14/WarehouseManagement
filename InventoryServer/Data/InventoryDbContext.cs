using InventoryServer.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace InventoryServer.Data;

public class InventoryDbContext : DbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ---------- users ----------
        modelBuilder.Entity<User>(user =>
        {
            user.ToTable("users");
            user.Property(u => u.Username).HasMaxLength(50).IsRequired();
            user.HasIndex(u => u.Username).IsUnique();
            user.Property(u => u.PasswordHash).HasMaxLength(256).IsRequired();
            user.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        });

        // ---------- products ----------
        modelBuilder.Entity<Product>(product =>
        {
            product.ToTable("products", table =>
            {
                table.HasCheckConstraint("CK_products_Quantity", "`Quantity` >= 0");
                table.HasCheckConstraint("CK_products_Price", "`Price` >= 0");
            });

            product.Property(p => p.Sku).HasMaxLength(40).IsRequired();
            product.HasIndex(p => p.Sku).IsUnique();
            product.Property(p => p.Name).HasMaxLength(120).IsRequired();
            product.Property(p => p.Category).HasMaxLength(60).IsRequired();
            product.Property(p => p.Size).HasMaxLength(10).IsRequired();
            product.Property(p => p.Color).HasMaxLength(30).IsRequired();
            product.Property(p => p.Price).HasPrecision(10, 2);
            product.HasIndex(p => p.Category);
        });

        // ---------- stock_movements ----------
        modelBuilder.Entity<StockMovement>(movement =>
        {
            movement.ToTable("stock_movements");
            movement.Property(m => m.Reason).HasMaxLength(200);

            movement.HasOne(m => m.Product)
                .WithMany(p => p.Movements)
                .HasForeignKey(m => m.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            movement.HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}