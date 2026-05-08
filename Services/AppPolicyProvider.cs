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

        return new AppPolicy
        {
            AllowedTargetOuDns = node.TryGetProperty("AllowedTargetOuDns", out var ous)
                ? ous.EnumerateArray().Select(x => x.GetString() ?? string.Empty).Where(x => x.Length > 0).ToList()
                : new List<string>(),
            ExcludedSamAccountNames = node.TryGetProperty("ExcludedSamAccountNames", out var ex)
                ? ex.EnumerateArray().Select(x => x.GetString() ?? string.Empty).Where(x => x.Length > 0).ToList()
                : new List<string>(),
            EditableAttributes = node.TryGetProperty("EditableAttributes", out var ed)
                ? ed.EnumerateArray().Select(x => x.GetString() ?? string.Empty).Where(x => x.Length > 0).ToList()
                : new List<string> { "mail", "department", "title" },
            UserDetailDisplayAttributes = node.TryGetProperty("UserDetailDisplayAttributes", out var display)
                ? display.EnumerateArray().Select(x => x.GetString() ?? string.Empty).Where(x => x.Length > 0).ToList()
                : new AppPolicy().UserDetailDisplayAttributes,
            LogPath = node.TryGetProperty("LogPath", out var log) ? (log.GetString() ?? @"C:\ProgramData\ManageAdTool\logs\audit.jsonl") : @"C:\ProgramData\ManageAdTool\logs\audit.jsonl",
            RetiredUsersOuDn = node.TryGetProperty("RetiredUsersOuDn", out var retiredOu) ? (retiredOu.GetString() ?? "OU=RetiredUsers,DC=example,DC=local") : "OU=RetiredUsers,DC=example,DC=local",
            ServiceMode = node.TryGetProperty("ServiceMode", out var sm) ? (sm.GetString() ?? "InMemory") : "InMemory",
            MaxSearchResults = node.TryGetProperty("MaxSearchResults", out var maxResults) && maxResults.TryGetInt32(out var maxResultsValue) ? maxResultsValue : 200,
            EditorAuthMode = node.TryGetProperty("EditorAuthMode", out var editorAuthMode) ? (editorAuthMode.GetString() ?? "None") : "None",
            AdminGroupDn = node.TryGetProperty("AdminGroupDn", out var adminGroupDn) ? (adminGroupDn.GetString() ?? string.Empty) : string.Empty,
            AllowNestedAdminGroupMembership = node.TryGetProperty("AllowNestedAdminGroupMembership", out var nested) && nested.GetBoolean(),
            EditSessionMinutes = node.TryGetProperty("EditSessionMinutes", out var sessionMin) && sessionMin.TryGetInt32(out var sessionMinValue) ? sessionMinValue : 15
        };
    }
}
