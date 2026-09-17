using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;   // ← این خط اضافه شه


namespace OrderManagementApi.Models
{
    public class ExpenseCategory
    {
        [Key]
        public int CategoryId { get; set; }

        [Required]
        [MaxLength(100)]
        public string CategoryName { get; set; } = string.Empty;

        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        [JsonIgnore]   // ← این خط اضافه شه
        public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
    }
}