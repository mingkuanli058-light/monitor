namespace MonitorServer.Models;

public class Device
{
    public int Id { get; set; }
    public string Code { get; set; } = "";       // 如 "3号电脑"
    public string UserName { get; set; } = "";   // 使用人
    public string? HostName { get; set; }        // 代理上报时用的 Windows 主机名，用于自动识别电脑
    public DateTime? LastHeartbeat { get; set; }
    public bool ForegroundTrackingEnabled { get; set; } = true;
    public bool Enabled { get; set; } = true;

    public ICollection<EventLog> Events { get; set; } = new List<EventLog>();
    public ICollection<Segment> Segments { get; set; } = new List<Segment>();
    public ICollection<AppUsage> AppUsages { get; set; } = new List<AppUsage>();
}

public enum EventType
{
    Boot, Shutdown, Restart, PowerLoss, Sleep, Wake,
    Login, Logout, Lock, Unlock
}

public class EventLog
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    public Device? Device { get; set; }
    public EventType Type { get; set; }
    public DateTime Time { get; set; }
    public string? Account { get; set; } // Windows 登录账户名
}

public enum SegmentType
{
    Active,   // 干活
    Idle      // 空闲（含锁屏/睡眠/未登录）
}

public class Segment
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    public Device? Device { get; set; }
    public DateOnly Date { get; set; }
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public SegmentType Type { get; set; }
    public bool IsLocked { get; set; } // 空闲原因是否为锁屏/睡眠
}

public class AppUsage
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    public Device? Device { get; set; }
    public DateOnly Date { get; set; }
    public string AppName { get; set; } = "";
    public int DurationMinutes { get; set; }
}

public class DailySummary
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    public Device? Device { get; set; }
    public DateOnly Date { get; set; }
    public DateTime? BootTime { get; set; }
    public DateTime? ShutdownTime { get; set; }
    public int OnMinutes { get; set; }
    public int ActiveMinutes { get; set; }
    public int IdleMinutes { get; set; }
    public bool HasAfterHoursActivity { get; set; } // 下班时间内有开机/干活
}

public class AdminUser
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
}

public class AuditLog
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string Action { get; set; } = "";
    public DateTime Time { get; set; }
}

public class SystemSetting
{
    public int Id { get; set; } = 1;
    public TimeOnly WorkStart { get; set; } = new TimeOnly(9, 0);
    public TimeOnly WorkEnd { get; set; } = new TimeOnly(18, 0);
    public int IdleThresholdMinutes { get; set; } = 5;
    public int DataRetentionDays { get; set; } = 365;
}
