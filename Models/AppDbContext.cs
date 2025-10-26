using Microsoft.EntityFrameworkCore;

namespace CafeWeb.Models
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Product> Products { get; set; } = null!;
        public DbSet<Order> Orders { get; set; } = null!;
        public DbSet<OrderItem> OrderItems { get; set; } = null!;
        public DbSet<Payment> Payments { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Cấu hình quan hệ User - Order
            modelBuilder.Entity<Order>()
                .HasOne(o => o.Creator)
                .WithMany()
                .HasForeignKey(o => o.CreatedBy)
                .OnDelete(DeleteBehavior.SetNull);

            // Cấu hình quan hệ Order - OrderItem
            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Order)
                .WithMany(o => o.OrderItems)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // Cấu hình quan hệ Product - OrderItem
            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Product)
                .WithMany()
                .HasForeignKey(oi => oi.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            // Cấu hình quan hệ Order - Payment
            modelBuilder.Entity<Payment>()
                .HasOne(p => p.Order)
                .WithMany()
                .HasForeignKey(p => p.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // Cấu hình quan hệ User - Payment
            modelBuilder.Entity<Payment>()
                .HasOne(p => p.PaidByUser)
                .WithMany()
                .HasForeignKey(p => p.PaidBy)
                .OnDelete(DeleteBehavior.SetNull);

            // Đảm bảo order_id trong payments là unique
            modelBuilder.Entity<Payment>()
                .HasIndex(p => p.OrderId)
                .IsUnique();
        }
    }
}