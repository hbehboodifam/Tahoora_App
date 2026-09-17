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
    private readonly RfmCalculator _rfmCalculator;

    public ReportsController(AppDbContext context, SmsService smsService, RfmCalculator rfmCalculator)
    {
        _context = context;
        _smsService = smsService;
        _rfmCalculator = rfmCalculator;
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

        // تبدیل به RfmRawData
        var rawData = orders
            .GroupBy(o => new { o.CustomerId, Name = o.Customer != null ? o.Customer.FullName : "نامشخص" })
            .Select(g => new RfmRawData
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

        var customersList = _rfmCalculator.Calculate(rawData, debtByCustomer);

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
    // ۵. گزارش سود و زیان (P&L)
    // ============================================================
    [HttpGet("profit-loss")]
    public async Task<IActionResult> GetProfitLossReport(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        if (!from.HasValue || !to.HasValue)
            return BadRequest("بازه‌ی تاریخ الزامی است.");

        var fromDate = from.Value.Date;
        var toEnd = to.Value.Date.AddDays(1).AddSeconds(-1);

        // ===== فروش و COGS بر اساس تاریخ تحویل =====
        var orderItems = await _context.OrderItems
            .Include(oi => oi.Order)
            .Include(oi => oi.Product)
            .Where(oi => oi.Order.DeliveryDate >= fromDate && oi.Order.DeliveryDate <= toEnd)
            .ToListAsync();

        var totalSales = orderItems.Sum(i => i.Quantity * i.UnitPrice);
        var cogs = orderItems.Sum(i => i.Quantity * (i.Product != null ? i.Product.CostPrice : 0m));
        var grossProfit = totalSales - cogs;

        // ===== هزینه‌های دوره =====
        var expenses = await _context.Expenses
            .Include(e => e.Category)
            .Where(e => e.ExpenseDate >= fromDate && e.ExpenseDate <= toEnd)
            .ToListAsync();

        var totalExpenses = expenses.Sum(e => e.Amount);
        var netProfit = grossProfit - totalExpenses;

        // ===== درصدها =====
        var grossMarginPercent = totalSales > 0 ? Math.Round((double)(grossProfit / totalSales * 100), 1) : 0;
        var netMarginPercent   = totalSales > 0 ? Math.Round((double)(netProfit  / totalSales * 100), 1) : 0;

        // ===== تفکیک هزینه‌ها بر اساس دسته =====
        var expenseBreakdown = expenses
            .GroupBy(e => new
            {
                e.CategoryId,
                CategoryName = e.Category != null ? e.Category.CategoryName : "نامشخص"
            })
            .Select(g => new
            {
                categoryName = g.Key.CategoryName,
                amount = g.Sum(e => e.Amount),
                percentage = totalExpenses > 0
                    ? Math.Round((double)(g.Sum(e => e.Amount) / totalExpenses * 100), 1)
                    : 0
            })
            .OrderByDescending(x => x.amount)
            .ToList();

        // ===== تفکیک سود بر اساس محصول =====
        var productBreakdown = orderItems
            .GroupBy(i => new
            {
                i.ProductId,
                ProductName = i.Product != null ? i.Product.ProductName : "نامشخص"
            })
            .Select(g =>
            {
                var sales = g.Sum(i => i.Quantity * i.UnitPrice);
                var cost  = g.Sum(i => i.Quantity * (i.Product != null ? i.Product.CostPrice : 0m));
                var profit = sales - cost;
                return new
                {
                    productName = g.Key.ProductName,
                    quantity = g.Sum(i => i.Quantity),
                    sales,
                    cogs = cost,
                    profit,
                    marginPercent = sales > 0
                        ? Math.Round((double)(profit / sales * 100), 1)
                        : 0
                };
            })
            .OrderByDescending(x => x.profit)
            .ToList();

        return Ok(new
        {
            totalSales,
            cogs,
            grossProfit,
            totalExpenses,
            netProfit,
            grossMarginPercent,
            netMarginPercent,
            expenseBreakdown,
            productBreakdown,
            hasData = orderItems.Any() || expenses.Any()
        });
    }

}