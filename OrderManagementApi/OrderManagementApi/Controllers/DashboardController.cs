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
}