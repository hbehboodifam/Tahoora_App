using Microsoft.EntityFrameworkCore;
using OrderManagementApi.Models;

namespace OrderManagementApi.Services
{
    // ===== انواع =====
    public enum PredictionStatus { Overdue, DueNow, Upcoming, Dormant }
    public enum ConfidenceLevel { Low, Medium, High }

    // ===== خروجی =====
    public class ReorderPrediction
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = "";
        public string? Phone { get; set; }

        public int OrderCount { get; set; }
        public DateTime LastOrderDate { get; set; }
        public DateTime PredictedDate { get; set; }

        /// <summary>میانه‌ی فاصله‌ی سفارشات (روز)</summary>
        public double AverageIntervalDays { get; set; }

        /// <summary>چند روز از آخرین سفارش گذشته</summary>
        public int DaysSinceLastOrder { get; set; }

        /// <summary>چند روز تا تاریخ پیش‌بینی. منفی = عقب‌افتاده</summary>
        public int DaysUntilPredicted { get; set; }

        /// <summary>0..1 — هرچی بالاتر، مشتری منظم‌تر</summary>
        public double Regularity { get; set; }

        public ConfidenceLevel Confidence { get; set; }
        public PredictionStatus Status { get; set; }

        // ===== اطلاعات جانبی =====
        public decimal TotalPurchase { get; set; }
        public decimal Debt { get; set; }
        public string RfmCategory { get; set; } = "";
        public string RfmCategoryKey { get; set; } = "";
    }

    /// <summary>
    /// سرویس مشترک «دستیار ارتباط با مشتری»
    /// - پیش‌بینی زمان سفارش مجدد بر اساس میانه‌ی فواصل
    /// - تشخیص مشتری‌های خواب‌رفته
    /// - استفاده‌ی مجدد از RfmCalculator
    /// فقط روی سفارشات OrderSource = "Regular" کار می‌کند
    /// </summary>
    public class CustomerEngagementService
    {
        private readonly AppDbContext _db;
        private readonly RfmCalculator _rfmCalculator;

        public CustomerEngagementService(AppDbContext db, RfmCalculator rfmCalculator)
        {
            _db = db;
            _rfmCalculator = rfmCalculator;
        }

        /// <summary>
        /// لیست همه‌ی مشتری‌ها با پیش‌بینی، مرتب‌شده بر اساس فوریت (عقب‌افتاده‌ها اول)
        /// </summary>
        public async Task<List<ReorderPrediction>> GetAllPredictionsAsync(int minOrderCount = 3)
        {
            // ۱. فقط سفارشات Regular (نه Surplus و نه Channel)
            var regularOrders = await _db.Orders
                .Where(o => o.OrderSource == "Regular")
                .Select(o => new { o.CustomerId, o.OrderDate, o.TotalAmount, o.IsPaid })
                .ToListAsync();

            if (!regularOrders.Any()) return new List<ReorderPrediction>();

            // ۲. اطلاعات مشتری
            var customers = await _db.Customers
                .Select(c => new { c.CustomerId, c.FullName, c.Phone })
                .ToDictionaryAsync(c => c.CustomerId);

            // ۳. گروه‌بندی سفارشات
            var grouped = regularOrders.GroupBy(o => o.CustomerId).ToList();

            // ۴. RFM برای همه‌ی مشتری‌ها (یک بار)
            var rawData = grouped.Select(g => new RfmRawData
            {
                CustomerId = g.Key,
                CustomerName = customers.ContainsKey(g.Key) ? customers[g.Key].FullName : "نامشخص",
                OrderCount = g.Count(),
                TotalPurchase = g.Sum(o => o.TotalAmount),
                AverageOrderValue = g.Average(o => o.TotalAmount),
                DistinctProducts = 0,
                LastOrderDate = g.Max(o => o.OrderDate)
            }).ToList();

            var debtByCustomer = regularOrders
                .Where(o => !o.IsPaid)
                .GroupBy(o => o.CustomerId)
                .ToDictionary(g => g.Key, g => g.Sum(o => o.TotalAmount));

            var rfmScores = _rfmCalculator.Calculate(rawData, debtByCustomer)
                .ToDictionary(r => r.CustomerId);

            // ۵. محاسبه‌ی پیش‌بینی برای هر مشتری
            var result = new List<ReorderPrediction>();

            foreach (var g in grouped)
            {
                if (g.Count() < minOrderCount) continue;

                var dates = g.Select(o => o.OrderDate.Date)
                             .OrderBy(d => d)
                             .ToList();

                var prediction = CalculatePrediction(dates);
                if (prediction == null) continue;

                prediction.CustomerId = g.Key;
                prediction.OrderCount = g.Count();
                prediction.TotalPurchase = g.Sum(o => o.TotalAmount);
                prediction.Debt = debtByCustomer.TryGetValue(g.Key, out var d) ? d : 0m;

                if (customers.TryGetValue(g.Key, out var c))
                {
                    prediction.CustomerName = c.FullName;
                    prediction.Phone = c.Phone;
                }

                if (rfmScores.TryGetValue(g.Key, out var rfm))
                {
                    prediction.RfmCategory = rfm.Category;
                    prediction.RfmCategoryKey = rfm.CategoryKey;
                }

                result.Add(prediction);
            }

            // مرتب‌سازی: عقب‌افتاده‌ها اول (daysUntil بیشترین منفی)، بعد بقیه
            return result
                .OrderBy(p => p.DaysUntilPredicted)
                .ThenBy(p => p.CustomerName)
                .ToList();
        }

        // ============================================================
        // هسته‌ی محاسبه — جدا کردم تا تست‌پذیر باشه
        // ============================================================
        private static ReorderPrediction? CalculatePrediction(List<DateTime> sortedDates)
        {
            // فقط ۶ سفارش آخر (رفتار جدید مهم‌تره)
            var recent = sortedDates.TakeLast(6).ToList();
            if (recent.Count < 3) return null;

            // فواصل — با فیلتر نویز (سفارش‌های کمتر از ۳ روز فاصله)
            var intervals = new List<double>();
            for (int i = 1; i < recent.Count; i++)
            {
                var days = (recent[i] - recent[i - 1]).TotalDays;
                if (days >= 3) intervals.Add(days);
            }

            if (intervals.Count < 2) return null;

            var median = Median(intervals);
            if (median <= 0) return null;

            // انحراف معیار مقاوم (MAD-based) — حساس به outlier نیست
            var stdDev = RobustStdDev(intervals, median);

            var lastOrder = recent.Last();
            var daysSinceLast = (DateTime.Today - lastOrder).TotalDays;
            var predictedDate = lastOrder.AddDays(median);
            var daysUntil = (predictedDate - DateTime.Today).TotalDays;

            // منظم بودن: 0..1
            var regularity = Math.Max(0, 1 - Math.Min(1, stdDev / median));

            var confidence = regularity switch
            {
                > 0.70 => ConfidenceLevel.High,
                > 0.40 => ConfidenceLevel.Medium,
                _      => ConfidenceLevel.Low
            };

            // وضعیت
            PredictionStatus status;
            if (daysSinceLast > 60 && daysSinceLast > median * 2)
                status = PredictionStatus.Dormant;
            else if (daysUntil < 0)
                status = PredictionStatus.Overdue;
            else if (daysUntil <= 3)
                status = PredictionStatus.DueNow;
            else
                status = PredictionStatus.Upcoming;

            return new ReorderPrediction
            {
                LastOrderDate = lastOrder,
                PredictedDate = predictedDate,
                AverageIntervalDays = Math.Round(median, 1),
                DaysSinceLastOrder = (int)daysSinceLast,
                DaysUntilPredicted = (int)daysUntil,
                Regularity = Math.Round(regularity, 2),
                Confidence = confidence,
                Status = status
            };
        }

        // ===== میانه =====
        private static double Median(List<double> values)
        {
            var sorted = values.OrderBy(v => v).ToList();
            int n = sorted.Count;
            if (n == 0) return 0;
            if (n % 2 == 1) return sorted[n / 2];
            return (sorted[n / 2 - 1] + sorted[n / 2]) / 2.0;
        }

        // ===== انحراف معیار مقاوم بر مبنای MAD =====
        // MAD = میانه‌ی قدرمطلق انحرافات از میانه
        // std_dev ≈ MAD × 1.4826
        private static double RobustStdDev(List<double> values, double median)
        {
            if (values.Count == 0) return 0;
            var deviations = values.Select(v => Math.Abs(v - median)).OrderBy(d => d).ToList();
            var mad = Median(deviations);
            return mad * 1.4826;
        }
    }
}