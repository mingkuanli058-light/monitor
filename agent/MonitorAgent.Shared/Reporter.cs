using System.Net.Http.Json;
using System.Text.Json;

namespace MonitorAgent.Shared;

// 负责向服务端上报数据；断网时写入本地队列文件缓存，联网后按顺序补报。
public class Reporter
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private readonly string _queuePath;
    private readonly object _lock = new();

    public Reporter(AgentConfig config, string queueFileName)
    {
        _http.BaseAddress = new Uri(config.ServerUrl);
        if (!string.IsNullOrEmpty(config.ApiKey))
        {
            _http.DefaultRequestHeaders.Add("X-Agent-Key", config.ApiKey);
        }
        Directory.CreateDirectory(AgentConfig.ConfigDir);
        _queuePath = Path.Combine(AgentConfig.ConfigDir, queueFileName);
    }

    public async Task SendAsync(string path, object payload)
    {
        Enqueue(path, payload);
        await FlushAsync();
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<T?> GetAsync<T>(string path)
    {
        try
        {
            return await _http.GetFromJsonAsync<T>(path, JsonOptions);
        }
        catch
        {
            return default;
        }
    }

    private void Enqueue(string path, object payload)
    {
        lock (_lock)
        {
            var items = ReadQueue();
            items.Add(new QueueItem(path, JsonSerializer.Serialize(payload)));
            File.WriteAllText(_queuePath, JsonSerializer.Serialize(items));
        }
    }

    public async Task FlushAsync()
    {
        List<QueueItem> items;
        lock (_lock)
        {
            items = ReadQueue();
        }
        if (items.Count == 0) return;

        var remaining = new List<QueueItem>(items);
        foreach (var item in items)
        {
            try
            {
                var content = new StringContent(item.PayloadJson, System.Text.Encoding.UTF8, "application/json");
                var resp = await _http.PostAsync(item.Path, content);
                if (!resp.IsSuccessStatusCode) break; // 保持顺序，失败则停止，等下次再试
                remaining.Remove(item);
            }
            catch
            {
                break; // 离线，停止尝试剩余项
            }
        }

        lock (_lock)
        {
            File.WriteAllText(_queuePath, JsonSerializer.Serialize(remaining));
        }
    }

    private List<QueueItem> ReadQueue()
    {
        try
        {
            if (!File.Exists(_queuePath)) return new();
            var json = File.ReadAllText(_queuePath);
            return JsonSerializer.Deserialize<List<QueueItem>>(json) ?? new();
        }
        catch
        {
            return new();
        }
    }

    private record QueueItem(string Path, string PayloadJson);
}
