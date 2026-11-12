using System.Text.Json;

namespace MonitorAgent.Shared;

public class AgentConfig
{
    public string ServerUrl { get; set; } = "http://localhost:5208";
    public string? ApiKey { get; set; }
    public string HostName { get; set; } = Environment.MachineName;

    public static string ConfigDir => @"C:\ProgramData\MonitorAgent";
    public static string ConfigPath => Path.Combine(ConfigDir, "agent.config.json");

    public static AgentConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<AgentConfig>(json);
                if (cfg != null) return cfg;
            }
        }
        catch
        {
            // 配置文件读取失败时使用默认值，避免代理因配置问题整体崩溃
        }
        return new AgentConfig();
    }

    public void Save()
    {
        Directory.CreateDirectory(ConfigDir);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
