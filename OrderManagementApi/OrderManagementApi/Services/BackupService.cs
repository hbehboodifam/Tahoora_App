using System.Diagnostics;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OrderManagementApi.Models;

namespace OrderManagementApi.Services
{
    public class BackupResult
    {
        public bool Success { get; set; }
        public string? FilePath { get; set; }
        public long FileSize { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class BackupStatusDto
    {
        public int TotalBackupCount { get; set; }
        public DateTime? LastSuccessDate { get; set; }
        public DateTime? LastAttemptDate { get; set; }
        public string? LastAttemptStatus { get; set; }
        public string? LastAttemptError { get; set; }
        public double? HoursSinceLastSuccess { get; set; }
        public long TotalSizeBytes { get; set; }
        public bool IsHealthy { get; set; }
    }

    public class BackupService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;
        private readonly ILogger<BackupService> _logger;

        private readonly string _mysqldumpPath;
        private readonly string _backupRoot;
        private readonly int _retentionDaily;
        private readonly int _retentionMonthly;

        private readonly string _host;
        private readonly string _port;
        private readonly string _user;
        private readonly string _password;
        private readonly string _database;

        public BackupService(AppDbContext context, IConfiguration config, ILogger<BackupService> logger)
        {
            _context = context;
            _config = config;
            _logger = logger;

            _mysqldumpPath = _config["BackupSettings:MySqlDumpPath"] ?? "";
            _backupRoot = _config["BackupSettings:BackupRootFolder"] ?? "";
            _retentionDaily = int.TryParse(_config["BackupSettings:RetentionDaily"], out var rd) ? rd : 30;
            _retentionMonthly = int.TryParse(_config["BackupSettings:RetentionMonthly"], out var rm) ? rm : 12;

            var cs = _config.GetConnectionString("DefaultConnection") ?? "";
            var parts = cs.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Split('=', 2))
                .Where(p => p.Length == 2)
                .ToDictionary(p => p[0].Trim(), p => p[1].Trim(), StringComparer.OrdinalIgnoreCase);

            _host = parts.GetValueOrDefault("Server", "localhost");
            _port = parts.GetValueOrDefault("Port", "3306");
            _user = parts.GetValueOrDefault("User", "root");
            _password = parts.GetValueOrDefault("Password", "");
            _database = parts.GetValueOrDefault("Database", "");
        }

        // ============================================================
        // ایجاد بکاپ
        // ============================================================
        public async Task<BackupResult> CreateBackupAsync(string type = "auto")
        {
            try
            {
                // ===== اعتبارسنجی تنظیمات =====
                if (string.IsNullOrEmpty(_mysqldumpPath) || !File.Exists(_mysqldumpPath))
                    return await FailAsync(type, "مسیر mysqldump پیدا نشد.");

                if (string.IsNullOrEmpty(_backupRoot))
                    return await FailAsync(type, "پوشه‌ی بکاپ تنظیم نشده.");

                if (string.IsNullOrEmpty(_database))
                    return await FailAsync(type, "نام دیتابیس در connection string پیدا نشد.");

                // ===== ساخت پوشه =====
                var typeFolder = Path.Combine(_backupRoot, type);
                Directory.CreateDirectory(typeFolder);

                // ===== مسیرها =====
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                var finalFileName = $"OrderManagement_{type}_{timestamp}.sql";
                var finalPath = Path.Combine(typeFolder, finalFileName);
                var tempPath = finalPath + ".tmp";

                // ===== اجرای mysqldump =====
                var args = new StringBuilder();
                args.Append($"--host={_host} ");
                args.Append($"--port={_port} ");
                args.Append($"--user={_user} ");
                args.Append($"--password={_password} ");
                args.Append("--default-character-set=utf8mb4 ");
                args.Append("--single-transaction ");
                args.Append("--routines --triggers --events ");
                args.Append($"--databases {_database} ");
                args.Append($"--result-file=\"{tempPath}\"");

                var psi = new ProcessStartInfo
                {
                    FileName = _mysqldumpPath,
                    Arguments = args.ToString(),
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null)
                    return await FailAsync(type, "نتونستم پروسه‌ی mysqldump رو اجرا کنم.");

                var stderr = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    TryDeleteFile(tempPath);
                    return await FailAsync(type, $"mysqldump خطا داد (code {process.ExitCode}): {stderr}");
                }

                // ===== اعتبارسنجی ۱: وجود فایل =====
                if (!File.Exists(tempPath))
                    return await FailAsync(type, "فایل بکاپ ساخته نشد.");

                // ===== اعتبارسنجی ۲: حجم =====
                var fileInfo = new FileInfo(tempPath);
                if (fileInfo.Length < 10 * 1024) // کمتر از ۱۰ کیلوبایت
                {
                    TryDeleteFile(tempPath);
                    return await FailAsync(type,
                        $"حجم فایل بکاپ خیلی کمه ({fileInfo.Length} بایت). احتمالاً بکاپ ناموفق بوده.");
                }

                // ===== اعتبارسنجی ۳: Signature =====
                var head = await ReadFirstBytesAsync(tempPath, 500);
                if (!head.Contains("-- MySQL dump"))
                {
                    TryDeleteFile(tempPath);
                    return await FailAsync(type, "فایل بکاپ معتبر نیست (signature پیدا نشد).");
                }

                // ===== انتقال از temp به final =====
                File.Move(tempPath, finalPath, overwrite: true);

                // ===== ثبت لاگ موفق =====
                _context.BackupLogs.Add(new BackupLog
                {
                    BackupDate = DateTime.Now,
                    BackupType = type,
                    FilePath = finalPath,
                    FileSizeBytes = fileInfo.Length,
                    Status = "Success"
                });

                // ===== اگه اول ماه بود، یه کپی ماهانه =====
                if (type == "auto" && DateTime.Today.Day == 1)
                {
                    var monthlyFolder = Path.Combine(_backupRoot, "monthly");
                    Directory.CreateDirectory(monthlyFolder);
                    var monthlyPath = Path.Combine(monthlyFolder,
                        finalFileName.Replace("_auto_", "_monthly_"));
                    File.Copy(finalPath, monthlyPath, overwrite: true);

                    _context.BackupLogs.Add(new BackupLog
                    {
                        BackupDate = DateTime.Now,
                        BackupType = "monthly",
                        FilePath = monthlyPath,
                        FileSizeBytes = fileInfo.Length,
                        Status = "Success"
                    });
                }

                // ===== پاکسازی قدیمی‌ها (فقط بعد از موفقیت) =====
                CleanupOldBackups();

                await _context.SaveChangesAsync();

                return new BackupResult
                {
                    Success = true,
                    FilePath = finalPath,
                    FileSize = fileInfo.Length
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Backup failed unexpectedly");
                return await FailAsync(type, $"خطای غیرمنتظره: {ex.Message}");
            }
        }

        private async Task<BackupResult> FailAsync(string type, string message)
        {
            _logger.LogWarning("Backup failed: {Message}", message);
            try
            {
                _context.BackupLogs.Add(new BackupLog
                {
                    BackupDate = DateTime.Now,
                    BackupType = type,
                    FilePath = "",
                    FileSizeBytes = 0,
                    Status = "Failed",
                    ErrorMessage = message
                });
                await _context.SaveChangesAsync();
            }
            catch { }

            return new BackupResult { Success = false, ErrorMessage = message };
        }

        private static async Task<string> ReadFirstBytesAsync(string path, int count)
        {
            var buffer = new byte[count];
            using var fs = File.OpenRead(path);
            var read = await fs.ReadAsync(buffer, 0, count);
            return Encoding.UTF8.GetString(buffer, 0, read);
        }

        private static void TryDeleteFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { }
        }

        private void CleanupOldBackups()
        {
            try
            {
                // ===== چرخش بکاپ‌های روزانه =====
                var dailyFolder = Path.Combine(_backupRoot, "auto");
                if (Directory.Exists(dailyFolder))
                {
                    var files = Directory.GetFiles(dailyFolder, "*.sql")
                        .Select(f => new FileInfo(f))
                        .OrderByDescending(f => f.CreationTime)
                        .Skip(_retentionDaily)
                        .ToList();

                    foreach (var f in files)
                    {
                        try { f.Delete(); } catch { }
                    }
                }

                // ===== چرخش بکاپ‌های ماهانه =====
                var monthlyFolder = Path.Combine(_backupRoot, "monthly");
                if (Directory.Exists(monthlyFolder))
                {
                    var files = Directory.GetFiles(monthlyFolder, "*.sql")
                        .Select(f => new FileInfo(f))
                        .OrderByDescending(f => f.CreationTime)
                        .Skip(_retentionMonthly)
                        .ToList();

                    foreach (var f in files)
                    {
                        try { f.Delete(); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cleanup failed (non-fatal)");
            }
        }

        // ============================================================
        // وضعیت سلامت بکاپ
        // ============================================================
        public async Task<BackupStatusDto> GetStatusAsync()
        {
            var all = await _context.BackupLogs
                .OrderByDescending(b => b.BackupDate)
                .ToListAsync();

            var lastSuccess = all.FirstOrDefault(b => b.Status == "Success");
            var lastAttempt = all.FirstOrDefault();

            var hoursSince = lastSuccess != null
                ? (DateTime.Now - lastSuccess.BackupDate).TotalHours
                : (double?)null;

            return new BackupStatusDto
            {
                TotalBackupCount = all.Count(b => b.Status == "Success"),
                LastSuccessDate = lastSuccess?.BackupDate,
                LastAttemptDate = lastAttempt?.BackupDate,
                LastAttemptStatus = lastAttempt?.Status,
                LastAttemptError = lastAttempt?.ErrorMessage,
                HoursSinceLastSuccess = hoursSince,
                TotalSizeBytes = all.Where(b => b.Status == "Success").Sum(b => b.FileSizeBytes),
                IsHealthy = lastSuccess != null && hoursSince < 48
            };
        }

        public async Task<List<BackupLog>> GetHistoryAsync(int take = 100)
        {
            return await _context.BackupLogs
                .OrderByDescending(b => b.BackupDate)
                .Take(take)
                .ToListAsync();
        }

        public async Task<bool> DeleteBackupAsync(int backupLogId)
        {
            var log = await _context.BackupLogs.FindAsync(backupLogId);
            if (log == null) return false;

            if (log.Status == "Success" && !string.IsNullOrEmpty(log.FilePath))
            {
                TryDeleteFile(log.FilePath);
            }

            _context.BackupLogs.Remove(log);
            await _context.SaveChangesAsync();
            return true;
        }

        public bool HasBackupToday()
        {
            var today = DateTime.Today;
            return _context.BackupLogs
                .Any(b => b.Status == "Success" && b.BackupDate >= today);
        }
    }
}