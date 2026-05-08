using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using ManageAdTool.Models;

namespace ManageAdTool.Services;

public class WriteAuditLogger
{
    private const string AppVersion = "0.4.0";
    private readonly string _path;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public WriteAuditLogger(string referenceLogPath)
    {
        var dir = Path.GetDirectoryName(referenceLogPath) ?? string.Empty;
        _path = Path.Combine(dir, "write-audit.jsonl");
    }

    public void Log(WriteAuditEntry entry)
    {
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

            var serializable = new
            {
                timestamp = DateTimeOffset.Now,
                operationId = entry.OperationId,
                appVersion = AppVersion,
                serviceMode = entry.ServiceMode,
                executor = entry.Executor,
                machineName = entry.MachineName,
                editorUser = entry.EditorUser,
                targetSamAccountName = entry.TargetSamAccountName,
                targetDn = entry.TargetDn,
                operationName = "UpdateUserAttributes",
                changes = entry.Changes.Select(c => new { field = c.Field, before = c.Before, after = c.After }).ToList(),
                success = entry.Success,
                error = entry.Error,
                verifiedAfterUpdate = entry.VerifiedAfterUpdate,
                allowedTargetOuMatched = entry.AllowedTargetOuMatched,
                excludedAccountMatched = entry.ExcludedAccountMatched
            };
            File.AppendAllText(_path, JsonSerializer.Serialize(serializable, _jsonOptions) + Environment.NewLine);
        }
        catch
        {
            // 書き込み監査ログの失敗で更新処理を妨げない。
        }
    }
}
