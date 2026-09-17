using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderManagementApi.Services;

namespace OrderManagementApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly InventoryService _inventory;

    public InventoryController(AppDbContext context, InventoryService inventory)
    {
        _context = context;
        _inventory = inventory;
    }

    /// <summary>لیست موجودی همه‌ی محصولات</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var products = await _context.Products.ToListAsync();
        var stocks = await _context.ProductStocks.ToDictionaryAsync(s => s.ProductId);

        var result = products
            .OrderBy(p => p.ProductName)
            .Select(p =>
            {
                stocks.TryGetValue(p.ProductId, out var stock);
                return new
                {
                    p.ProductId,
                    p.ProductName,
                    p.Unit,
                    p.CostPrice,
                    p.UnitPrice,
                    Quantity = stock?.Quantity ?? 0m,
                    LastUpdated = stock?.LastUpdated
                };
            })
            .ToList();

        return Ok(result);
    }

    /// <summary>موجودی یه محصول خاص</summary>
    [HttpGet("{productId}")]
    public async Task<IActionResult> GetByProduct(int productId)
    {
        var product = await _context.Products.FindAsync(productId);
        if (product == null) return NotFound("محصول یافت نشد.");

        var quantity = await _inventory.GetStockAsync(productId);

        return Ok(new
        {
            product.ProductId,
            product.ProductName,
            product.Unit,
            product.CostPrice,
            product.UnitPrice,
            Quantity = quantity
        });
    }
}