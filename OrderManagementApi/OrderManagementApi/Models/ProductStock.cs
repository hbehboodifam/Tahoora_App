using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OrderManagementApi.Models
{
    /// <summary>
    /// موجودی فعلی هر محصول (فقط یه رکورد به ازای هر محصول)
    /// </summary>
    public class ProductStock
    {
        [Key]
        public int ProductId { get; set; }

        [ForeignKey(nameof(ProductId))]
        public Product? Product { get; set; }

        public decimal Quantity { get; set; } = 0;

        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }
}