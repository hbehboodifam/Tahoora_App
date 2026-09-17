using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderManagementApi.Models;

namespace OrderManagementApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExpensesController : ControllerBase
{
    private readonly AppDbContext _context;

    public ExpensesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int? categoryId = null)
    {
        var q = _context.Expenses.Include(e => e.Category).AsQueryable();

        if (from.HasValue) q = q.Where(e => e.ExpenseDate >= from.Value);
        if (to.HasValue)   q = q.Where(e => e.ExpenseDate <= to.Value);
        if (categoryId.HasValue) q = q.Where(e => e.CategoryId == categoryId.Value);

        var list = await q.OrderByDescending(e => e.ExpenseDate).ToListAsync();
        return Ok(list);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var e = await _context.Expenses
            .Include(x => x.Category)
            .FirstOrDefaultAsync(x => x.ExpenseId == id);
        if (e == null) return NotFound();
        return Ok(e);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Expense expense)
    {
        _context.Expenses.Add(expense);
        await _context.SaveChangesAsync();

        await _context.Entry(expense).Reference(e => e.Category).LoadAsync();

        return CreatedAtAction(nameof(GetById), new { id = expense.ExpenseId }, expense);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Expense expense)
    {
        if (id != expense.ExpenseId) return BadRequest();

        var existing = await _context.Expenses.FindAsync(id);
        if (existing == null) return NotFound();

        existing.ExpenseDate = expense.ExpenseDate;
        existing.CategoryId  = expense.CategoryId;
        existing.Amount      = expense.Amount;
        existing.IsRecurring = expense.IsRecurring;
        existing.Description = expense.Description;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var e = await _context.Expenses.FindAsync(id);
        if (e == null) return NotFound();
        _context.Expenses.Remove(e);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}