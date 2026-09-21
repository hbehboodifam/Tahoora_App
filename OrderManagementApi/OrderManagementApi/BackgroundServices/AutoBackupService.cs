using OrderManagementApi.Services;

namespace OrderManagementApi.BackgroundServices
{
    public class AutoBackupService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly IConfiguration _config;
        private readonly ILogger<AutoBackupService> _logger;

        public AutoBackupService(
            IServiceProvider services,
            IConfiguration config,
            ILogger<AutoBackupService> logger)
        {
            _services = services;
            _config = config;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var enabled = _config["BackupSettings:Enabled"] == "true";
            if (!enabled)
            {
                _logger.LogInformation("Backup is disabled in config.");
                return;
            }

            // ===== یک دقیقه صبر کن تا API کامل بالا بیاد =====
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

            // ===== اولین اجرا: چک کن امروز بکاپ گرفتیم یا نه =====
            await TryBackupIfNeeded("startup");

            var intervalHours = int.TryParse(
                _config["BackupSettings:CheckIntervalHours"], out var h) ? h : 6;

            // ===== حلقه‌ی دوره‌ای =====
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromHours(intervalHours), stoppingToken);
                    await TryBackupIfNeeded("scheduled");
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Auto backup loop error");
                }
            }
        }

        private async Task TryBackupIfNeeded(string reason)
        {
            try
            {
                using var scope = _services.CreateScope();
                var backupService = scope.ServiceProvider.GetRequiredService<BackupService>();

                if (backupService.HasBackupToday())
                {
                    _logger.LogInformation("Backup already done today ({Reason}).", reason);
                    return;
                }

                _logger.LogInformation("Taking auto backup ({Reason})...", reason);
                var result = await backupService.CreateBackupAsync("auto");

                if (result.Success)
                    _logger.LogInformation("Backup success: {Path}", result.FilePath);
                else
                    _logger.LogWarning("Backup failed: {Error}", result.ErrorMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Backup attempt failed");
            }
        }
    }
}