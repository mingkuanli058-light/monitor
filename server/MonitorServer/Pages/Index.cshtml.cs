using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MonitorServer.Data;
using MonitorServer.Models;

namespace MonitorServer.Pages;

public class DeviceStatusRow
{
    public int DeviceId { get; set; }
    public string Code { get; set; } = "";
    public string UserName { get; set; } = "";
    public string Status { get; set; } = "off"; // active / idle / locked / off / offline
    public string StatusText { get; set; } = "";
    public int OnMinutesToday { get; set; }
    public int ActiveMinutesToday { get; set; }
}

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) => _db = db;

    public List<DeviceStatusRow> Rows { get; set; } = new();
    public int OnlineCount { get; set; }
    public int ActiveCount { get; set; }

    public void OnGet()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var now = DateTime.Now;

        var devices = _db.Devices.Where(d => d.Enabled).OrderBy(d => d.Id).ToList();
        var summaries = _db.DailySummaries.Where(s => s.Date == today).ToDictionary(s => s.DeviceId);
        var segments = _db.Segments.Where(s => s.Date == today).ToList();

        foreach (var device in devices)
        {
            var row = new DeviceStatusRow { DeviceId = device.Id, Code = device.Code, UserName = device.UserName };

            bool offline = device.LastHeartbeat == null || (now - device.LastHeartbeat.Value) > TimeSpan.FromMinutes(10);
            var summary = summaries.GetValueOrDefault(device.Id);

            if (summary != null)
            {
                row.OnMinutesToday = summary.ActiveMinutes + summary.IdleMinutes;
                row.ActiveMinutesToday = summary.ActiveMinutes;
            }

            if (offline)
            {
                row.Status = "offline";
                row.StatusText = "离线";
            }
            else if (summary == null || summary.BootTime == null || now < summary.BootTime || (summary.ShutdownTime != null && now > summary.ShutdownTime))
            {
                row.Status = "off";
                row.StatusText = "关机";
            }
            else
            {
                var seg = segments.Where(s => s.DeviceId == device.Id && s.Start <= now && now < s.End)
                                   .OrderByDescending(s => s.Start).FirstOrDefault();
                if (seg == null)
                {
                    row.Status = "off";
                    row.StatusText = "关机";
                }
                else if (seg.Type == SegmentType.Active)
                {
                    row.Status = "active";
                    row.StatusText = "干活";
                }
                else if (seg.IsLocked)
                {
                    row.Status = "locked";
                    row.StatusText = "锁屏";
                }
                else
                {
                    row.Status = "idle";
                    row.StatusText = "空闲";
                }
            }

            Rows.Add(row);
        }

        OnlineCount = Rows.Count(r => r.Status != "offline");
        ActiveCount = Rows.Count(r => r.Status == "active");
    }
}
