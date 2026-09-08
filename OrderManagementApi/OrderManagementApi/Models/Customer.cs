using System.ComponentModel.DataAnnotations;

namespace OrderManagementApi.Models;

public class Customer
{
    [Key]
    public int CustomerId { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;
    
    [MaxLength(20)]
    public string? Phone { get; set; }
    
    [MaxLength(100)]
    public string? Email { get; set; }
    
    public string? Address { get; set; }
    
    // رابطه یک به چند با Order
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}