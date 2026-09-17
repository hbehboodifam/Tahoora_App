using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderManagementApi.Models;
using OrderManagementApi.Services;

namespace OrderManagementApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductionController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly InventoryService _inventory;

    public ProductionController(AppDbContext context, InventoryService inventory)
    {
        _context = context;
        _inventory = inventory;
    }

    // ============================================================
    // ۱. برنامه‌ی تولید — جمع سفارش‌های این بازه به تفکیک محصول
    // ============================================================
        [HttpGet("plan")]
    public async Task<IActionResult> GetProductionPlan(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var fromDate = from?.Date ?? DateTime.Today;
        var toEnd = (to?.Date ?? DateTime.Today).AddDays(1).AddSeconds(-1);

        // ===== سفارش‌های این بازه (بر اساس DeliveryDate) =====
        var items = await _context.OrderItems
            .Include(oi => oi.Order)
            .Include(oi => oi.Product)
            .Where(oi => oi.Order.DeliveryDate >= fromDate && oi.Order.DeliveryDate <= toEnd)
            .ToListAsync();

        // ===== موجودی فعلی (لحظه‌ای، مستقل از بازه) =====
        var stocks = await _context.ProductStocks.ToDictionaryAsync(s => s.ProductId);

        var allProducts = await _context.Products
            .OrderBy(p => p.ProductName)
            .ToListAsync();

        // ===== نمای تجمعی =====
        var plannedByProduct = items
            .GroupBy(oi => oi.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

        var aggregated = allProducts
            .Select(p =>
            {
                var planned = plannedByProduct.ContainsKey(p.ProductId) ? plannedByProduct[p.ProductId] : 0m;
                var stock = stocks.ContainsKey(p.ProductId) ? stocks[p.ProductId].Quantity : 0m;
                var netRequired = planned - stock;
                if (netRequired < 0) netRequired = 0;

                return new
                {
                    p.ProductId,
                    p.ProductName,
                    p.Unit,
                    p.CostPrice,
                    PlannedQuantity = planned,
                    CurrentStock = stock,
                    NetRequired = netRequired,
                    HasOrder = planned > 0
                };
            })
            .Where(x => x.HasOrder || x.CurrentStock > 0)
            .ToList();

        // ===== نمای تفکیکی =====
        var byDate = items
            .GroupBy(oi => oi.Order.DeliveryDate.Date)
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                date = g.Key,
                entries = g
                    .GroupBy(oi => oi.ProductId)
                    .Select(pg =>
                    {
                        var product = pg.First().Product;
                        return new
                        {
                            productId = pg.Key,
                            productName = product?.ProductName ?? "نامشخص",
                            unit = product?.Unit ?? "",
                            plannedQuantity = pg.Sum(x => x.Quantity),
                            orderCount = pg.Select(x => x.OrderId).Distinct().Count()
                        };
                    })
                    .OrderBy(x => x.productName)
                    .ToList(),
                totalPlanned = g.Sum(x => x.Quantity),
                distinctProducts = g.Select(x => x.ProductId).Distinct().Count()
            })
            .ToList();

        return Ok(new
        {
            from = fromDate,
            to = toEnd,
            entries = aggregated,
            byDate,
            totalPlanned = plannedByProduct.Values.Sum(),
            distinctProducts = plannedByProduct.Count
        });
    }

    // ============================================================
    // ۲. ثبت تولید (چند محصول همزمان)
    // ============================================================
    [HttpPost]
    public async Task<IActionResult> RecordProduction([FromBody] RecordProductionDto dto)
    {
        if (dto == null || dto.Entries == null || !dto.Entries.Any())
            return BadRequest("حداقل یک قلم تولید باید ثبت شود.");

        var productionDate = dto.ProductionDate == default
            ? DateTime.Now
            : dto.ProductionDate;

        var savedLogs = new List<object>();

        foreach (var entry in dto.Entries)
        {
            if (entry.ActualProducedQuantity < 0)
                return BadRequest($"مقدار تولید محصول {entry.ProductId} نمی‌تواند منفی باشد.");

            var product = await _context.Products.FindAsync(entry.ProductId);
            if (product == null)
                return BadRequest($"محصول با شناسه {entry.ProductId} یافت نشد.");

            var log = new ProductionLog
            {
                ProductId = entry.ProductId,
                ProductionDate = productionDate,
                PlannedQuantity = entry.PlannedQuantity,
                ActualProducedQuantity = entry.ActualProducedQuantity,
                Notes = entry.Notes
            };
            _context.ProductionLogs.Add(log);

            // مازاد = تولید − سفارش این بازه
            var delta = entry.ActualProducedQuantity - entry.PlannedQuantity;
            await _inventory.AdjustStockAsync(entry.ProductId, delta);
            
            savedLogs.Add(new
            {
                ProductId = entry.ProductId,
                ProductName = product.ProductName,
                Planned = entry.PlannedQuantity,
                Produced = entry.ActualProducedQuantity,
                Surplus = entry.ActualProducedQuantity - entry.PlannedQuantity
            });
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "تولید با موفقیت ثبت شد.",
            productionDate,
            logs = savedLogs
        });
    }

    // ============================================================
    // ۳. تاریخچه‌ی تولید
    // ============================================================
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var query = _context.ProductionLogs
            .Include(p => p.Product)
            .AsQueryable();

        if (from.HasValue)
            query = query.Where(p => p.ProductionDate >= from.Value.Date);

        if (to.HasValue)
            query = query.Where(p => p.ProductionDate <= to.Value.Date.AddDays(1).AddSeconds(-1));

        var logs = await query
            .OrderByDescending(p => p.ProductionDate)
            .ThenBy(p => p.Product != null ? p.Product.ProductName : "")
            .Select(p => new
            {
                p.ProductionLogId,
                p.ProductionDate,
                p.ProductId,
                ProductName = p.Product != null ? p.Product.ProductName : "نامشخص",
                Unit = p.Product != null ? p.Product.Unit : "",
                p.PlannedQuantity,
                p.ActualProducedQuantity,
                Surplus = p.ActualProducedQuantity - p.PlannedQuantity,
                p.Notes
            })
            .ToListAsync();

        return Ok(new
        {
            logs,
            totalProduced = logs.Sum(l => l.ActualProducedQuantity),
            totalPlanned = logs.Sum(l => l.PlannedQuantity),
            totalSurplus = logs.Sum(l => l.Surplus),
            distinctProducts = logs.Select(l => l.ProductId).Distinct().Count()
        });
    }
}

// ============================================================
// DTO ها
// ============================================================
public class RecordProductionDto
{
    public DateTime ProductionDate { get; set; } = DateTime.Now;
    public List<ProductionEntryDto> Entries { get; set; } = new();
}

public class ProductionEntryDto
{
    public int ProductId { get; set; }
    public decimal PlannedQuantity { get; set; }
    public decimal ActualProducedQuantity { get; set; }
    public string? Notes { get; set; }
}