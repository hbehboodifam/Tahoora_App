using Microsoft.EntityFrameworkCore;
using OrderManagementApi.Models;

namespace OrderManagementApi
{
    public class AppDbContext : DbContext
    {
        // ===== این سازنده را اضافه کنید =====
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<Product> Products { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<CustomerSmsLog> CustomerSmsLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // تنظیمات اضافی در صورت نیاز
            base.OnModelCreating(modelBuilder);
        }
    }
}