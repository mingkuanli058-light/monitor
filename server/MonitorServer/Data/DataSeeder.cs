using MonitorServer.Models;

namespace MonitorServer.Data;

public static class DataSeeder
{
    private static readonly string[] SampleApps = { "Word", "Excel", "Chrome", "微信", "PowerPoint", "记事本", "Outlook" };

    public static void Seed(AppDbContext db)
    {
        db.Database.EnsureCreated();

        if (!db.AdminUsers.Any())
        {
            db.AdminUsers.Add(new AdminUser
            {
                Username = "admin",
                PasswordHash = BCryptLite.Hash("admin123")
            });
        }

        if (!db.SystemSettings.Any())
        {
            db.SystemSettings.Add(new SystemSetting());
        }

        if (!db.Devices.Any())
        {
            var rnd = new Random(42);
            var devices = new List<Device>();
            for (int i = 1; i <= 15; i++)
            {
                devices.Add(new Device
                {
                    Code = $"{i}号电脑",
                    UserName = $"员工{i:D2}",
                    ForegroundTrackingEnabled = true,
                    Enabled = true,
                    LastHeartbeat = DateTime.Now.AddMinutes(-rnd.Next(0, 20))
                });
            }
            db.Devices.AddRange(devices);
            db.SaveChanges();

            var today = DateOnly.FromDateTime(DateTime.Now);
            foreach (var device in devices)
            {
                for (int dayOffset = 6; dayOffset >= 0; dayOffset--)
                {
                    var date = today.AddDays(-dayOffset);
                    if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                        continue;

                    GenerateDay(db, device, date, rnd);
                }
            }
            db.SaveChanges();
        }
    }

    private static void GenerateDay(AppDbContext db, Device device, DateOnly date, Random rnd)
    {
        var boot = date.ToDateTime(new TimeOnly(8, rnd.Next(30, 59)));
        var shutdown = date.ToDateTime(new TimeOnly(17, rnd.Next(30, 59))).AddHours(rnd.Next(0, 2));

        db.EventLogs.Add(new EventLog { DeviceId = device.Id, Type = EventType.Boot, Time = boot, Account = device.UserName });
        db.EventLogs.Add(new EventLog { DeviceId = device.Id, Type = EventType.Login, Time = boot.AddMinutes(1), Account = device.UserName });
        db.EventLogs.Add(new EventLog { DeviceId = device.Id, Type = EventType.Shutdown, Time = shutdown, Account = device.UserName });

        var cursor = boot;
        int activeTotal = 0, idleTotal = 0;
        var appMinutes = new Dictionary<string, int>();

        while (cursor < shutdown)
        {
            bool isActive = rnd.NextDouble() < 0.72;
            int segMinutes = rnd.Next(15, 90);
            var end = cursor.AddMinutes(segMinutes);
            if (end > shutdown) end = shutdown;

            db.Segments.Add(new Segment
            {
                DeviceId = device.Id,
                Date = date,
                Start = cursor,
                End = end,
                Type = isActive ? SegmentType.Active : SegmentType.Idle,
                IsLocked = !isActive && rnd.NextDouble() < 0.3
            });

            int minutes = (int)(end - cursor).TotalMinutes;
            if (isActive)
            {
                activeTotal += minutes;
                var app = SampleApps[rnd.Next(SampleApps.Length)];
                appMinutes[app] = appMinutes.GetValueOrDefault(app) + minutes;
            }
            else
            {
                idleTotal += minutes;
            }

            cursor = end;
        }

        foreach (var (app, minutes) in appMinutes)
        {
            db.AppUsages.Add(new AppUsage { DeviceId = device.Id, Date = date, AppName = app, DurationMinutes = minutes });
        }

        db.DailySummaries.Add(new DailySummary
        {
            DeviceId = device.Id,
            Date = date,
            BootTime = boot,
            ShutdownTime = shutdown,
            OnMinutes = (int)(shutdown - boot).TotalMinutes,
            ActiveMinutes = activeTotal,
            IdleMinutes = idleTotal,
            HasAfterHoursActivity = shutdown.TimeOfDay > new TimeSpan(18, 0, 0)
        });
    }
}

// 极简密码哈希（第一期内网工具，避免引入额外第三方包）；生产可替换为 BCrypt.Net-Next。
public static class BCryptLite
{
    public static string Hash(string password)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes("monitor-salt:" + password));
        return Convert.ToBase64String(bytes);
    }

    public static bool Verify(string password, string hash) => Hash(password) == hash;
}
