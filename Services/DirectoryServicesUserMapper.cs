using System.DirectoryServices;
using ManageAdTool.Models;

namespace ManageAdTool.Services;

public static class DirectoryServicesUserMapper
{
    private const int AccountDisabledFlag = 0x0002;

    public static AdUser MapUser(SearchResult r)
    {
        var groups = new List<string>();
        if (r.Properties.Contains("memberOf"))
        {
            foreach (var v in r.Properties["memberOf"]) groups.Add(ExtractCn(v?.ToString() ?? string.Empty));
        }

        var userAccountControl = GetInt(r, "userAccountControl");

        return new AdUser
        {
            SamAccountName = GetString(r, "samAccountName"),
            DisplayName = GetString(r, "displayName"),
            Name = GetString(r, "name"),
            Mail = GetString(r, "mail"),
            Department = GetString(r, "department"),
            Title = GetString(r, "title"),
            DistinguishedName = GetString(r, "distinguishedName"),
            Enabled = !userAccountControl.HasValue || (userAccountControl.Value & AccountDisabledFlag) == 0,
            UserAccountControl = userAccountControl,
            AccountExpiresAt = GetNullableFileTime(r, "accountExpires"),
            LastLogonAt = GetNullableFileTime(r, "lastLogonTimestamp"),
            Groups = groups
        };
    }

    private static string GetString(SearchResult r, string name)
        => r.Properties.Contains(name) && r.Properties[name].Count > 0 ? r.Properties[name][0]?.ToString() ?? string.Empty : string.Empty;

    private static int? GetInt(SearchResult r, string name)
    {
        var raw = GetPropertyValue(r, name);
        if (raw is null) return null;
        return int.TryParse(raw.ToString(), out var value) ? value : null;
    }

    private static DateTimeOffset? GetNullableFileTime(SearchResult r, string name)
    {
        var raw = GetPropertyValue(r, name);
        var fileTime = ConvertLargeIntegerToInt64(raw);
        if (!fileTime.HasValue || fileTime.Value <= 0 || fileTime.Value == long.MaxValue) return null;
        try
        {
            return DateTimeOffset.FromFileTime(fileTime.Value);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static object? GetPropertyValue(SearchResult r, string name)
        => r.Properties.Contains(name) && r.Properties[name].Count > 0 ? r.Properties[name][0] : null;

    private static long? ConvertLargeIntegerToInt64(object? value)
    {
        if (value is null) return null;
        if (value is long longValue) return longValue;
        if (long.TryParse(value.ToString(), out var parsed)) return parsed;

        var type = value.GetType();
        try
        {
            var highPart = Convert.ToInt64(type.InvokeMember("HighPart", System.Reflection.BindingFlags.GetProperty, null, value, null));
            var lowPart = Convert.ToInt64(type.InvokeMember("LowPart", System.Reflection.BindingFlags.GetProperty, null, value, null));
            return (highPart << 32) + (lowPart & 0xffffffffL);
        }
        catch
        {
            return null;
        }
    }

    private static string ExtractCn(string dn)
    {
        var p = dn.Split(',').FirstOrDefault() ?? dn;
        return p.StartsWith("CN=", StringComparison.OrdinalIgnoreCase) ? p[3..] : p;
    }
}
