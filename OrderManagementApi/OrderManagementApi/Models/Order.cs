using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OrderManagementApi.Models;

public class Order
{
    [Key]
    public int OrderId { get; set; }
    
    [Required]
    public DateTime OrderDate { get; set; } = DateTime.Now;  // تاریخ ثبت سفارش (پیش‌فرض امروز)
    
    [Required]
    public DateTime DeliveryDate { get; set; }  // تاریخ تحویل
    
    [Required]
    public int CustomerId { get; set; }
    
    public decimal TotalAmount { get; set; } = 0;  // مجموع مبلغ (خودکار محاسبه می‌شود)
    
    public bool IsPaid { get; set; } = false;  // وضعیت پرداخت
    
    public DateTime? PaymentDate { get; set; }  // تاریخ واریز (در صورت پرداخت)
    
    public string? Notes { get; set; }  // توضیحات
    
    // رابطه چند به یک با Customer
    [ForeignKey("CustomerId")]
    public Customer Customer { get; set; } = null!;
    
    // رابطه یک به چند با OrderItem
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}