using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderManagementApi.Models;

namespace OrderManagementApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ReportsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("production")]
    public async Task<IActionResult> GetProductionReport(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var query = _context.OrderItems
            .Include(oi => oi.Order)
                .ThenInclude(o => o.Customer)
            .Include(oi => oi.Product)
            .AsQueryable();

        if (from.HasValue)
            query = query.Where(oi => oi.Order.DeliveryDate >= from.Value.Date);

        if (to.HasValue)
        {
            var toEnd = to.Value.Date.AddDays(1).AddSeconds(-1);
            query = query.Where(oi => oi.Order.DeliveryDate <= toEnd);
        }

        var items = await query.ToListAsync();

        if (!items.Any())
        {
            return Ok(new
            {
                summary = new List<object>(),
                details = new List<object>(),
                totalOrders = 0,
                totalWeight = 0m,
                distinctProducts = 0
            });
        }

        // ===== خلاصه‌ی تولید بر اساس محصول =====
        var totalWeight = items.Sum(i => i.Quantity);

        var summary = items
            .GroupBy(i => new { i.ProductId, ProductName = i.Product != null ? i.Product.ProductName : "نامشخص" })
            .Select(g => new
            {
                productName = g.Key.ProductName,
                totalQuantity = g.Sum(i => i.Quantity),
                orderCount = g.Select(i => i.OrderId).Distinct().Count(),
                percentage = totalWeight > 0
                    ? Math.Round((double)(g.Sum(i => i.Quantity) / totalWeight * 100), 1)
                    : 0
            })
            .OrderByDescending(x => x.totalQuantity)
            .ToList();

        // ===== سفارشات تفصیلی: مرتب‌سازی تاریخ تحویل → محصول =====
        var details = items
            .OrderBy(i => i.Order.DeliveryDate)
            .ThenBy(i => i.Product != null ? i.Product.ProductName : "")
            .Select(i => new
            {
                deliveryDate = i.Order.DeliveryDate,
                productName = i.Product != null ? i.Product.ProductName : "نامشخص",
                customerName = i.Order.Customer != null ? i.Order.Customer.FullName : "نامشخص",
                quantity = i.Quantity
            })
            .ToList();

        return Ok(new
        {
            summary,
            details,
            totalOrders = items.Select(i => i.OrderId).Distinct().Count(),
            totalWeight,
            distinctProducts = summary.Count
        });
    }
}