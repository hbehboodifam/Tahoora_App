using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderManagementApi.Models;

namespace OrderManagementApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _context;

    public DashboardController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var query = _context.Orders
            .Include(o => o.Customer)
            .AsQueryable();

        if (from.HasValue)
            query = query.Where(o => o.OrderDate >= from.Value);
        if (to.HasValue)
            query = query.Where(o => o.OrderDate <= to.Value);

        var totalOrders = await query.CountAsync();
        var totalRevenue = await query.SumAsync(o => o.TotalAmount);
        var totalCustomers = await _context.Customers.CountAsync();
        var pendingPayments = await query.CountAsync(o => !o.IsPaid);

        var recentOrders = await query
            .OrderByDescending(o => o.OrderDate)
            .Take(10)
            .Select(o => new
            {
                o.OrderId,
                CustomerName = o.Customer.FullName,
                o.OrderDate,
                o.TotalAmount,
                o.IsPaid
            })
            .ToListAsync();

        return Ok(new
        {
            TotalOrders = totalOrders,
            TotalRevenue = totalRevenue,
            TotalCustomers = totalCustomers,
            PendingPayments = pendingPayments,
            RecentOrders = recentOrders
        });
    }
    
        [HttpGet("charts")]
    public async Task<IActionResult> GetCharts([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var pc = new System.Globalization.PersianCalendar();

        // ===== ۱. فروش و سود ۶ ماه اخیر =====
        var today = DateTime.Today;
        var startOfCurrentMonth = new DateTime(today.Year, today.Month, 1);
        var sixMonthsAgo = startOfCurrentMonth.AddMonths(-5);

        var recentItems = await _context.OrderItems
            .Include(oi => oi.Order)
            .Include(oi => oi.Product)
            .Where(oi => oi.Order.DeliveryDate >= sixMonthsAgo)
            .ToListAsync();

        var monthlyData = new List<object>();
        for (int i = 0; i < 6; i++)
        {
            var monthStart = sixMonthsAgo.AddMonths(i);
            var monthEnd = monthStart.AddMonths(1).AddSeconds(-1);
            var items = recentItems
                .Where(oi => oi.Order.DeliveryDate >= monthStart && oi.Order.DeliveryDate <= monthEnd)
                .ToList();
            var sales = items.Sum(oi => oi.Quantity * oi.UnitPrice);
            var cogs  = items.Sum(oi => oi.Quantity * (oi.Product != null ? oi.Product.CostPrice : 0m));
            monthlyData.Add(new
            {
                monthLabel = $"{pc.GetYear(monthStart)}/{pc.GetMonth(monthStart):00}",
                sales,
                profit = sales - cogs
            });
        }

        // ===== ۲. پرفروش‌ترین محصولات (در بازه‌ی فیلتر) =====
        var itemsQuery = _context.OrderItems
            .Include(oi => oi.Order)
            .Include(oi => oi.Product)
            .AsQueryable();
        if (from.HasValue) itemsQuery = itemsQuery.Where(oi => oi.Order.DeliveryDate >= from.Value);
        if (to.HasValue)   itemsQuery = itemsQuery.Where(oi => oi.Order.DeliveryDate <= to.Value);

        var topProducts = (await itemsQuery.ToListAsync())
            .GroupBy(oi => oi.Product != null ? oi.Product.ProductName : "نامشخص")
            .Select(g => new
            {
                productName = g.Key,
                revenue = g.Sum(oi => oi.Quantity * oi.UnitPrice)
            })
            .OrderByDescending(x => x.revenue)
            .Take(5)
            .ToList();

        // ===== ۳. توزیع هزینه‌ها بر اساس دسته =====
        var expensesQuery = _context.Expenses.Include(e => e.Category).AsQueryable();
        if (from.HasValue) expensesQuery = expensesQuery.Where(e => e.ExpenseDate >= from.Value);
        if (to.HasValue)   expensesQuery = expensesQuery.Where(e => e.ExpenseDate <= to.Value);

        var expensesByCategory = (await expensesQuery.ToListAsync())
            .GroupBy(e => e.Category != null ? e.Category.CategoryName : "نامشخص")
            .Select(g => new
            {
                categoryName = g.Key,
                amount = g.Sum(e => e.Amount)
            })
            .OrderByDescending(x => x.amount)
            .ToList();

        return Ok(new { monthlyData, topProducts, expensesByCategory });
    }
}