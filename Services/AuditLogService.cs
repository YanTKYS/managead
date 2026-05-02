using System.Text.Json;
using ManageAdTool.Models;

namespace ManageAdTool.Services;

public class AuditLogService
{
    private readonly string _path;

    public AuditLogService(string path)
    {
        _path = path;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
    }

    public void Write(string executor, ChangeSet changeSet, bool success, string? error = null)
    {
        var record = new
        {
            timestamp = DateTimeOffset.UtcNow,
            executor,
            target = changeSet.TargetSamAccountName,
            changes = changeSet.Changes,
            result = success ? "Success" : "Failed",
            error
        };
        File.AppendAllText(_path, JsonSerializer.Serialize(record) + Environment.NewLine);
    }
}
