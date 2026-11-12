using MonitorServer.Data;
using MonitorServer.Models;

namespace MonitorServer.Services;

public class AgentIngestService
{
    private readonly AppDbContext _db;
    public AgentIngestService(AppDbContext db) => _db = db;

    public Device GetOrCreateDevice(string hostName)
    {
        var device = _db.Devices.FirstOrDefault(d => d.HostName == hostName);
        if (device != null) return device;

        device = new Device
        {
            Code = hostName,
            UserName = "",
            HostName = hostName,
            Enabled = true,
            ForegroundTrackingEnabled = true
        };
        _db.Devices.Add(device);
        _db.SaveChanges();
        return device;
    }

    public SystemSetting GetSettings() => _db.SystemSettings.FirstOrDefault() ?? new SystemSetting();

    public void Heartbeat(string hostName, DateTime time)
    {
        var device = GetOrCreateDevice(hostName);
        device.LastHeartbeat = time;
        _db.SaveChanges();
    }

    public void RecordEvent(string hostName, EventType type, DateTime time, string? account)
    {
        var device = GetOrCreateDevice(hostName);
        device.LastHeartbeat = time;
        _db.EventLogs.Add(new EventLog { DeviceId = device.Id, Type = type, Time = time, Account = account });

        var date = DateOnly.FromDateTime(time);
        var summary = GetOrCreateSummary(device.Id, date);

        if (type == EventType.Boot)
        {
            summary.BootTime = time;
        }
        else if (type == EventType.Shutdown)
        {
            summary.ShutdownTime = time;
            CloseOpenSegment(device.Id, date, time);
        }

        _db.SaveChanges();
    }

    // 代理每分钟上报一次该分钟内的活动状态和前台程序，服务端据此累加分段、程序使用时长与当日汇总
    public void RecordMinute(string hostName, DateTime minuteStart, bool isActive, bool locked, string? app)
    {
        var device = GetOrCreateDevice(hostName);
        device.LastHeartbeat = minuteStart;
        var date = DateOnly.FromDateTime(minuteStart);
        var minuteEnd = minuteStart.AddMinutes(1);
        var type = isActive ? SegmentType.Active : SegmentType.Idle;

        var last = _db.Segments.Where(s => s.DeviceId == device.Id && s.Date == date)
                                .OrderByDescending(s => s.Start).FirstOrDefault();

        if (last != null && last.Type == type && last.IsLocked == locked && last.End >= minuteStart.AddSeconds(-5))
        {
            last.End = minuteEnd;
        }
        else
        {
            _db.Segments.Add(new Segment
            {
                DeviceId = device.Id,
                Date = date,
                Start = minuteStart,
                End = minuteEnd,
                Type = type,
                IsLocked = locked
            });
        }

        if (isActive && !string.IsNullOrWhiteSpace(app) && device.ForegroundTrackingEnabled)
        {
            var usage = _db.AppUsages.FirstOrDefault(a => a.DeviceId == device.Id && a.Date == date && a.AppName == app);
            if (usage == null)
            {
                usage = new AppUsage { DeviceId = device.Id, Date = date, AppName = app, DurationMinutes = 0 };
                _db.AppUsages.Add(usage);
            }
            usage.DurationMinutes += 1;
        }

        var summary = GetOrCreateSummary(device.Id, date);
        summary.BootTime ??= minuteStart;
        if (isActive) summary.ActiveMinutes += 1; else summary.IdleMinutes += 1;
        summary.OnMinutes = summary.ActiveMinutes + summary.IdleMinutes;

        var settings = GetSettings();
        if (minuteStart.TimeOfDay < settings.WorkStart.ToTimeSpan() || minuteStart.TimeOfDay > settings.WorkEnd.ToTimeSpan())
        {
            summary.HasAfterHoursActivity = true;
        }

        _db.SaveChanges();
    }

    private void CloseOpenSegment(int deviceId, DateOnly date, DateTime time)
    {
        var last = _db.Segments.Where(s => s.DeviceId == deviceId && s.Date == date)
                                .OrderByDescending(s => s.Start).FirstOrDefault();
        if (last != null && last.End < time) last.End = time;
    }

    private DailySummary GetOrCreateSummary(int deviceId, DateOnly date)
    {
        var summary = _db.DailySummaries.FirstOrDefault(s => s.DeviceId == deviceId && s.Date == date);
        if (summary == null)
        {
            summary = new DailySummary { DeviceId = deviceId, Date = date };
            _db.DailySummaries.Add(summary);
        }
        return summary;
    }
}
