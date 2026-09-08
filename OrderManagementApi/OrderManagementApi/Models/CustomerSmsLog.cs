using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OrderManagementApi.Models
{
    public class CustomerSmsLog
    {
        [Key]
        public int LogId { get; set; }

        [Required]
        public int CustomerId { get; set; }

        [Required]
        public DateTime SentDate { get; set; }

        public string? MessageText { get; set; }

        public string? SmsType { get; set; } // "Auto" یا "Manual"

        [ForeignKey("CustomerId")]
        public virtual Customer? Customer { get; set; } // قابل‌تهی (Nullable)
    }
}