using Microsoft.Win32;
using MonitorAgent.Shared;

namespace MonitorAgent.Service;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly AgentConfig _config;
    private readonly Reporter _reporter;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
        _config = AgentConfig.Load();
        _reporter = new Reporter(_config, "service-queue.json");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 用系统启动以来的运行时间反推准确的开机时间，比用服务启动时间更接近真实开机时刻
        var bootTime = DateTime.Now - TimeSpan.FromMilliseconds(Environment.TickCount64);
        await ReportEventAsync("Boot", bootTime);

        SystemEvents.PowerModeChanged += OnPowerModeChanged;

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await _reporter.SendAsync("/api/agent/heartbeat", new HeartbeatDto(_config.HostName, DateTime.Now));
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }
        finally
        {
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        // 服务被停止通常对应关机或手动停止服务，尽力上报一次关机事件（时间有限，不保证一定成功）
        await ReportEventAsync("Shutdown", DateTime.Now);
        await base.StopAsync(cancellationToken);
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend)
        {
            _ = ReportEventAsync("Sleep", DateTime.Now);
        }
        else if (e.Mode == PowerModes.Resume)
        {
            _ = ReportEventAsync("Wake", DateTime.Now);
        }
    }

    private async Task ReportEventAsync(string type, DateTime time)
    {
        try
        {
            await _reporter.SendAsync("/api/agent/event", new EventDto(_config.HostName, type, time, Environment.UserName));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "上报事件 {Type} 失败", type);
        }
    }
}
