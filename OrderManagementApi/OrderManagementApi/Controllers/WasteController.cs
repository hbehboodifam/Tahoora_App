using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderManagementApi.Models;

namespace OrderManagementApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WasteController : ControllerBase
{
    private readonly AppDbContext _context;

    public WasteController(AppDbContext context)
    {
        _context = context;
    }

    // ============================================================
    // ثبت ضایعات
    // ============================================================
    [HttpPost]
    public async Task<IActionResult> RecordWaste([FromBody] RecordWasteDto dto)
    {
        if (dto == null)
            return BadRequest("داده‌ای ارسال نشد.");

        if (dto.Quantity <= 0)
            return BadRequest("مقدار ضایعات باید مثبت باشد.");

        var product = await _context.Products.FindAsync(dto.ProductId);
        if (product == null)
            return BadRequest("محصول یافت نشد.");

        // ===== چک موجودی =====
        var stock = await _context.ProductStocks
            .FirstOrDefaultAsync(s => s.ProductId == dto.ProductId);

        var currentStock = stock?.Quantity ?? 0m;
        if (currentStock < dto.Quantity)
            return BadRequest(
                $"موجودی کافی برای ثبت ضایعات نیست. موجودی فعلی: {currentStock} {product.Unit}، " +
                $"درخواستی: {dto.Quantity} {product.Unit}");

        // ===== ثبت WasteLog با UnitCost قفل‌شده =====
        var waste = new WasteLog
        {
            ProductId = dto.ProductId,
            Quantity = dto.Quantity,
            UnitCost = product.CostPrice,   // ← قفل کردن عدد در لحظه
            WasteDate = dto.WasteDate ?? DateTime.Now,
            Reason = dto.Reason,
            Notes = dto.Notes
        };

        _context.WasteLogs.Add(waste);

        // ===== کاهش موجودی =====
        stock!.Quantity -= dto.Quantity;
        stock.LastUpdated = DateTime.Now;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "ضایعات با موفقیت ثبت شد.",
            wasteLogId = waste.WasteLogId,
            productName = product.ProductName,
            quantity = waste.Quantity,
            unitCost = waste.UnitCost,
            totalCost = waste.Quantity * waste.UnitCost,
            reason = waste.Reason,
            remainingStock = stock.Quantity
        });
    }

    // ============================================================
    // تاریخچه‌ی ضایعات
    // ============================================================
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? productId)
    {
        var query = _context.WasteLogs
            .Include(w => w.Product)
            .AsQueryable();

        if (from.HasValue)
            query = query.Where(w => w.WasteDate >= from.Value.Date);

        if (to.HasValue)
            query = query.Where(w => w.WasteDate <= to.Value.Date.AddDays(1).AddSeconds(-1));

        if (productId.HasValue)
            query = query.Where(w => w.ProductId == productId.Value);

        var rawLogs = await query
            .OrderByDescending(w => w.WasteDate)
            .ToListAsync();

        var logs = rawLogs.Select(w => new
        {
            w.WasteLogId,
            w.WasteDate,
            w.ProductId,
            ProductName = w.Product != null ? w.Product.ProductName : "نامشخص",
            Unit = w.Product != null ? w.Product.Unit : "",
            w.Quantity,
            w.UnitCost,
            TotalCost = w.Quantity * w.UnitCost,
            w.Reason,
            w.Notes
        }).ToList();

        // ===== تفکیک بر اساس دلیل =====
        var byReason = rawLogs
            .GroupBy(w => string.IsNullOrEmpty(w.Reason) ? "نامشخص" : w.Reason)
            .Select(g => new
            {
                reason = g.Key,
                count = g.Count(),
                totalQuantity = g.Sum(x => x.Quantity),
                totalCost = g.Sum(x => x.Quantity * x.UnitCost)
            })
            .OrderByDescending(x => x.totalCost)
            .ToList();

        return Ok(new
        {
            logs,
            totalWasteCost = rawLogs.Sum(w => w.Quantity * w.UnitCost),
            totalWasteQuantity = rawLogs.Sum(w => w.Quantity),
            recordCount = rawLogs.Count,
            byReason
        });
    }

    // ============================================================
    // حذف ضایعات (اشتباه ثبت شده)
    // ============================================================
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var waste = await _context.WasteLogs.FindAsync(id);
        if (waste == null) return NotFound();

        // برگرداندن مقدار به موجودی
        var stock = await _context.ProductStocks
            .FirstOrDefaultAsync(s => s.ProductId == waste.ProductId);

        if (stock != null)
        {
            stock.Quantity += waste.Quantity;
            stock.LastUpdated = DateTime.Now;
        }

        _context.WasteLogs.Remove(waste);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}

// ============================================================
// DTO
// ============================================================
public class RecordWasteDto
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public string? Reason { get; set; }
    public string? Notes { get; set; }
    public DateTime? WasteDate { get; set; }
}