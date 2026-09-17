using System.ComponentModel.DataAnnotations;

namespace OrderManagementApi.Models
{
    public class Product
    {
        [Key]
        public int ProductId { get; set; }

        [Required]
        [MaxLength(100)]
        public string ProductName { get; set; } = string.Empty;

        [Required]
        public decimal UnitPrice { get; set; }

        [MaxLength(20)]
        public string Unit { get; set; } = "کیلوگرم";

        public string? Description { get; set; }

        // ===== اجزای بهای تمام‌شده =====

        public decimal MaterialCost { get; set; } = 0;

        public decimal PackagingCost { get; set; } = 0;

        public decimal WastePercent { get; set; } = 0;

        public decimal LaborCost { get; set; } = 0;

        public decimal OverheadPercent { get; set; } = 0;

        /// <summary>بهای تمام‌شده — در Backend محاسبه می‌شه</summary>
        public decimal CostPrice { get; set; } = 0;

        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

        // ===== محاسبه =====
        public void RecalculateCostPrice()
        {
            CostPrice = ComputeCostPrice(
                MaterialCost, PackagingCost, WastePercent, LaborCost, OverheadPercent);
        }

        public static decimal ComputeCostPrice(
            decimal materialCost, decimal packagingCost,
            decimal wastePercent, decimal laborCost, decimal overheadPercent)
        {
            var baseCost     = materialCost + packagingCost;
            var afterWaste   = baseCost * (1 + wastePercent / 100m);
            var withLabor    = afterWaste + laborCost;
            var withOverhead = withLabor * (1 + overheadPercent / 100m);
            return Math.Round(withOverhead, 2);
        }
    }
}