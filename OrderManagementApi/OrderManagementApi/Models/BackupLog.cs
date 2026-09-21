using System.ComponentModel.DataAnnotations;

namespace OrderManagementApi.Models
{
    public class BackupLog
    {
        [Key]
        public int BackupLogId { get; set; }

        public DateTime BackupDate { get; set; } = DateTime.Now;

        /// <summary>auto / monthly / manual</summary>
        [MaxLength(20)]
        public string BackupType { get; set; } = "auto";

        [MaxLength(500)]
        public string FilePath { get; set; } = "";

        public long FileSizeBytes { get; set; }

        /// <summary>Success / Failed</summary>
        [MaxLength(20)]
        public string Status { get; set; } = "Success";

        public string? ErrorMessage { get; set; }
    }
}