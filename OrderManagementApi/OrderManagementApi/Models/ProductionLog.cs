using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OrderManagementApi.Models
{
    /// <summary>
    /// هر بار تولید یک محصول — برنامه‌ریزی‌شده vs تولید واقعی
    /// </summary>
    public class ProductionLog
    {
        [Key]
        public int ProductionLogId { get; set; }

        [Required]
        public int ProductId { get; set; }

        [ForeignKey(nameof(ProductId))]
        public Product? Product { get; set; }

        public DateTime ProductionDate { get; set; } = DateTime.Now;

        /// <summary>جمع سفارش‌های امروز/فردا برای این محصول</summary>
        public decimal PlannedQuantity { get; set; }

        /// <summary>مقدار واقعی که تولید شد</summary>
        public decimal ActualProducedQuantity { get; set; }

        public string? Notes { get; set; }
    }
}