using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace ManageAdTool.Services;

public class AuthAuditLogger
{
    private readonly string _path;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public AuthAuditLogger(string referenceLogPath)
    {
        var dir = Path.GetDirectoryName(referenceLogPath) ?? string.Empty;
        _path = Path.Combine(dir, "auth.jsonl");
    }

    public void Log(string action, string user, bool success, string message = "")
    {
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

            var entry = new
            {
                timestamp = DateTimeOffset.Now,
                action,
                user,
                success,
                message
            };
            File.AppendAllText(_path, JsonSerializer.Serialize(entry, _jsonOptions) + Environment.NewLine);
        }
        catch
        {
            // 認証ログの書き込み失敗で認証操作を妨げない。
        }
    }
}
