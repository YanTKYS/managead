using System.IO;
using System.Text.Json;
using ManageAdTool.Models;

namespace ManageAdTool.Services;

public static class AppPolicyProvider
{
    public static AppPolicy Load(string path = "appsettings.json")
    {
        if (!File.Exists(path)) return new AppPolicy();

        using var fs = File.OpenRead(path);
        using var doc = JsonDocument.Parse(fs);
        if (!doc.RootElement.TryGetProperty("AppPolicy", out var node)) return new AppPolicy();

        var d = new AppPolicy();

        return new AppPolicy
        {
            ServiceMode                     = ReadString(node, "ServiceMode",                     d.ServiceMode),
            LogPath                         = ReadString(node, "LogPath",                         d.LogPath),
            RetiredUsersOuDn                = ReadString(node, "RetiredUsersOuDn",                d.RetiredUsersOuDn),
            EditorAuthMode                  = ReadString(node, "EditorAuthMode",                  d.EditorAuthMode),
            AdminGroupDn                    = ReadString(node, "AdminGroupDn",                    d.AdminGroupDn),
            AllowNestedAdminGroupMembership = ReadBool  (node, "AllowNestedAdminGroupMembership", false),
            EditSessionMinutes              = ReadInt   (node, "EditSessionMinutes",              d.EditSessionMinutes,  minValue: 1),
            MaxSearchResults               = ReadInt   (node, "MaxSearchResults",               d.MaxSearchResults,   minValue: 1),
            MaxLogDisplayRows              = ReadInt   (node, "MaxLogDisplayRows",              d.MaxLogDisplayRows,  minValue: 1),
            EnableOperationSupport         = ReadBool  (node, "EnableOperationSupport",         d.EnableOperationSupport),

            AllowedTargetOuDns             = ReadStringList(node, "AllowedTargetOuDns"),
            ExcludedSamAccountNames        = ReadStringList(node, "ExcludedSamAccountNames"),
            EditableAttributes             = ReadStringListOrDefault(node, "EditableAttributes",             d.EditableAttributes),
            UserDetailDisplayAttributes    = ReadStringListOrDefault(node, "UserDetailDisplayAttributes",    d.UserDetailDisplayAttributes),

            AllowedComputerOuDns           = ReadStringList(node, "AllowedComputerOuDns"),
            ExcludedComputerNames          = ReadStringList(node, "ExcludedComputerNames"),
            EditableComputerAttributes     = ReadStringListOrDefault(node, "EditableComputerAttributes",     d.EditableComputerAttributes),

            EditableGroupOuDns             = ReadStringList(node, "EditableGroupOuDns"),
            ProtectedGroupNames            = ReadStringList(node, "ProtectedGroupNames"),
            ProtectedGroupDns              = ReadStringList(node, "ProtectedGroupDns"),

            OperationChecklistItems        = ReadStringList(node, "OperationChecklistItems"),
        };
    }

    private static string ReadString(JsonElement node, string key, string fallback)
        => node.TryGetProperty(key, out var val) && val.ValueKind == JsonValueKind.String
            ? val.GetString() ?? fallback
            : fallback;

    // ValueKind を Number に限定し、文字列 "1000" 等をフォールバックに落とす。
    // minValue 未満の値も設定ミスとみなしてフォールバックを返す。
    private static int ReadInt(JsonElement node, string key, int fallback, int minValue = int.MinValue)
    {
        if (!node.TryGetProperty(key, out var val) ||
            val.ValueKind != JsonValueKind.Number ||
            !val.TryGetInt32(out var result))
            return fallback;
        return result < minValue ? fallback : result;
    }

    private static bool ReadBool(JsonElement node, string key, bool fallback)
    {
        if (!node.TryGetProperty(key, out var val)) return fallback;
        if (val.ValueKind == JsonValueKind.True)  return true;
        if (val.ValueKind == JsonValueKind.False) return false;
        return fallback;
    }

    private static List<string> ReadStringList(JsonElement node, string key)
        => node.TryGetProperty(key, out var arr) && arr.ValueKind == JsonValueKind.Array
            ? arr.EnumerateArray()
                 .Select(x => x.GetString() ?? string.Empty)
                 .Where(x => x.Length > 0)
                 .ToList()
            : new List<string>();

    private static List<string> ReadStringListOrDefault(JsonElement node, string key, IReadOnlyList<string> fallback)
        => node.TryGetProperty(key, out var arr) && arr.ValueKind == JsonValueKind.Array
            ? arr.EnumerateArray()
                 .Select(x => x.GetString() ?? string.Empty)
                 .Where(x => x.Length > 0)
                 .ToList()
            : fallback.ToList();
}
