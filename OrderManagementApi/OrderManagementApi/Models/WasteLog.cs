using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OrderManagementApi.Models
{
    /// <summary>
    /// ثبت ضایعات — با قفل کردن UnitCost در لحظه‌ی ثبت
    /// </summary>
    public class WasteLog
    {
        [Key]
        public int WasteLogId { get; set; }

        [Required]
        public int ProductId { get; set; }

        [ForeignKey(nameof(ProductId))]
        public Product? Product { get; set; }

        [Required]
        public decimal Quantity { get; set; }

        /// <summary>بهای تمام‌شده در لحظه‌ی ثبت ضایعات (قفل‌شده)</summary>
        [Required]
        public decimal UnitCost { get; set; }

        public DateTime WasteDate { get; set; } = DateTime.Now;

        /// <summary>دلیل: مازاد فروش‌نرفته / خراب شده / منقضی / برگشتی</summary>
        [MaxLength(50)]
        public string? Reason { get; set; }

        public string? Notes { get; set; }
    }
}