using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderManagementApi.Models;
using OrderManagementApi.Services;

namespace OrderManagementApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly RfmCalculator _rfmCalculator;

    public CustomersController(AppDbContext context, RfmCalculator rfmCalculator)
    {
        _context = context;
        _rfmCalculator = rfmCalculator;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search)
    {
        var query = _context.Customers.AsQueryable();
        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(c => c.FullName.Contains(search) || c.Phone.Contains(search));
        }
        var customers = await query.OrderBy(c => c.FullName).ToListAsync();
        return Ok(customers);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer == null) return NotFound();
        return Ok(customer);
    }

    // ============================================================
    // پروفایل ۳۶۰ درجه‌ی مشتری
    // ============================================================
    [HttpGet("{id}/profile")]
    public async Task<IActionResult> GetProfile(int id)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer == null) return NotFound("مشتری یافت نشد.");

        // ===== سفارشات =====
        var orders = await _context.Orders
            .Where(o => o.CustomerId == id)
            .Include(o => o.OrderItems)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();

        // ===== پیامک‌ها =====
        var smsLogs = await _context.CustomerSmsLogs
            .Where(s => s.CustomerId == id)
            .OrderByDescending(s => s.SentDate)
            .ToListAsync();

        // ===== یادداشت‌ها =====
        var notes = await _context.CustomerNotes
            .Where(n => n.CustomerId == id)
            .OrderByDescending(n => n.CreatedDate)
            .ToListAsync();

        // ===== RFM: محاسبه برای کل مشتریان، بعد فیلتر =====
        var allOrders = await _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderItems)
            .ToListAsync();

        var debtByCustomer = allOrders
            .Where(o => !o.IsPaid)
            .GroupBy(o => o.CustomerId)
            .ToDictionary(g => g.Key, g => g.Sum(o => o.TotalAmount));

        var rawData = allOrders
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

        var rfmList = _rfmCalculator.Calculate(rawData, debtByCustomer);
        var rfm = rfmList.FirstOrDefault(r => r.CustomerId == id);

        // ===== آمار خلاصه =====
        var totalOrders = orders.Count;
        var totalPurchases = orders.Sum(o => o.TotalAmount);
        var debt = debtByCustomer.ContainsKey(id) ? debtByCustomer[id] : 0m;
        var daysSinceLastOrder = orders.Any()
            ? (int)(DateTime.Now - orders.Max(o => o.OrderDate)).TotalDays
            : 0;

        return Ok(new
        {
            customer = new
            {
                customer.CustomerId,
                customer.FullName,
                customer.Phone,
                customer.Email,
                customer.Address
            },
            stats = new
            {
                totalOrders,
                totalPurchases,
                debt,
                daysSinceLastOrder
            },
            rfm,
            orders = orders.Select(o => new
            {
                o.OrderId,
                o.OrderDate,
                o.DeliveryDate,
                o.TotalAmount,
                o.IsPaid,
                o.PaymentDate,
                o.Notes,
                ItemCount = o.OrderItems.Count
            }).ToList(),
            smsLogs,
            notes
        });
    }

    // ============================================================
    // CRUD یادداشت‌ها (توی همین کنترلر چون وابسته به مشترین)
    // ============================================================
    [HttpGet("{id}/notes")]
    public async Task<IActionResult> GetNotes(int id)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer == null) return NotFound();

        var notes = await _context.CustomerNotes
            .Where(n => n.CustomerId == id)
            .OrderByDescending(n => n.CreatedDate)
            .ToListAsync();
        return Ok(notes);
    }

    [HttpPost("{id}/notes")]
    public async Task<IActionResult> CreateNote(int id, [FromBody] CustomerNote note)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer == null) return NotFound("مشتری یافت نشد.");

        if (string.IsNullOrWhiteSpace(note.NoteText))
            return BadRequest("متن یادداشت اجباری است.");

        note.NoteId = 0;
        note.CustomerId = id;
        note.CreatedDate = DateTime.Now;

        _context.CustomerNotes.Add(note);
        await _context.SaveChangesAsync();
        return Ok(note);
    }

    [HttpPut("{id}/notes/{noteId}")]
    public async Task<IActionResult> UpdateNote(int id, int noteId, [FromBody] CustomerNote note)
    {
        if (noteId != note.NoteId) return BadRequest();

        var existing = await _context.CustomerNotes
            .FirstOrDefaultAsync(n => n.NoteId == noteId && n.CustomerId == id);
        if (existing == null) return NotFound();

        existing.NoteText = note.NoteText;
        existing.NoteType = note.NoteType;
        existing.IsResolved = note.IsResolved;
        existing.FollowUpDate = note.FollowUpDate;
        existing.CreatedBy = note.CreatedBy;

        await _context.SaveChangesAsync();
        return Ok(existing);
    }

    [HttpDelete("{id}/notes/{noteId}")]
    public async Task<IActionResult> DeleteNote(int id, int noteId)
    {
        var note = await _context.CustomerNotes
            .FirstOrDefaultAsync(n => n.NoteId == noteId && n.CustomerId == id);
        if (note == null) return NotFound();

        _context.CustomerNotes.Remove(note);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Customer customer)
    {
        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = customer.CustomerId }, customer);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Customer customer)
    {
        if (id != customer.CustomerId) return BadRequest();
        _context.Entry(customer).State = EntityState.Modified;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer == null) return NotFound();
        _context.Customers.Remove(customer);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}