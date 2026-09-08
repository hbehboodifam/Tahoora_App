namespace OrderManagementApp.Models
{
    public class DashboardStats
    {
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public int TotalCustomers { get; set; }
        public int PendingPayments { get; set; }
        public List<RecentOrderDto> RecentOrders { get; set; } = new();
    }

    public class RecentOrderDto
    {
        public int OrderId { get; set; }
        public string CustomerName { get; set; } = "";
        public string OrderDate { get; set; } = "";
        public decimal TotalAmount { get; set; }
        public bool IsPaid { get; set; }
    }
}