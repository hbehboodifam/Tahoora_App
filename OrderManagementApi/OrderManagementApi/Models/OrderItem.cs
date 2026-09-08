using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OrderManagementApi.Models;

public class OrderItem
{
    [Key]
    public int OrderItemId { get; set; }
    
    [Required]
    public int OrderId { get; set; }
    
    [Required]
    public int ProductId { get; set; }
    
    [Required]
    public decimal Quantity { get; set; }  // تعداد (کیلو یا عدد)
    
    [Required]
    public decimal UnitPrice { get; set; }  // قیمت واحد در زمان سفارش
    
    public decimal TotalPrice => Quantity * UnitPrice;  // محاسبه خودکار جمع (نیاز به ذخیره در دیتابیس نیست)
    
    // روابط
    [ForeignKey("OrderId")]
    public Order Order { get; set; } = null!;
    
    [ForeignKey("ProductId")]
    public Product Product { get; set; } = null!;
}