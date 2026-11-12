using Microsoft.AspNetCore.Mvc.RazorPages;
using MonitorServer.Data;
using MonitorServer.Models;

namespace MonitorServer.Pages;

public class TimelineModel : PageModel
{
    private readonly AppDbContext _db;
    public TimelineModel(AppDbContext db) => _db = db;

    public List<Device> Devices { get; set; } = new();
    public int DeviceId { get; set; }
    public DateOnly Date { get; set; }
    public Device? Selected { get; set; }
    public DailySummary? Summary { get; set; }
    public List<Segment> Segments { get; set; } = new();
    public List<AppUsage> AppUsages { get; set; } = new();

    public void OnGet(int? deviceId, DateOnly? date)
    {
        Devices = _db.Devices.OrderBy(d => d.Id).ToList();
        Date = date ?? DateOnly.FromDateTime(DateTime.Now);
        DeviceId = deviceId ?? Devices.FirstOrDefault()?.Id ?? 0;
        Selected = Devices.FirstOrDefault(d => d.Id == DeviceId);

        if (Selected == null) return;

        Summary = _db.DailySummaries.FirstOrDefault(s => s.DeviceId == DeviceId && s.Date == Date);
        Segments = _db.Segments.Where(s => s.DeviceId == DeviceId && s.Date == Date)
                                .OrderBy(s => s.Start).ToList();
        AppUsages = _db.AppUsages.Where(a => a.DeviceId == DeviceId && a.Date == Date)
                                  .OrderByDescending(a => a.DurationMinutes).ToList();
    }

    public int TotalMinutes => Segments.Count == 0 ? 1 : (int)(Segments.Last().End - Segments.First().Start).TotalMinutes;
}
