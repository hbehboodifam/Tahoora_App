using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderManagementApi.Models;

namespace OrderManagementApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MigrationController : ControllerBase
{
    private readonly AppDbContext _context;

    public MigrationController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost("import")]
    public async Task<IActionResult> ImportData([FromBody] ImportDto data)
    {
        try
        {
            // ===== پاک کردن داده‌های قبلی =====
            _context.OrderItems.RemoveRange(_context.OrderItems);
            _context.Orders.RemoveRange(_context.Orders);
            _context.Products.RemoveRange(_context.Products);
            _context.Customers.RemoveRange(_context.Customers);
            await _context.SaveChangesAsync();

            // ===== درج مشتریان =====
            foreach (var c in data.Customers)
            {
                _context.Customers.Add(new Customer
                {
                    CustomerId = c.CustomerId,
                    FullName = c.FullName,
                    Phone = c.Phone,
                    Email = c.Email,
                    Address = c.Address
                });
            }
            await _context.SaveChangesAsync();

            // ===== درج محصولات =====
            foreach (var p in data.Products)
            {
                _context.Products.Add(new Product
                {
                    ProductId = p.ProductId,
                    ProductName = p.ProductName,
                    UnitPrice = p.UnitPrice,
                    Description = p.Description
                });
            }
            await _context.SaveChangesAsync();

            // ===== درج سفارشات =====
            foreach (var o in data.Orders)
            {
                _context.Orders.Add(new Order
                {
                    OrderId = o.OrderId,
                    OrderDate = o.OrderDate,
                    DeliveryDate = o.DeliveryDate,
                    CustomerId = o.CustomerId,
                    TotalAmount = o.TotalAmount,
                    IsPaid = o.IsPaid,
                    PaymentDate = o.PaymentDate,
                    Notes = o.Notes
                });
            }
            await _context.SaveChangesAsync();

            // ===== درج آیتم‌های سفارش =====
            foreach (var i in data.OrderItems)
            {
                _context.OrderItems.Add(new OrderItem
                {
                    OrderItemId = i.OrderItemId,
                    OrderId = i.OrderId,
                    ProductId = i.ProductId,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice
                });
            }
            await _context.SaveChangesAsync();

            // ===== به‌روزرسانی Sequence ها در PostgreSQL =====
            await _context.Database.ExecuteSqlRawAsync(
                "SELECT setval(pg_get_serial_sequence('\"Customers\"', 'CustomerId'), COALESCE(MAX(\"CustomerId\"), 1)) FROM \"Customers\"");
            await _context.Database.ExecuteSqlRawAsync(
                "SELECT setval(pg_get_serial_sequence('\"Products\"', 'ProductId'), COALESCE(MAX(\"ProductId\"), 1)) FROM \"Products\"");
            await _context.Database.ExecuteSqlRawAsync(
                "SELECT setval(pg_get_serial_sequence('\"Orders\"', 'OrderId'), COALESCE(MAX(\"OrderId\"), 1)) FROM \"Orders\"");
            await _context.Database.ExecuteSqlRawAsync(
                "SELECT setval(pg_get_serial_sequence('\"OrderItems\"', 'OrderItemId'), COALESCE(MAX(\"OrderItemId\"), 1)) FROM \"OrderItems\"");

            return Ok(new
            {
                success = true,
                customers = data.Customers.Count,
                products = data.Products.Count,
                orders = data.Orders.Count,
                orderItems = data.OrderItems.Count
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"خطا در Import: {ex.Message}");
        }
    }
}

// ===== DTOها =====
public class ImportDto
{
    public List<CustomerImport> Customers { get; set; } = new();
    public List<ProductImport> Products { get; set; } = new();
    public List<OrderImport> Orders { get; set; } = new();
    public List<OrderItemImport> OrderItems { get; set; } = new();
}

public class CustomerImport
{
    public int CustomerId { get; set; }
    public string FullName { get; set; } = "";
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
}

public class ProductImport
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public string? Description { get; set; }
}

public class OrderImport
{
    public int OrderId { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime DeliveryDate { get; set; }
    public int CustomerId { get; set; }
    public decimal TotalAmount { get; set; }
    public bool IsPaid { get; set; }
    public DateTime? PaymentDate { get; set; }
    public string? Notes { get; set; }
}

public class OrderItemImport
{
    public int OrderItemId { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}