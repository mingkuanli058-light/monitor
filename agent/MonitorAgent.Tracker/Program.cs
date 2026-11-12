using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using MonitorAgent.Shared;

var config = AgentConfig.Load();
var reporter = new Reporter(config, "tracker-queue.json");

int idleThresholdMinutes = 5;
bool foregroundTrackingEnabled = true;
bool locked = false;

var cts = new CancellationTokenSource();

SystemEvents.SessionSwitch += (_, e) =>
{
    switch (e.Reason)
    {
        case SessionSwitchReason.SessionLock:
            locked = true;
            _ = ReportEvent("Lock");
            break;
        case SessionSwitchReason.SessionUnlock:
            locked = false;
            _ = ReportEvent("Unlock");
            break;
        case SessionSwitchReason.SessionLogoff:
            _ = ReportEvent("Logout").ContinueWith(_ => cts.Cancel());
            break;
    }
};

AppDomain.CurrentDomain.ProcessExit += (_, _) => reporter.FlushAsync().Wait(TimeSpan.FromSeconds(3));

await ReportEvent("Login");

// 每 30 秒采样一次，攒够一分钟后把这一分钟的"是否干活/前台程序"上报一次
var samplesThisMinute = new List<(bool IsActive, string? App)>();
var refreshTimer = Stopwatch.StartNew();

while (!cts.Token.IsCancellationRequested)
{
    try
    {
        if (refreshTimer.Elapsed > TimeSpan.FromMinutes(5))
        {
            var cfg = await reporter.GetAsync<AgentConfigResponse>($"/api/agent/config?hostName={Uri.EscapeDataString(config.HostName)}");
            if (cfg != null)
            {
                idleThresholdMinutes = cfg.IdleThresholdMinutes;
                foregroundTrackingEnabled = cfg.ForegroundTrackingEnabled;
            }
            refreshTimer.Restart();
        }

        var idleSeconds = NativeInput.GetIdleSeconds();
        bool isActive = !locked && idleSeconds < idleThresholdMinutes * 60;
        string? app = (isActive && foregroundTrackingEnabled) ? NativeInput.GetForegroundAppName() : null;
        samplesThisMinute.Add((isActive, app));

        if (samplesThisMinute.Count >= 2) // 2 * 30秒 = 1分钟
        {
            var minuteActive = samplesThisMinute.Any(s => s.IsActive);
            var minuteApp = samplesThisMinute.LastOrDefault(s => s.App != null).App;
            var minuteStart = FloorToMinute(DateTime.Now.AddMinutes(-1));
            await reporter.SendAsync("/api/agent/minute", new MinuteDto(config.HostName, minuteStart, minuteActive, locked, minuteApp));
            samplesThisMinute.Clear();
        }

        await Task.Delay(TimeSpan.FromSeconds(30), cts.Token);
    }
    catch (TaskCanceledException)
    {
        break;
    }
    catch
    {
        // 单次循环异常不应导致整个 Tracker 退出
    }
}

async Task ReportEvent(string type)
{
    try
    {
        await reporter.SendAsync("/api/agent/event", new EventDto(config.HostName, type, DateTime.Now, Environment.UserName));
    }
    catch
    {
        // 忽略单次上报失败，依赖本地队列重试
    }
}

static DateTime FloorToMinute(DateTime time) => new(time.Year, time.Month, time.Day, time.Hour, time.Minute, 0);

static class NativeInput
{
    public static int GetIdleSeconds()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref info)) return 0;
        var idleMs = (uint)Environment.TickCount - info.dwTime;
        return (int)(idleMs / 1000);
    }

    public static string? GetForegroundAppName()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return null;
            GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == 0) return null;
            using var process = Process.GetProcessById((int)pid);
            return process.ProcessName;
        }
        catch
        {
            return null;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
}
