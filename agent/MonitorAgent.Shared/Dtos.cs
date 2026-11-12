namespace MonitorAgent.Shared;

public record HeartbeatDto(string HostName, DateTime Time);
public record EventDto(string HostName, string Type, DateTime Time, string? Account);
public record MinuteDto(string HostName, DateTime MinuteStart, bool IsActive, bool Locked, string? App);
public record AgentConfigResponse(int DeviceId, int IdleThresholdMinutes, bool ForegroundTrackingEnabled);
