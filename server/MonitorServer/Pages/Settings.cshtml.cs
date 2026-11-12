using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MonitorServer.Data;
using MonitorServer.Models;

namespace MonitorServer.Pages;

public class SettingsModel : PageModel
{
    private readonly AppDbContext _db;
    public SettingsModel(AppDbContext db) => _db = db;

    public SystemSetting Setting { get; set; } = new();
    public List<Device> Devices { get; set; } = new();
    public string? Message { get; set; }

    public void OnGet()
    {
        Setting = _db.SystemSettings.FirstOrDefault() ?? new SystemSetting();
        Devices = _db.Devices.OrderBy(d => d.Id).ToList();
    }

    public IActionResult OnPostSettings(TimeOnly workStart, TimeOnly workEnd, int idleThresholdMinutes, int dataRetentionDays)
    {
        var setting = _db.SystemSettings.FirstOrDefault();
        if (setting == null)
        {
            setting = new SystemSetting();
            _db.SystemSettings.Add(setting);
        }
        setting.WorkStart = workStart;
        setting.WorkEnd = workEnd;
        setting.IdleThresholdMinutes = idleThresholdMinutes;
        setting.DataRetentionDays = dataRetentionDays;
        _db.AuditLogs.Add(new AuditLog { Username = User.Identity?.Name ?? "", Action = "修改系统设置", Time = DateTime.Now });
        _db.SaveChanges();

        return RedirectToPage(new { saved = 1 });
    }

    public IActionResult OnPostDevice(int deviceId, string code, string userName, bool foregroundTrackingEnabled)
    {
        var device = _db.Devices.FirstOrDefault(d => d.Id == deviceId);
        if (device != null)
        {
            device.Code = code;
            device.UserName = userName;
            device.ForegroundTrackingEnabled = foregroundTrackingEnabled;
            _db.AuditLogs.Add(new AuditLog { Username = User.Identity?.Name ?? "", Action = $"修改设备 {device.Code} 设置", Time = DateTime.Now });
            _db.SaveChanges();
        }
        return RedirectToPage(new { saved = 1 });
    }
}
