using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace OrderManagementApi.Models
{
    public class CustomerNote
    {
        [Key]
        public int NoteId { get; set; }

        [Required]
        public int CustomerId { get; set; }

        [ForeignKey(nameof(CustomerId))]
        [JsonIgnore]   // ← این خط جدید
        public Customer? Customer { get; set; }

        [Required]
        public string NoteText { get; set; } = string.Empty;

        [MaxLength(50)]
        public string NoteType { get; set; } = "متفرقه";

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public bool IsResolved { get; set; } = false;

        public DateTime? FollowUpDate { get; set; }

        [MaxLength(100)]
        public string? CreatedBy { get; set; }
    }
}