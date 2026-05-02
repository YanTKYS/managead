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
        => WriteExtended(Guid.NewGuid().ToString("N"), executor, Environment.MachineName, changeSet.TargetSamAccountName, "LegacyOperation", changeSet.Changes, success, error);

    public void WriteExtended(string operationId, string executor, string machineName, string targetDn, string operationName, IEnumerable<FieldChange> changes, bool success, string? error = null)
    {
        var before = changes.ToDictionary(c => c.Field, c => c.Before);
        var after = changes.ToDictionary(c => c.Field, c => c.After);
        var record = new
        {
            timestamp = DateTimeOffset.UtcNow,
            operationId,
            executor,
            machineName,
            targetDn,
            operationName,
            before,
            after,
            success,
            error
        };
        File.AppendAllText(_path, JsonSerializer.Serialize(record) + Environment.NewLine);
    }
}
