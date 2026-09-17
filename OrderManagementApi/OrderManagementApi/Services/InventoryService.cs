using Microsoft.EntityFrameworkCore;
using OrderManagementApi.Models;

namespace OrderManagementApi.Services
{
    public class InventoryService
    {
        private readonly AppDbContext _context;

        public InventoryService(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>خوندن موجودی — اگه رکورد نبود، صفر برمی‌گردونه</summary>
        public async Task<decimal> GetStockAsync(int productId)
        {
            var stock = await _context.ProductStocks
                .FirstOrDefaultAsync(s => s.ProductId == productId);
            return stock?.Quantity ?? 0m;
        }

        /// <summary>افزایش موجودی (تولید، خرید، ...)</summary>
        public async Task IncreaseStockAsync(int productId, decimal quantity)
        {
            if (quantity <= 0)
                throw new ArgumentException("مقدار افزایش باید مثبت باشد.");

            var stock = await EnsureStockExistsAsync(productId);
            stock.Quantity += quantity;
            stock.LastUpdated = DateTime.Now;
        }

        /// <summary>کاهش موجودی (فروش، ضایعات، ...)</summary>
        public async Task DecreaseStockAsync(int productId, decimal quantity)
        {
            if (quantity <= 0)
                throw new ArgumentException("مقدار کاهش باید مثبت باشد.");

            var stock = await EnsureStockExistsAsync(productId);

            if (stock.Quantity < quantity)
                throw new InvalidOperationException(
                    $"موجودی کافی نیست. موجودی فعلی: {stock.Quantity}، درخواستی: {quantity}");

            stock.Quantity -= quantity;
            stock.LastUpdated = DateTime.Now;
        }
        
        /// <summary>
        /// کاهش موجودی بدون validation — موجودی می‌تونه منفی بشه
        /// برای ثبت سفارش (چون ممکنه موقع ثبت، هنوز تولید نکرده باشی)
        /// </summary>
        public async Task DecreaseStockAllowNegativeAsync(int productId, decimal quantity)
        {
            if (quantity <= 0)
                throw new ArgumentException("مقدار کاهش باید مثبت باشد.");

            var stock = await EnsureStockExistsAsync(productId);
            stock.Quantity -= quantity;
            stock.LastUpdated = DateTime.Now;
        }
        
        /// <summary>
        /// تنظیم موجودی با delta (مثبت یا منفی) — بدون validation
        /// برای موارد خاص مثل ثبت مازاد/کسری تولید
        /// </summary>
        public async Task AdjustStockAsync(int productId, decimal delta)
        {
            if (delta == 0) return;

            var stock = await EnsureStockExistsAsync(productId);
            stock.Quantity += delta;
            if (stock.Quantity < 0) stock.Quantity = 0;
            stock.LastUpdated = DateTime.Now;
        }

        /// <summary>اطمینان از وجود رکورد موجودی برای محصول</summary>
        public async Task<ProductStock> EnsureStockExistsAsync(int productId)
        {
            var stock = await _context.ProductStocks
                .FirstOrDefaultAsync(s => s.ProductId == productId);

            if (stock == null)
            {
                stock = new ProductStock
                {
                    ProductId = productId,
                    Quantity = 0,
                    LastUpdated = DateTime.Now
                };
                _context.ProductStocks.Add(stock);
                await _context.SaveChangesAsync();
            }

            return stock;
        }
    }
}