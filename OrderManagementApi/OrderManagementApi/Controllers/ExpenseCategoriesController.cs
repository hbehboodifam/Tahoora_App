using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderManagementApi.Models;

namespace OrderManagementApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExpenseCategoriesController : ControllerBase
{
    private readonly AppDbContext _context;

    public ExpenseCategoriesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var cats = await _context.ExpenseCategories
            .OrderBy(c => c.CategoryName)
            .ToListAsync();
        return Ok(cats);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var cat = await _context.ExpenseCategories.FindAsync(id);
        if (cat == null) return NotFound();
        return Ok(cat);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ExpenseCategory cat)
    {
        _context.ExpenseCategories.Add(cat);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = cat.CategoryId }, cat);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] ExpenseCategory cat)
    {
        if (id != cat.CategoryId) return BadRequest();

        var existing = await _context.ExpenseCategories.FindAsync(id);
        if (existing == null) return NotFound();

        existing.CategoryName = cat.CategoryName;
        existing.Description  = cat.Description;
        existing.IsActive     = cat.IsActive;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var cat = await _context.ExpenseCategories.FindAsync(id);
        if (cat == null) return NotFound();

        var hasExpenses = await _context.Expenses.AnyAsync(e => e.CategoryId == id);
        if (hasExpenses)
            return BadRequest(new { message = "این دسته هزینه دارد و قابل حذف نیست." });

        _context.ExpenseCategories.Remove(cat);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}