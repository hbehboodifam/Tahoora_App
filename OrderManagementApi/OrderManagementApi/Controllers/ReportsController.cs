using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderManagementApi.Models;
using OrderManagementApi.Services;

namespace OrderManagementApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly SmsService _smsService;

    public ReportsController(AppDbContext context, SmsService smsService)
    {
        _context = context;
        _smsService = smsService;
    }

    // ============================================================
    // ۱. گزارش تولید بر اساس تاریخ تحویل
    // ============================================================
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

    // ============================================================
    // ۲. گزارش فروش محصولات
    // ============================================================
    [HttpGet("product-sales")]
    public async Task<IActionResult> GetProductSalesReport(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        if (!from.HasValue || !to.HasValue)
            return BadRequest("بازه‌ی تاریخ الزامی است.");

        var fromDate = from.Value.Date;
        var toEnd = to.Value.Date.AddDays(1).AddSeconds(-1);

        var periodLength = (toEnd - fromDate).TotalSeconds;
        var prevTo = fromDate.AddSeconds(-1);
        var prevFrom = prevTo.AddSeconds(-periodLength);

        var currentItems = await _context.OrderItems
            .Include(oi => oi.Order)
            .Include(oi => oi.Product)
            .Where(oi => oi.Order.DeliveryDate >= fromDate && oi.Order.DeliveryDate <= toEnd)
            .ToListAsync();

        var previousItems = await _context.OrderItems
            .Include(oi => oi.Order)
            .Include(oi => oi.Product)
            .Where(oi => oi.Order.DeliveryDate >= prevFrom && oi.Order.DeliveryDate <= prevTo)
            .ToListAsync();

        if (!currentItems.Any())
        {
            return Ok(new
            {
                summary = new List<object>(),
                details = new List<object>(),
                totalRevenue = 0m,
                totalQuantity = 0m,
                distinctProducts = 0,
                topProduct = (string?)null,
                comparison = new List<object>(),
                hasData = false
            });
        }

        var totalRevenue = currentItems.Sum(i => i.Quantity * i.UnitPrice);
        var totalQuantity = currentItems.Sum(i => i.Quantity);

        var grouped = currentItems
            .GroupBy(i => new { i.ProductId, ProductName = i.Product != null ? i.Product.ProductName : "نامشخص", Unit = i.Product != null ? i.Product.Unit : "کیلوگرم" })
            .Select(g => new
            {
                productName = g.Key.ProductName,
                unit = g.Key.Unit,
                totalQuantity = g.Sum(i => i.Quantity),
                totalRevenue = g.Sum(i => i.Quantity * i.UnitPrice),
                averagePrice = g.Sum(i => i.Quantity) > 0
                    ? Math.Round(g.Sum(i => i.Quantity * i.UnitPrice) / g.Sum(i => i.Quantity), 0)
                    : 0,
                orderCount = g.Select(i => i.OrderId).Distinct().Count(),
                distinctCustomers = g.Select(i => i.Order.CustomerId).Distinct().Count()
            })
            .OrderByDescending(x => x.totalRevenue)
            .ToList();

        var summary = new List<object>();
        decimal cumulativePercent = 0;
        foreach (var item in grouped)
        {
            var percent = totalRevenue > 0 ? (double)(item.totalRevenue / totalRevenue * 100) : 0;
            cumulativePercent += (decimal)percent;

            summary.Add(new
            {
                item.productName,
                item.unit,
                item.totalQuantity,
                item.totalRevenue,
                item.averagePrice,
                item.orderCount,
                item.distinctCustomers,
                percentage = Math.Round(percent, 1),
                cumulativePercentage = Math.Round((double)cumulativePercent, 1)
            });
        }

        var details = currentItems
            .OrderBy(i => i.Order.DeliveryDate)
            .ThenBy(i => i.Product != null ? i.Product.ProductName : "")
            .Select(i => new
            {
                deliveryDate = i.Order.DeliveryDate,
                productName = i.Product != null ? i.Product.ProductName : "نامشخص",
                customerName = i.Order.Customer != null ? i.Order.Customer.FullName : "نامشخص",
                quantity = i.Quantity,
                unitPrice = i.UnitPrice,
                total = i.Quantity * i.UnitPrice
            })
            .ToList();

        var previousGrouped = previousItems
            .GroupBy(i => new { i.ProductId, ProductName = i.Product != null ? i.Product.ProductName : "نامشخص" })
            .Select(g => new
            {
                productName = g.Key.ProductName,
                totalRevenue = g.Sum(i => i.Quantity * i.UnitPrice)
            })
            .ToList();

        var comparison = grouped.Select(c =>
        {
            var prev = previousGrouped.FirstOrDefault(p => p.productName == c.productName);
            var prevRevenue = prev?.totalRevenue ?? 0;
            double changePercent = 0;
            string trend = "🆕 جدید";

            if (prevRevenue > 0)
            {
                changePercent = (double)((c.totalRevenue - prevRevenue) / prevRevenue * 100);
                trend = changePercent > 5 ? "🔼 رشد" : changePercent < -5 ? "🔽 افت" : "➡️ ثابت";
            }

            return new
            {
                productName = c.productName,
                currentRevenue = c.totalRevenue,
                previousRevenue = prevRevenue,
                changePercent = Math.Round(changePercent, 1),
                trend
            };
        })
        .OrderByDescending(x => x.currentRevenue)
        .ToList<object>();

        var topProduct = grouped.FirstOrDefault()?.productName;

        return Ok(new
        {
            summary,
            details,
            totalRevenue,
            totalQuantity,
            distinctProducts = grouped.Count,
            topProduct,
            comparison,
            hasData = true
        });
    }

    // ============================================================
    // ۳. گزارش عملکرد مشتریان (RFM)
    // ============================================================
    [HttpGet("customer-performance")]
    public async Task<IActionResult> GetCustomerPerformanceReport(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? category)
    {
        if (!from.HasValue || !to.HasValue)
            return BadRequest("بازه‌ی تاریخ الزامی است.");

        var fromDate = from.Value.Date;
        var toEnd = to.Value.Date.AddDays(1).AddSeconds(-1);

        var orders = await _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderItems)
            .Where(o => o.OrderDate >= fromDate && o.OrderDate <= toEnd)
            .ToListAsync();

        var allUnpaidOrders = await _context.Orders
            .Where(o => !o.IsPaid)
            .Select(o => new { o.CustomerId, o.TotalAmount })
            .ToListAsync();

        var debtByCustomer = allUnpaidOrders
            .GroupBy(o => o.CustomerId)
            .ToDictionary(g => g.Key, g => g.Sum(o => o.TotalAmount));

        if (!orders.Any())
        {
            return Ok(new
            {
                customers = new List<object>(),
                totalActiveCustomers = 0,
                totalRevenue = 0m,
                totalDebt = debtByCustomer.Values.Sum(),
                churnRiskCount = 0,
                hasData = false
            });
        }

        var rawData = orders
            .GroupBy(o => new { o.CustomerId, Name = o.Customer != null ? o.Customer.FullName : "نامشخص" })
            .Select(g => new
            {
                CustomerId = g.Key.CustomerId,
                CustomerName = g.Key.Name,
                OrderCount = g.Count(),
                TotalPurchase = g.Sum(o => o.TotalAmount),
                AverageOrderValue = g.Average(o => o.TotalAmount),
                DistinctProducts = g.SelectMany(o => o.OrderItems).Select(oi => oi.ProductId).Distinct().Count(),
                LastOrderDate = g.Max(o => o.OrderDate)
            })
            .ToList();

        var recencyValues = rawData.Select(r => (DateTime.Now - r.LastOrderDate).TotalDays).OrderBy(v => v).ToList();
        var frequencyValues = rawData.Select(r => (double)r.OrderCount).OrderBy(v => v).ToList();
        var monetaryValues = rawData.Select(r => (double)r.TotalPurchase).OrderBy(v => v).ToList();

        double recencyP33 = GetPercentile(recencyValues, 33);
        double recencyP66 = GetPercentile(recencyValues, 66);
        double freqP33 = GetPercentile(frequencyValues, 33);
        double freqP66 = GetPercentile(frequencyValues, 66);
        double monetaryP33 = GetPercentile(monetaryValues, 33);
        double monetaryP66 = GetPercentile(monetaryValues, 66);

        var customersList = rawData.Select(r =>
        {
            var recencyDays = (DateTime.Now - r.LastOrderDate).TotalDays;

            int rScore = recencyDays <= recencyP33 ? 3 : recencyDays <= recencyP66 ? 2 : 1;
            int fScore = r.OrderCount >= freqP66 ? 3 : r.OrderCount >= freqP33 ? 2 : 1;
            int mScore = (double)r.TotalPurchase >= monetaryP66 ? 3 : (double)r.TotalPurchase >= monetaryP33 ? 2 : 1;
            int totalScore = rScore + fScore + mScore;

            string cat = totalScore >= 8 ? "🏆 VIP"
                       : totalScore >= 6 ? "🟢 وفادار"
                       : totalScore >= 4 ? "🟡 معمولی"
                       : "🔴 در معرض ریزش";

            string catKey = totalScore >= 8 ? "VIP"
                          : totalScore >= 6 ? "Loyal"
                          : totalScore >= 4 ? "Normal"
                          : "AtRisk";

            return new
            {
                r.CustomerId,
                r.CustomerName,
                r.OrderCount,
                r.TotalPurchase,
                r.AverageOrderValue,
                r.DistinctProducts,
                r.LastOrderDate,
                DaysSinceLastOrder = (int)recencyDays,
                Debt = debtByCustomer.ContainsKey(r.CustomerId) ? debtByCustomer[r.CustomerId] : 0m,
                RScore = rScore,
                FScore = fScore,
                MScore = mScore,
                TotalScore = totalScore,
                Category = cat,
                CategoryKey = catKey
            };
        })
        .OrderByDescending(c => c.TotalPurchase)
        .ToList();

        if (!string.IsNullOrEmpty(category) && category != "All")
        {
            customersList = customersList.Where(c => c.CategoryKey == category).ToList();
        }

        return Ok(new
        {
            customers = customersList,
            totalActiveCustomers = customersList.Count,
            totalRevenue = customersList.Sum(c => c.TotalPurchase),
            totalDebt = debtByCustomer.Values.Sum(),
            churnRiskCount = customersList.Count(c => c.CategoryKey == "AtRisk"),
            hasData = true
        });
    }

    // ============================================================
    // ۴. ارسال پیامک یادآوری
    // ============================================================
    [HttpPost("send-reminder/{customerId}")]
    public async Task<IActionResult> SendReminderSms(int customerId)
    {
        try
        {
            var customer = await _context.Customers.FindAsync(customerId);
            if (customer == null) return NotFound("مشتری یافت نشد.");

            if (string.IsNullOrEmpty(customer.Phone))
                return BadRequest("شماره تلفن مشتری موجود نیست.");

            var lastOrder = await _context.Orders
                .Where(o => o.CustomerId == customerId)
                .OrderByDescending(o => o.OrderDate)
                .FirstOrDefaultAsync();

            if (lastOrder == null)
                return BadRequest("مشتری هیچ سفارشی ندارد.");

            var daysSince = (int)(DateTime.Now - lastOrder.OrderDate).TotalDays;

            var message = $"سلام {customer.FullName} عزیز،\n" +
                          $"{daysSince} روز از آخرین خرید شما می‌گذرد. " +
                          $"مشتاق دیدار مجدد شما هستیم!\n\n" +
                          $"تیم پشتیبانی";

            var result = await _smsService.SendSms(customer.Phone, message);

            if (result.Success)
            {
                _context.CustomerSmsLogs.Add(new Models.CustomerSmsLog
                {
                    CustomerId = customerId,
                    SentDate = DateTime.Now,
                    MessageText = message,
                    SmsType = "Reminder"
                });
                await _context.SaveChangesAsync();

                return Ok("پیامک ارسال شد.");
            }

            return StatusCode(500, $"خطا در ارسال: {result.ErrorMessage}");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"خطا: {ex.Message}");
        }
    }

    // ============================================================
    // متد کمکی: محاسبه‌ی صدک
    // ============================================================
    private static double GetPercentile(List<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0) return 0;
        if (sortedValues.Count == 1) return sortedValues[0];

        double rank = (percentile / 100.0) * (sortedValues.Count - 1);
        int lowerIndex = (int)Math.Floor(rank);
        int upperIndex = (int)Math.Ceiling(rank);

        if (lowerIndex == upperIndex)
            return sortedValues[lowerIndex];

        double weight = rank - lowerIndex;
        return sortedValues[lowerIndex] * (1 - weight) + sortedValues[upperIndex] * weight;
    }
}