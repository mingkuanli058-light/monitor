using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MonitorServer.Data;
using MonitorServer.Models;

namespace MonitorServer.Pages;

public class ReportRow
{
    public int DeviceId { get; set; }
    public string Code { get; set; } = "";
    public string UserName { get; set; } = "";
    public int Days { get; set; }
    public int OnMinutes { get; set; }
    public int ActiveMinutes { get; set; }
    public int IdleMinutes { get; set; }
    public double ActiveRatio => OnMinutes == 0 ? 0 : (double)ActiveMinutes / OnMinutes * 100;
}

public class ReportsModel : PageModel
{
    private readonly AppDbContext _db;
    public ReportsModel(AppDbContext db) => _db = db;

    public string Period { get; set; } = "week"; // day / week / month
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public List<ReportRow> Rows { get; set; } = new();
    public List<(string App, int Minutes)> TopApps { get; set; } = new();

    public void OnGet(string? period)
    {
        Period = period ?? "week";
        var today = DateOnly.FromDateTime(DateTime.Now);
        (From, To) = ResolveRange(Period, today);
        LoadData();
    }

    public IActionResult OnGetExport(string? period)
    {
        Period = period ?? "week";
        var today = DateOnly.FromDateTime(DateTime.Now);
        (From, To) = ResolveRange(Period, today);
        LoadData();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("统计报表");
        ws.Cell(1, 1).Value = "电脑";
        ws.Cell(1, 2).Value = "使用人";
        ws.Cell(1, 3).Value = "统计天数";
        ws.Cell(1, 4).Value = "开机总时长(分)";
        ws.Cell(1, 5).Value = "干活时长(分)";
        ws.Cell(1, 6).Value = "空闲时长(分)";
        ws.Cell(1, 7).Value = "干活占比(%)";

        int r = 2;
        foreach (var row in Rows)
        {
            ws.Cell(r, 1).Value = row.Code;
            ws.Cell(r, 2).Value = row.UserName;
            ws.Cell(r, 3).Value = row.Days;
            ws.Cell(r, 4).Value = row.OnMinutes;
            ws.Cell(r, 5).Value = row.ActiveMinutes;
            ws.Cell(r, 6).Value = row.IdleMinutes;
            ws.Cell(r, 7).Value = Math.Round(row.ActiveRatio, 1);
            r++;
        }
        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        var fileName = $"统计报表_{From:yyyyMMdd}_{To:yyyyMMdd}.xlsx";
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    private void LoadData()
    {
        var summaries = _db.DailySummaries.Where(s => s.Date >= From && s.Date <= To).ToList();
        var devices = _db.Devices.OrderBy(d => d.Id).ToDictionary(d => d.Id);

        Rows = summaries.GroupBy(s => s.DeviceId)
            .Select(g => new ReportRow
            {
                DeviceId = g.Key,
                Code = devices.GetValueOrDefault(g.Key)?.Code ?? "",
                UserName = devices.GetValueOrDefault(g.Key)?.UserName ?? "",
                Days = g.Count(),
                OnMinutes = g.Sum(x => x.OnMinutes),
                ActiveMinutes = g.Sum(x => x.ActiveMinutes),
                IdleMinutes = g.Sum(x => x.IdleMinutes)
            })
            .OrderBy(r => r.Code)
            .ToList();

        TopApps = _db.AppUsages.Where(a => a.Date >= From && a.Date <= To)
            .ToList()
            .GroupBy(a => a.AppName)
            .Select(g => (g.Key, g.Sum(x => x.DurationMinutes)))
            .OrderByDescending(x => x.Item2)
            .Take(10)
            .ToList();
    }

    private static (DateOnly, DateOnly) ResolveRange(string period, DateOnly today) => period switch
    {
        "day" => (today, today),
        "month" => (new DateOnly(today.Year, today.Month, 1), today),
        _ => (today.AddDays(-(int)today.DayOfWeek + (today.DayOfWeek == DayOfWeek.Sunday ? -6 : 1)), today)
    };
}
