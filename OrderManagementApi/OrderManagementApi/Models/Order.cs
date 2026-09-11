using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OrderManagementApi.Models
{
    public class Order
    {
        [Key]
        public int OrderId { get; set; }

        public DateTime OrderDate { get; set; } = DateTime.Now;

        public DateTime DeliveryDate { get; set; }

        public int CustomerId { get; set; }

        public decimal TotalAmount { get; set; } = 0;

        public bool IsPaid { get; set; } = false;

        public DateTime? PaymentDate { get; set; }

        public string? Notes { get; set; }

        // Navigation Properties (بدون [Required])
        [ForeignKey("CustomerId")]
        public virtual Customer? Customer { get; set; }

        public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }
}