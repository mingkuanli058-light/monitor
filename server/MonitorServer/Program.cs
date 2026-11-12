using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using MonitorServer.Data;
using MonitorServer.Models;
using MonitorServer.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=monitor.db"));

builder.Services.AddScoped<AgentIngestService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.AccessDeniedPath = "/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = options.DefaultPolicy; // 默认所有页面需登录
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    DataSeeder.Seed(db);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

// ---- 终端代理上报接口 ----
// 可选的简单共享密钥校验：appsettings.json 中配置 Agent:ApiKey 后，代理需在请求头 X-Agent-Key 中携带相同值。
// 留空（默认）则不校验，便于内网测试。
var agentApi = app.MapGroup("/api/agent").AllowAnonymous();

bool CheckApiKey(HttpRequest request)
{
    var expected = app.Configuration["Agent:ApiKey"];
    if (string.IsNullOrEmpty(expected)) return true;
    return request.Headers.TryGetValue("X-Agent-Key", out var got) && got == expected;
}

agentApi.MapGet("/config", (string hostName, HttpRequest req, AgentIngestService svc) =>
{
    if (!CheckApiKey(req)) return Results.Unauthorized();
    var device = svc.GetOrCreateDevice(hostName);
    var settings = svc.GetSettings();
    return Results.Ok(new
    {
        deviceId = device.Id,
        idleThresholdMinutes = settings.IdleThresholdMinutes,
        foregroundTrackingEnabled = device.ForegroundTrackingEnabled
    });
});

agentApi.MapPost("/heartbeat", (HeartbeatRequest body, HttpRequest req, AgentIngestService svc) =>
{
    if (!CheckApiKey(req)) return Results.Unauthorized();
    svc.Heartbeat(body.HostName, body.Time);
    return Results.Ok();
});

agentApi.MapPost("/event", (EventRequest body, HttpRequest req, AgentIngestService svc) =>
{
    if (!CheckApiKey(req)) return Results.Unauthorized();
    if (!Enum.TryParse<EventType>(body.Type, true, out var type)) return Results.BadRequest("未知事件类型");
    svc.RecordEvent(body.HostName, type, body.Time, body.Account);
    return Results.Ok();
});

agentApi.MapPost("/minute", (MinuteRequest body, HttpRequest req, AgentIngestService svc) =>
{
    if (!CheckApiKey(req)) return Results.Unauthorized();
    svc.RecordMinute(body.HostName, body.MinuteStart, body.IsActive, body.Locked, body.App);
    return Results.Ok();
});

app.Run();

record HeartbeatRequest(string HostName, DateTime Time);
record EventRequest(string HostName, string Type, DateTime Time, string? Account);
record MinuteRequest(string HostName, DateTime MinuteStart, bool IsActive, bool Locked, string? App);
