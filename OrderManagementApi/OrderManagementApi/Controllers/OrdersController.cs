using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderManagementApi.Models;
using OrderManagementApi.Services;
using System.Text;

namespace OrderManagementApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly SmsService _smsService;

    public OrdersController(AppDbContext context, SmsService smsService)
    {
        _context = context;
        _smsService = smsService;
    }

    // ===== دریافت همه سفارشات =====
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? customerId,
        [FromQuery] bool? isPaid)
    {
        var query = _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .AsQueryable();

        if (from.HasValue)
            query = query.Where(o => o.OrderDate >= from.Value);
        if (to.HasValue)
            query = query.Where(o => o.OrderDate <= to.Value);
        if (customerId.HasValue && customerId.Value > 0)
            query = query.Where(o => o.CustomerId == customerId.Value);
        if (isPaid.HasValue)
            query = query.Where(o => o.IsPaid == isPaid.Value);

        var orders = await query
            .OrderByDescending(o => o.OrderDate)
            .Select(o => new
            {
                o.OrderId,
                CustomerName = o.Customer != null ? o.Customer.FullName : "نامشخص",
                o.OrderDate,
                o.DeliveryDate,
                o.TotalAmount,
                o.IsPaid,
                o.PaymentDate,
                o.Notes
            })
            .ToListAsync();

        return Ok(orders);
    }

    // ===== دریافت یک سفارش با شناسه =====
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var order = await _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .Where(o => o.OrderId == id)
            .Select(o => new
            {
                o.OrderId,
                CustomerName = o.Customer != null ? o.Customer.FullName : "نامشخص",
                o.OrderDate,
                o.DeliveryDate,
                o.TotalAmount,
                o.IsPaid,
                o.PaymentDate,
                o.Notes,
                OrderItems = o.OrderItems.Select(oi => new
                {
                    oi.ProductId,
                    ProductName = oi.Product != null ? oi.Product.ProductName : "نامشخص",
                    oi.Quantity,
                    oi.UnitPrice,
                    TotalPrice = oi.Quantity * oi.UnitPrice
                })
            })
            .FirstOrDefaultAsync();

        if (order == null) return NotFound();
        return Ok(order);
    }

    // ===== ثبت سفارش جدید =====
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderDto orderDto)
    {
        if (orderDto == null)
            return BadRequest("داده ارسالی نامعتبر است.");

        if (orderDto.CustomerId <= 0)
            return BadRequest("مشتری نامعتبر است.");

        if (orderDto.OrderItems == null || !orderDto.OrderItems.Any())
            return BadRequest("سفارش باید حداقل یک محصول داشته باشد.");

        var order = new Order
        {
            CustomerId = orderDto.CustomerId,
            OrderDate = DateTime.Now,
            DeliveryDate = orderDto.DeliveryDate,
            IsPaid = orderDto.IsPaid,
            Notes = orderDto.Notes,
            OrderItems = orderDto.OrderItems.Select(item => new OrderItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice
            }).ToList()
        };

        order.TotalAmount = order.OrderItems.Sum(oi => oi.Quantity * oi.UnitPrice);

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        return Ok(order.OrderId);
    }

    // ===== ویرایش سفارش =====
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateOrderDto orderDto)
    {
        if (orderDto == null) return BadRequest("داده ارسالی نامعتبر است.");
        if (id != orderDto.OrderId) return BadRequest("شناسه سفارش مطابقت ندارد.");

        var existing = await _context.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.OrderId == id);

        if (existing == null) return NotFound();

        existing.CustomerId = orderDto.CustomerId;
        existing.DeliveryDate = orderDto.DeliveryDate;
        existing.IsPaid = orderDto.IsPaid;
        existing.Notes = orderDto.Notes;

        // حذف آیتم‌های قبلی
        _context.OrderItems.RemoveRange(existing.OrderItems);

        // اضافه کردن آیتم‌های جدید
        existing.OrderItems = orderDto.OrderItems.Select(item => new OrderItem
        {
            OrderId = id,
            ProductId = item.ProductId,
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice
        }).ToList();

        existing.TotalAmount = existing.OrderItems.Sum(oi => oi.Quantity * oi.UnitPrice);

        await _context.SaveChangesAsync();
        return NoContent();
    }

    // ===== حذف سفارش =====
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.OrderId == id);

        if (order == null) return NotFound();

        _context.OrderItems.RemoveRange(order.OrderItems);
        _context.Orders.Remove(order);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // ===== تغییر وضعیت پرداخت =====
    [HttpPut("{id}/toggle-payment")]
    public async Task<IActionResult> TogglePayment(int id, [FromBody] TogglePaymentDto dto)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order == null) return NotFound();

        order.IsPaid = dto.IsPaid;
        order.PaymentDate = dto.IsPaid ? DateTime.Now : (DateTime?)null;

        await _context.SaveChangesAsync();
        return Ok();
    }

    // ===== ارسال پیامک تأیید =====
    [HttpPost("{orderId}/send-confirmation")]
    public async Task<IActionResult> SendOrderConfirmation(int orderId)
    {
        try
        {
            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null) return NotFound("سفارش یافت نشد.");

            var phone = order.Customer?.Phone;
            if (string.IsNullOrEmpty(phone))
                return BadRequest("شماره تلفن مشتری موجود نیست.");

            var message = BuildOrderMessage(order);
            var result = await _smsService.SendSms(phone, message);

            if (result.Success)
                return Ok("پیامک با موفقیت ارسال شد.");
            else
                return StatusCode(500, $"خطا در ارسال پیامک: {result.ErrorMessage}");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"خطا در ارسال پیامک: {ex.Message}");
        }
    }

    // ===== ساخت متن پیامک =====
    private string BuildOrderMessage(Order order)
    {
        var sb = new StringBuilder();
        sb.AppendLine("با سلام و احترام،");
        sb.AppendLine();
        sb.AppendLine("سفارش شما با موفقیت در سیستم ثبت گردید.");
        sb.AppendLine("جزئیات سفارش به شرح زیر است:");
        sb.AppendLine();

        foreach (var item in order.OrderItems)
        {
            string quantityFormat = item.Quantity % 1 == 0 ? "N0" : "N2";
            string quantityText = item.Quantity.ToString(quantityFormat);
            decimal itemTotal = item.Quantity * item.UnitPrice;

            sb.AppendLine($"✅ {item.Product?.ProductName ?? "نامشخص"} : {quantityText} کیلوگرم - {itemTotal:N0} تومان");
        }

        sb.AppendLine();
        sb.AppendLine($"جمع کل سفارش: {order.TotalAmount:N0} تومان");
        sb.AppendLine($"تاریخ تحویل: {order.DeliveryDate:yyyy/MM/dd}");
        sb.AppendLine();
        sb.AppendLine("از اعتماد شما سپاسگزاریم.");
        sb.AppendLine("تیم پشتیبانی محصولات طهورا");
        sb.AppendLine("https://ble.ir/ProteinTahoora");

        return sb.ToString();
    }
}

// ===== DTOها =====
public class CreateOrderDto
{
    public int CustomerId { get; set; }
    public DateTime DeliveryDate { get; set; }
    public bool IsPaid { get; set; }
    public string? Notes { get; set; }
    public List<CreateOrderItemDto> OrderItems { get; set; } = new();
}

public class CreateOrderItemDto
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class UpdateOrderDto
{
    public int OrderId { get; set; }
    public int CustomerId { get; set; }
    public DateTime DeliveryDate { get; set; }
    public bool IsPaid { get; set; }
    public string? Notes { get; set; }
    public List<CreateOrderItemDto> OrderItems { get; set; } = new();
}

public class TogglePaymentDto
{
    public bool IsPaid { get; set; }
}