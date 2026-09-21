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
        
        public DbSet<BackupLog> BackupLogs { get; set; }

        // ===== جدید: انبار و تولید =====
        public DbSet<ProductStock> ProductStocks { get; set; }
        public DbSet<ProductionLog> ProductionLogs { get; set; }
        public DbSet<WasteLog> WasteLogs { get; set; }

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

            // ===== Order: OrderSource =====
            modelBuilder.Entity<Order>()
                .Property(o => o.OrderSource)
                .HasMaxLength(20)
                .HasDefaultValue("Regular");
            
            modelBuilder.Entity<Order>()
                .Property(o => o.IsShipped)
                .HasDefaultValue(false);

            // ===== ProductStock =====
            modelBuilder.Entity<ProductStock>()
                .Property(s => s.Quantity)
                .HasColumnType("decimal(18,3)");

            modelBuilder.Entity<ProductStock>()
                .HasOne(s => s.Product)
                .WithOne()
                .HasForeignKey<ProductStock>(s => s.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            // ===== ProductionLog =====
            modelBuilder.Entity<ProductionLog>()
                .Property(p => p.PlannedQuantity)
                .HasColumnType("decimal(18,3)");
            modelBuilder.Entity<ProductionLog>()
                .Property(p => p.ActualProducedQuantity)
                .HasColumnType("decimal(18,3)");

            modelBuilder.Entity<ProductionLog>()
                .HasOne(p => p.Product)
                .WithMany()
                .HasForeignKey(p => p.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            // ===== WasteLog =====
            modelBuilder.Entity<WasteLog>()
                .Property(w => w.Quantity)
                .HasColumnType("decimal(18,3)");
            modelBuilder.Entity<WasteLog>()
                .Property(w => w.UnitCost)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<WasteLog>()
                .HasOne(w => w.Product)
                .WithMany()
                .HasForeignKey(w => w.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<BackupLog>()
                .Property(b => b.FilePath)
                .HasMaxLength(500);

            base.OnModelCreating(modelBuilder);
        }
    }
}