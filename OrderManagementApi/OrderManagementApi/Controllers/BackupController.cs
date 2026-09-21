using Microsoft.AspNetCore.Mvc;
using OrderManagementApi.Services;

namespace OrderManagementApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BackupController : ControllerBase
{
    private readonly BackupService _backup;

    public BackupController(BackupService backup)
    {
        _backup = backup;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        return Ok(await _backup.GetStatusAsync());
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromQuery] int take = 100)
    {
        return Ok(await _backup.GetHistoryAsync(take));
    }

    [HttpPost("create")]
    public async Task<IActionResult> CreateManual()
    {
        var result = await _backup.CreateBackupAsync("manual");
        if (result.Success)
        {
            return Ok(new
            {
                message = "بکاپ با موفقیت گرفته شد.",
                filePath = result.FilePath,
                fileSize = result.FileSize
            });
        }
        return BadRequest(new { message = result.ErrorMessage });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await _backup.DeleteBackupAsync(id);
        return ok ? NoContent() : NotFound();
    }

    /// <summary>دانلود یه بکاپ</summary>
    [HttpGet("download/{id}")]
    public async Task<IActionResult> Download(int id)
    {
        var history = await _backup.GetHistoryAsync(1000);
        var log = history.FirstOrDefault(b => b.BackupLogId == id);

        if (log == null || log.Status != "Success" || string.IsNullOrEmpty(log.FilePath))
            return NotFound("بکاپ یافت نشد.");

        if (!System.IO.File.Exists(log.FilePath))
            return NotFound("فایل بکاپ روی دیسک پیدا نشد.");

        var bytes = await System.IO.File.ReadAllBytesAsync(log.FilePath);
        var fileName = Path.GetFileName(log.FilePath);
        return File(bytes, "application/sql", fileName);
    }
}