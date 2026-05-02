using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using ManageAdTool.Models;

namespace ManageAdTool.Services;

public static class AppSettingsBootstrapper
{
    public static AppPolicy MergeFromCurrentEnvironment(string path = "appsettings.json")
    {
        var domain = Environment.UserDomainName;
        var machine = Environment.MachineName;
        var user = Environment.UserName;
        var domainDn = string.Join(',', domain.Split('.', StringSplitOptions.RemoveEmptyEntries).Select(x => $"DC={x}"));

        var defaultUserOu = $"OU=Users,{domainDn}";
        var defaultComputerOu = $"OU=Computers,{domainDn}";

        JsonObject root;
        if (File.Exists(path))
        {
            root = JsonNode.Parse(File.ReadAllText(path))?.AsObject() ?? new JsonObject();
        }
        else
        {
            root = new JsonObject();
        }

        var appPolicy = root["AppPolicy"] as JsonObject ?? new JsonObject();
        root["AppPolicy"] = appPolicy;

        var allowed = appPolicy["AllowedTargetOuDns"] as JsonArray ?? new JsonArray();
        AddIfMissing(allowed, defaultUserOu);
        AddIfMissing(allowed, defaultComputerOu);
        appPolicy["AllowedTargetOuDns"] = allowed;

        var excluded = appPolicy["ExcludedSamAccountNames"] as JsonArray ?? new JsonArray();
        AddIfMissing(excluded, "administrator");
        AddIfMissing(excluded, "krbtgt");
        appPolicy["ExcludedSamAccountNames"] = excluded;

        appPolicy["DetectedContext"] = new JsonObject
        {
            ["User"] = $"{domain}\\{user}",
            ["Machine"] = machine,
            ["Domain"] = domain,
            ["DomainDn"] = domainDn,
            ["DetectedAtUtc"] = DateTimeOffset.UtcNow.ToString("O")
        };

        File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return AppPolicyProvider.Load(path);
    }

    private static void AddIfMissing(JsonArray arr, string value)
    {
        if (!arr.Any(x => string.Equals(x?.GetValue<string>(), value, StringComparison.OrdinalIgnoreCase)))
        {
            arr.Add(value);
        }
    }
}
