using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using ManageAdTool.Models;

namespace ManageAdTool.Services;

public static class LogReader
{
    public static IReadOnlyList<LogEntry> ReadLog(string filePath, int maxRows)
    {
        if (!File.Exists(filePath))
            return Array.Empty<LogEntry>();

        // 循環バッファで末尾 maxRows 行だけ保持する（巨大ログでも全行メモリ展開しない）
        var buffer = new string[maxRows];
        int written = 0;

        foreach (var line in File.ReadLines(filePath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            buffer[written % maxRows] = line;
            written++;
        }

        var count = Math.Min(written, maxRows);
        var start = written <= maxRows ? 0 : written % maxRows;
        var result = new List<LogEntry>(count);
        for (var i = 0; i < count; i++)
            result.Add(ParseLine(buffer[(start + i) % maxRows]));

        return result;
    }

    public static string FormatAndMaskJson(string rawJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var formatted = JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            return MaskPasswordFields(formatted);
        }
        catch
        {
            return rawJson;
        }
    }

    private static LogEntry ParseLine(string line)
    {
        var entry = new LogEntry { RawJson = line };
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            ParseEntry(root, entry);
        }
        catch
        {
            entry.IsParseError = true;
            entry.Action = "(解析エラー)";
            entry.Message = line.Length > 120 ? line[..120] + "..." : line;
        }
        return entry;
    }

    private static void ParseEntry(JsonElement root, LogEntry entry)
    {
        if (root.TryGetProperty("timestamp", out var ts))
        {
            if (DateTimeOffset.TryParse(ts.GetString(), out var dto))
                entry.Timestamp = dto;
        }

        entry.Action = GetString(root, "action") ?? GetString(root, "operationName") ?? string.Empty;
        entry.Target = GetString(root, "target")
            ?? GetString(root, "targetName")
            ?? GetString(root, "targetUser")
            ?? GetString(root, "targetSamAccountName")
            ?? string.Empty;
        entry.Executor = GetString(root, "executor") ?? GetString(root, "user") ?? string.Empty;
        entry.EditorUser = GetString(root, "editorUser") ?? string.Empty;
        entry.TargetType = GetString(root, "targetType") ?? string.Empty;
        entry.Message = GetString(root, "message") ?? GetString(root, "error") ?? string.Empty;

        if (root.TryGetProperty("success", out var suc))
        {
            if (suc.ValueKind == JsonValueKind.True) entry.Success = true;
            else if (suc.ValueKind == JsonValueKind.False) entry.Success = false;
        }

        if (root.TryGetProperty("changes", out var changes) && changes.ValueKind == JsonValueKind.Array)
            entry.ChangesCount = changes.GetArrayLength();
    }

    private static string? GetString(JsonElement root, string key)
    {
        if (root.TryGetProperty(key, out var val) && val.ValueKind == JsonValueKind.String)
            return val.GetString();
        return null;
    }

    private static string MaskPasswordFields(string json)
    {
        return Regex.Replace(json,
            @"(""password""\s*:\s*)""[^""]*""",
            @"$1""***""",
            RegexOptions.IgnoreCase);
    }
}
