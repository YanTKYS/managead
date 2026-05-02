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
            LogPath = node.TryGetProperty("LogPath", out var log) ? (log.GetString() ?? @"C:\ProgramData\ManageAdTool\logs\audit.jsonl") : @"C:\ProgramData\ManageAdTool\logs\audit.jsonl",
            RetiredUsersOuDn = node.TryGetProperty("RetiredUsersOuDn", out var retiredOu) ? (retiredOu.GetString() ?? "OU=RetiredUsers,DC=example,DC=local") : "OU=RetiredUsers,DC=example,DC=local",
            ServiceMode = node.TryGetProperty("ServiceMode", out var sm) ? (sm.GetString() ?? "InMemory") : "InMemory"
        };
    }
}
