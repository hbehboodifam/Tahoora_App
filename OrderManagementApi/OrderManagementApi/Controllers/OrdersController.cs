using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderManagementApi.Models;

namespace OrderManagementApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _context;

    public OrdersController(AppDbContext context)
    {
        _context = context;
    }

    // دریافت لیست سفارشات به همراه مشتری و آیتم‌ها
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var orders = await _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.Product)
            .OrderByDescending(o => o.OrderDate)
            .Select(o => new
            {
                o.OrderId,
                CustomerName = o.Customer.FullName,
                o.OrderDate,
                o.DeliveryDate,
                o.TotalAmount,
                o.IsPaid,
                o.PaymentDate,
                o.Notes,
                OrderItems = o.OrderItems.Select(oi => new
                {
                    oi.ProductId,
                    ProductName = oi.Product.ProductName,
                    oi.Quantity,
                    oi.UnitPrice,
                    oi.TotalPrice
                })
            })
            .ToListAsync();

        return Ok(orders);
    }

    // دریافت یک سفارش با شناسه
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var order = await _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(o => o.OrderId == id);
        if (order == null) return NotFound();
        return Ok(order);
    }

    // ثبت سفارش جدید
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Order order)
    {
        order.OrderDate = DateTime.Now;
        order.TotalAmount = order.OrderItems.Sum(oi => oi.TotalPrice);
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = order.OrderId }, order);
    }

    // ویرایش سفارش
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Order order)
    {
        if (id != order.OrderId) return BadRequest();
        var existing = await _context.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.OrderId == id);
        if (existing == null) return NotFound();

        existing.CustomerId = order.CustomerId;
        existing.DeliveryDate = order.DeliveryDate;
        existing.IsPaid = order.IsPaid;
        existing.PaymentDate = order.PaymentDate;
        existing.Notes = order.Notes;

        _context.OrderItems.RemoveRange(existing.OrderItems);
        foreach (var item in order.OrderItems)
        {
            existing.OrderItems.Add(item);
        }
        existing.TotalAmount = existing.OrderItems.Sum(oi => oi.TotalPrice);

        await _context.SaveChangesAsync();
        return NoContent();
    }

    // حذف سفارش
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order == null) return NotFound();
        _context.Orders.Remove(order);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}