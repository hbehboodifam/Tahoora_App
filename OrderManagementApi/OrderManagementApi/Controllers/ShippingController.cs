using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderManagementApi.Models;

namespace OrderManagementApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ShippingController : ControllerBase
{
    private readonly AppDbContext _context;

    public ShippingController(AppDbContext context)
    {
        _context = context;
    }

    // ============================================================
    // لیست سفارشات آماده‌ی ارسال
    // ============================================================
    [HttpGet("pending")]
    public async Task<IActionResult> GetPending(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] bool includeShipped = false)
    {
        var fromDate = from?.Date ?? DateTime.Today;
        var toEnd = (to?.Date ?? DateTime.Today).AddDays(1).AddSeconds(-1);

        var query = _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .Where(o => o.DeliveryDate >= fromDate && o.DeliveryDate <= toEnd);

        if (!includeShipped)
            query = query.Where(o => !o.IsShipped);

        var orders = await query
            .OrderBy(o => o.DeliveryDate)
            .ThenBy(o => o.OrderId)
            .ToListAsync();

        var orderList = orders.Select(o => new
        {
            o.OrderId,
            o.CustomerId,
            CustomerName = o.Customer != null ? o.Customer.FullName : "نامشخص",
            CustomerPhone = o.Customer != null ? o.Customer.Phone : null,
            o.DeliveryDate,
            o.OrderDate,
            o.TotalAmount,
            o.IsPaid,
            o.IsShipped,
            o.ShippedDate,
            o.Notes,
            ItemCount = o.OrderItems.Count,
            Items = o.OrderItems.Select(oi => new
            {
                oi.ProductId,
                ProductName = oi.Product != null ? oi.Product.ProductName : "نامشخص",
                Unit = oi.Product != null ? oi.Product.Unit : "",
                oi.Quantity,
                oi.UnitPrice,
                Total = oi.Quantity * oi.UnitPrice
            }).ToList()
        }).ToList();

        // ===== لیست بسته‌بندی (تجمیع محصولات) =====
        var shoppingList = orders
            .SelectMany(o => o.OrderItems)
            .GroupBy(oi => new
            {
                oi.ProductId,
                ProductName = oi.Product != null ? oi.Product.ProductName : "نامشخص",
                Unit = oi.Product != null ? oi.Product.Unit : ""
            })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.ProductName,
                g.Key.Unit,
                TotalQuantity = g.Sum(x => x.Quantity),
                OrderCount = g.Select(x => x.OrderId).Distinct().Count()
            })
            .OrderByDescending(x => x.TotalQuantity)
            .ToList();

        // ===== خلاصه =====
        var summary = new
        {
            totalOrders = orderList.Count,
            unshippedCount = orderList.Count(o => !o.IsShipped),
            shippedCount = orderList.Count(o => o.IsShipped),
            totalAmount = orderList.Sum(o => o.TotalAmount),
            unpaidAmount = orderList.Where(o => !o.IsPaid).Sum(o => o.TotalAmount),
            totalItems = orders.Sum(o => o.OrderItems.Count),
            hasData = orderList.Any()
        };

        return Ok(new
        {
            from = fromDate,
            to = toEnd,
            orders = orderList,
            shoppingList,
            summary
        });
    }

    // ============================================================
    // علامت‌گذاری به‌عنوان ارسال‌شده
    // ============================================================
    [HttpPost("mark-shipped/{orderId}")]
    public async Task<IActionResult> MarkShipped(int orderId)
    {
        var order = await _context.Orders.FindAsync(orderId);
        if (order == null) return NotFound("سفارش یافت نشد.");

        order.IsShipped = true;
        order.ShippedDate = DateTime.Now;
        await _context.SaveChangesAsync();

        return Ok(new { message = "سفارش به‌عنوان ارسال‌شده ثبت شد." });
    }

    [HttpPost("unmark-shipped/{orderId}")]
    public async Task<IActionResult> UnmarkShipped(int orderId)
    {
        var order = await _context.Orders.FindAsync(orderId);
        if (order == null) return NotFound("سفارش یافت نشد.");

        order.IsShipped = false;
        order.ShippedDate = null;
        await _context.SaveChangesAsync();

        return Ok(new { message = "علامت ارسال حذف شد." });
    }
}