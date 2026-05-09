using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace ManageAdTool.Services;

public class ReferenceAuditLogger
{
    private readonly string _path;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public ReferenceAuditLogger(string path)
    {
        _path = path;
    }

    public void Log(string action, string target, int resultCount, bool success, string message = "")
    {
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

            var entry = new
            {
                timestamp = DateTimeOffset.Now,
                action,
                target,
                resultCount,
                success,
                message
            };
            File.AppendAllText(_path, JsonSerializer.Serialize(entry, _jsonOptions) + Environment.NewLine);
        }
        catch
        {
            // 参照ログの書き込み失敗で参照操作を妨げない。
        }
    }

    public void LogOperationPlan(string executor, string targetSam, int attrChangeCount, int groupAddCount, int groupRemoveCount)
    {
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

            var entry = new
            {
                timestamp = DateTimeOffset.Now,
                action = "OperationPlanCreated",
                executor,
                targetUser = targetSam,
                plannedAttributeChanges = attrChangeCount,
                plannedGroupAdds = groupAddCount,
                plannedGroupRemoves = groupRemoveCount,
            };
            File.AppendAllText(_path, JsonSerializer.Serialize(entry, _jsonOptions) + Environment.NewLine);
        }
        catch
        {
            // 参照ログの書き込み失敗で参照操作を妨げない。
        }
    }
}
