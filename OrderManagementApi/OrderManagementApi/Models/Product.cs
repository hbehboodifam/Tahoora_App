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

        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }
}