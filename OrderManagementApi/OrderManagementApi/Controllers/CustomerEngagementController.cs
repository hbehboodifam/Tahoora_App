using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderManagementApi.Models;
using OrderManagementApi.Services;

namespace OrderManagementApi.Controllers;

[ApiController]
[Route("api/customer-engagement")]
public class CustomerEngagementController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly CustomerEngagementService _engagement;
    private readonly SmsService _sms;

    public CustomerEngagementController(
        AppDbContext db,
        CustomerEngagementService engagement,
        SmsService sms)
    {
        _db = db;
        _engagement = engagement;
        _sms = sms;
    }

    // ============================================================
    // لیست کامل پیش‌بینی‌ها (مرتب‌شده بر اساس فوریت)
    // ============================================================
    [HttpGet("predictions")]
    public async Task<IActionResult> GetPredictions()
    {
        var list = await _engagement.GetAllPredictionsAsync();
        return Ok(list.Select(ToDto));
    }

    // ============================================================
    // خلاصه‌ی آماری برای کارت‌های بالای صفحه
    // ============================================================
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var list = await _engagement.GetAllPredictionsAsync();
        return Ok(new
        {
            overdue  = list.Count(p => p.Status == PredictionStatus.Overdue),
            dueNow   = list.Count(p => p.Status == PredictionStatus.DueNow),
            upcoming = list.Count(p => p.Status == PredictionStatus.Upcoming),
            dormant  = list.Count(p => p.Status == PredictionStatus.Dormant),
            total    = list.Count
        });
    }

    // ============================================================
    // ارسال پیامک یادآوری (دستی از سمت کاربر)
    // ============================================================
    [HttpPost("send-reminder/{customerId}")]
    public async Task<IActionResult> SendReminder(int customerId, [FromBody] SendReminderRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.Message))
            return BadRequest("متن پیامک خالی است.");

        var customer = await _db.Customers.FindAsync(customerId);
        if (customer == null) return NotFound("مشتری یافت نشد.");
        if (string.IsNullOrWhiteSpace(customer.Phone))
            return BadRequest("شماره تماس برای این مشتری ثبت نشده.");

        // ===== قانون ضد-اسپم: حداکثر ۱ Reminder در ۷ روز =====
        var weekAgo = DateTime.Now.AddDays(-7);
        var recentReminder = await _db.CustomerSmsLogs.AnyAsync(s =>
            s.CustomerId == customerId &&
            s.SmsType == "Reminder" &&
            s.SentDate > weekAgo);

        if (recentReminder && !req.Force)
            return BadRequest("در ۷ روز گذشته یادآوری ارسال شده. اگر باز هم لازم است، با تایید مجدد ارسال کنید.");

        // ===== ارسال =====
        var (success, error) = await _sms.SendSms(customer.Phone, req.Message);

        // ===== لاگ (حتی اگر ناموفق بود، ثبت کن) =====
        _db.CustomerSmsLogs.Add(new CustomerSmsLog
        {
            CustomerId = customerId,
            SentDate = DateTime.Now,
            MessageText = req.Message,
            SmsType = "Reminder"
        });
        await _db.SaveChangesAsync();

        if (!success)
            return BadRequest($"خطا در ارسال پیامک: {error}");

        return Ok(new { success = true, message = "پیامک با موفقیت ارسال شد." });
    }

    // ============================================================
    // DTO برای Blazor (بدون وابستگی به enumهای API)
    // ============================================================
    private static object ToDto(ReorderPrediction p) => new
    {
        p.CustomerId,
        p.CustomerName,
        p.Phone,
        p.OrderCount,
        p.LastOrderDate,
        p.PredictedDate,
        p.AverageIntervalDays,
        p.DaysSinceLastOrder,
        p.DaysUntilPredicted,
        p.Regularity,
        Confidence = p.Confidence.ToString(),   // "High" / "Medium" / "Low"
        Status     = p.Status.ToString(),        // "Overdue" / "DueNow" / ...
        p.TotalPurchase,
        p.Debt,
        p.RfmCategory,
        p.RfmCategoryKey
    };

    public class SendReminderRequest
    {
        public string Message { get; set; } = "";
        public bool Force { get; set; } = false;
    }
}