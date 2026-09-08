using System.ComponentModel.DataAnnotations;

namespace OrderManagementApi.Models;

public class Product
{
    [Key]
    public int ProductId { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string ProductName { get; set; } = string.Empty;
    
    [Required]
    public decimal UnitPrice { get; set; }  // قیمت واحد
    
    public string? Description { get; set; }  // توضیحات (اختیاری)
    
    // رابطه یک به چند با OrderItem
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}