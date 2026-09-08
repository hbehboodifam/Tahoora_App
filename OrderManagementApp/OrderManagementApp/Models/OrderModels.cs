namespace OrderManagementApp.Models
{
    public class CustomerDto
    {
        public int CustomerId { get; set; }
        public string FullName { get; set; } = "";
    }

    public class OrderListModel
    {
        public int OrderId { get; set; }
        public string CustomerName { get; set; } = "";
        public string OrderDate { get; set; } = "";
        public string DeliveryDate { get; set; } = "";
        public decimal TotalAmount { get; set; }
        public bool IsPaid { get; set; }
        public DateTime? PaymentDate { get; set; }
        public string? Notes { get; set; }
    }
}