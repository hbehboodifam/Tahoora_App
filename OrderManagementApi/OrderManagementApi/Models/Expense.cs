using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OrderManagementApi.Models
{
    public class Expense
    {
        [Key]
        public int ExpenseId { get; set; }

        [Required]
        public DateTime ExpenseDate { get; set; }

        [Required]
        public int CategoryId { get; set; }

        [ForeignKey(nameof(CategoryId))]
        public ExpenseCategory? Category { get; set; }

        [Required]
        public decimal Amount { get; set; }

        public bool IsRecurring { get; set; } = false;

        public string? Description { get; set; }
    }
}