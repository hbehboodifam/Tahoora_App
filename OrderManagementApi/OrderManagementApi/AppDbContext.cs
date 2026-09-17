using Microsoft.EntityFrameworkCore;
using OrderManagementApi.Models;

namespace OrderManagementApi
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<Product> Products { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<CustomerSmsLog> CustomerSmsLogs { get; set; }
        public DbSet<Expense> Expenses { get; set; }
        public DbSet<ExpenseCategory> ExpenseCategories { get; set; }
        public DbSet<CustomerNote> CustomerNotes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // ===== Product =====
            modelBuilder.Entity<Product>()
                .Property(p => p.UnitPrice).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Product>()
                .Property(p => p.MaterialCost).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Product>()
                .Property(p => p.PackagingCost).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Product>()
                .Property(p => p.LaborCost).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Product>()
                .Property(p => p.CostPrice).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Product>()
                .Property(p => p.WastePercent).HasColumnType("decimal(5,2)");
            modelBuilder.Entity<Product>()
                .Property(p => p.OverheadPercent).HasColumnType("decimal(5,2)");

            // ===== Expense =====
            modelBuilder.Entity<Expense>()
                .Property(e => e.Amount).HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Expense>()
                .HasOne(e => e.Category)
                .WithMany(c => c.Expenses)
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            
            // ===== CustomerNote =====
            modelBuilder.Entity<CustomerNote>()
                .HasOne(n => n.Customer)
                .WithMany()
                .HasForeignKey(n => n.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CustomerNote>()
                .Property(n => n.NoteText)
                .HasColumnType("text");

            base.OnModelCreating(modelBuilder);
        }
    }
}