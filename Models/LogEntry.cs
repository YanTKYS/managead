namespace ManageAdTool.Models;

public class LogEntry
{
    public DateTimeOffset? Timestamp { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string Executor { get; set; } = string.Empty;
    public string EditorUser { get; set; } = string.Empty;
    public bool? Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public int ChangesCount { get; set; }
    public string RawJson { get; set; } = string.Empty;
    public bool IsParseError { get; set; }

    public string TimestampDisplay =>
        Timestamp.HasValue ? Timestamp.Value.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss") : string.Empty;

    public string SuccessDisplay => IsParseError
        ? "(解析エラー)"
        : Success.HasValue ? (Success.Value ? "○" : "✕") : "-";

    public string ExecutorDisplay =>
        !string.IsNullOrEmpty(EditorUser) ? EditorUser : Executor;

    public string DetailSummary =>
        ChangesCount > 0 ? $"変更{ChangesCount}件" : Message;
}
