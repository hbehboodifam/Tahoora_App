namespace OrderManagementApi.Services
{
    // ===== ورودی خام =====
    public class RfmRawData
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = "";
        public int OrderCount { get; set; }
        public decimal TotalPurchase { get; set; }
        public decimal AverageOrderValue { get; set; }
        public int DistinctProducts { get; set; }
        public DateTime LastOrderDate { get; set; }
    }

    // ===== خروجی امتیازدهی‌شده =====
    public class RfmScore
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = "";
        public int OrderCount { get; set; }
        public decimal TotalPurchase { get; set; }
        public decimal AverageOrderValue { get; set; }
        public int DistinctProducts { get; set; }
        public DateTime LastOrderDate { get; set; }
        public int DaysSinceLastOrder { get; set; }
        public decimal Debt { get; set; }
        public int RScore { get; set; }
        public int FScore { get; set; }
        public int MScore { get; set; }
        public int TotalScore { get; set; }
        public string Category { get; set; } = "";
        public string CategoryKey { get; set; } = "";
    }

    /// <summary>
    /// سرویس مشترک محاسبه‌ی RFM
    /// هم گزارش عملکرد مشتریان و هم پروفایل مشتری از این استفاده می‌کنند
    /// </summary>
    public class RfmCalculator
    {
        public List<RfmScore> Calculate(
            List<RfmRawData> rawData,
            Dictionary<int, decimal> debtByCustomer)
        {
            if (rawData == null || !rawData.Any())
                return new List<RfmScore>();

            // ===== محاسبه‌ی صدک‌ها =====
            var recencyValues   = rawData.Select(r => (DateTime.Now - r.LastOrderDate).TotalDays).OrderBy(v => v).ToList();
            var frequencyValues = rawData.Select(r => (double)r.OrderCount).OrderBy(v => v).ToList();
            var monetaryValues  = rawData.Select(r => (double)r.TotalPurchase).OrderBy(v => v).ToList();

            double recencyP33   = GetPercentile(recencyValues, 33);
            double recencyP66   = GetPercentile(recencyValues, 66);
            double freqP33      = GetPercentile(frequencyValues, 33);
            double freqP66      = GetPercentile(frequencyValues, 66);
            double monetaryP33  = GetPercentile(monetaryValues, 33);
            double monetaryP66  = GetPercentile(monetaryValues, 66);

            // ===== امتیازدهی =====
            return rawData.Select(r =>
            {
                var recencyDays = (DateTime.Now - r.LastOrderDate).TotalDays;

                int rScore = recencyDays <= recencyP33 ? 3 : recencyDays <= recencyP66 ? 2 : 1;
                int fScore = r.OrderCount >= freqP66 ? 3 : r.OrderCount >= freqP33 ? 2 : 1;
                int mScore = (double)r.TotalPurchase >= monetaryP66 ? 3
                           : (double)r.TotalPurchase >= monetaryP33 ? 2 : 1;
                int totalScore = rScore + fScore + mScore;

                string cat = totalScore >= 8 ? "🏆 VIP"
                           : totalScore >= 6 ? "🟢 وفادار"
                           : totalScore >= 4 ? "🟡 معمولی"
                           : "🔴 در معرض ریزش";

                string catKey = totalScore >= 8 ? "VIP"
                              : totalScore >= 6 ? "Loyal"
                              : totalScore >= 4 ? "Normal"
                              : "AtRisk";

                return new RfmScore
                {
                    CustomerId = r.CustomerId,
                    CustomerName = r.CustomerName,
                    OrderCount = r.OrderCount,
                    TotalPurchase = r.TotalPurchase,
                    AverageOrderValue = r.AverageOrderValue,
                    DistinctProducts = r.DistinctProducts,
                    LastOrderDate = r.LastOrderDate,
                    DaysSinceLastOrder = (int)recencyDays,
                    Debt = debtByCustomer != null && debtByCustomer.ContainsKey(r.CustomerId)
                        ? debtByCustomer[r.CustomerId] : 0m,
                    RScore = rScore,
                    FScore = fScore,
                    MScore = mScore,
                    TotalScore = totalScore,
                    Category = cat,
                    CategoryKey = catKey
                };
            })
            .OrderByDescending(c => c.TotalPurchase)
            .ToList();
        }

        public static double GetPercentile(List<double> sortedValues, double percentile)
        {
            if (sortedValues.Count == 0) return 0;
            if (sortedValues.Count == 1) return sortedValues[0];

            double rank = (percentile / 100.0) * (sortedValues.Count - 1);
            int lowerIndex = (int)Math.Floor(rank);
            int upperIndex = (int)Math.Ceiling(rank);

            if (lowerIndex == upperIndex)
                return sortedValues[lowerIndex];

            double weight = rank - lowerIndex;
            return sortedValues[lowerIndex] * (1 - weight) + sortedValues[upperIndex] * weight;
        }
    }
}