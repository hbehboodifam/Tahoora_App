using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderManagementApi.Models;

namespace OrderManagementApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SurplusSaleController : ControllerBase
{
    private readonly AppDbContext _context;

    public SurplusSaleController(AppDbContext context)
    {
        _context = context;
    }

    // ============================================================
    // فروش از مازاد
    // ============================================================
    [HttpPost]
    public async Task<IActionResult> CreateSurplusSale([FromBody] SurplusSaleDto dto)
    {
        if (dto == null || dto.Items == null || !dto.Items.Any())
            return BadRequest("حداقل یک قلم باید ارسال شود.");

        // ===== ۱. تعیین مشتری =====
        Customer customer;

        if (dto.ExistingCustomerId.HasValue && dto.ExistingCustomerId.Value > 0)
        {
            var existing = await _context.Customers.FindAsync(dto.ExistingCustomerId.Value);
            if (existing == null)
                return BadRequest("مشتری انتخاب‌شده یافت نشد.");
            customer = existing;
        }
        else if (dto.NewCustomer != null && !string.IsNullOrWhiteSpace(dto.NewCustomer.FullName))
        {
            customer = new Customer
            {
                FullName = dto.NewCustomer.FullName,
                Phone = dto.NewCustomer.Phone,
                Email = dto.NewCustomer.Email,
                Address = dto.NewCustomer.Address
            };
            _context.Customers.Add(customer);
        }
        else
        {
            return BadRequest("یا مشتری موجود انتخاب کنید یا اطلاعات مشتری جدید را وارد کنید.");
        }

        // ===== ۲. pre-fetch محصولات و موجودی =====
        var productIds = dto.Items.Select(i => i.ProductId).Distinct().ToList();

        var products = await _context.Products
            .Where(p => productIds.Contains(p.ProductId))
            .ToDictionaryAsync(p => p.ProductId);

        var stocks = await _context.ProductStocks
            .Where(s => productIds.Contains(s.ProductId))
            .ToDictionaryAsync(s => s.ProductId);

        // ===== ۳. اعتبارسنجی موجودی =====
        foreach (var item in dto.Items)
        {
            if (!products.ContainsKey(item.ProductId))
                return BadRequest($"محصول با شناسه {item.ProductId} یافت نشد.");

            if (item.Quantity <= 0)
                return BadRequest("مقدار هر قلم باید مثبت باشد.");

            var stock = stocks.ContainsKey(item.ProductId) ? stocks[item.ProductId].Quantity : 0m;
            if (stock < item.Quantity)
            {
                var p = products[item.ProductId];
                return BadRequest(
                    $"موجودی کافی برای «{p.ProductName}» نیست. " +
                    $"موجودی فعلی: {stock} {p.Unit}، درخواستی: {item.Quantity} {p.Unit}");
            }
        }

        // ===== ۴. ساخت سفارش =====
        var order = new Order
        {
            Customer = customer,
            OrderDate = DateTime.Now,
            DeliveryDate = dto.DeliveryDate ?? DateTime.Now,
            IsPaid = dto.IsPaid,
            PaymentDate = dto.IsPaid ? DateTime.Now : null,
            Notes = dto.Notes,
            OrderSource = "Surplus",
            IsShipped = true,
            ShippedDate = DateTime.Now
        };

        decimal total = 0;
        foreach (var item in dto.Items)
        {
            var product = products[item.ProductId];
            var unitPrice = item.UnitPriceOverride ?? product.UnitPrice;
            var lineTotal = item.Quantity * unitPrice;
            total += lineTotal;

            order.OrderItems.Add(new OrderItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = unitPrice
            });
        }

        order.TotalAmount = total;
        _context.Orders.Add(order);

        // ===== ۵. کاهش موجودی =====
        foreach (var item in dto.Items)
        {
            var stock = stocks[item.ProductId];
            stock.Quantity -= item.Quantity;
            stock.LastUpdated = DateTime.Now;
        }

        // ===== ۶. ذخیره همه‌چیز در یه تراکنش =====
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "فروش از مازاد با موفقیت ثبت شد.",
            orderId = order.OrderId,
            customerId = customer.CustomerId,
            customerName = customer.FullName,
            totalAmount = order.TotalAmount,
            itemCount = order.OrderItems.Count
        });
    }
}

// ============================================================
// DTO ها
// ============================================================
public class SurplusSaleDto
{
    public int? ExistingCustomerId { get; set; }
    public NewCustomerDto? NewCustomer { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public bool IsPaid { get; set; } = false;
    public string? Notes { get; set; }
    public List<SurplusSaleItemDto> Items { get; set; } = new();
}

public class NewCustomerDto
{
    public string FullName { get; set; } = "";
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
}

public class SurplusSaleItemDto
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    /// <summary>اگه خالی باشه، از UnitPrice محصول استفاده می‌شه</summary>
    public decimal? UnitPriceOverride { get; set; }
}