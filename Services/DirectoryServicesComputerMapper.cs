using System.DirectoryServices;
using ManageAdTool.Models;

namespace ManageAdTool.Services;

public static class DirectoryServicesComputerMapper
{
    private const int AccountDisabledFlag = 0x0002;

    public static AdComputer MapComputer(SearchResult r)
    {
        var groups = new List<string>();
        if (r.Properties.Contains("memberOf"))
        {
            foreach (var v in r.Properties["memberOf"])
                groups.Add(ExtractCn(v?.ToString() ?? string.Empty));
        }

        var uac = GetInt(r, "userAccountControl");
        return new AdComputer
        {
            Name = GetString(r, "name"),
            SamAccountName = GetString(r, "sAMAccountName"),
            DnsHostName = GetString(r, "dNSHostName"),
            OperatingSystem = GetString(r, "operatingSystem"),
            Description = GetString(r, "description"),
            DistinguishedName = GetString(r, "distinguishedName"),
            Enabled = !uac.HasValue || (uac.Value & AccountDisabledFlag) == 0,
            LastLogonAt = GetNullableFileTime(r, "lastLogonTimestamp"),
            WhenCreated = GetGeneralizedTime(r, "whenCreated"),
            WhenChanged = GetGeneralizedTime(r, "whenChanged"),
            Groups = groups
        };
    }

    private static string GetString(SearchResult r, string name)
        => r.Properties.Contains(name) && r.Properties[name].Count > 0
            ? r.Properties[name][0]?.ToString() ?? string.Empty
            : string.Empty;

    private static int? GetInt(SearchResult r, string name)
    {
        var raw = r.Properties.Contains(name) && r.Properties[name].Count > 0 ? r.Properties[name][0] : null;
        if (raw is null) return null;
        return int.TryParse(raw.ToString(), out var v) ? v : null;
    }

    private static DateTimeOffset? GetNullableFileTime(SearchResult r, string name)
    {
        var raw = r.Properties.Contains(name) && r.Properties[name].Count > 0 ? r.Properties[name][0] : null;
        var fileTime = ConvertLargeIntegerToInt64(raw);
        if (!fileTime.HasValue || fileTime.Value <= 0 || fileTime.Value == long.MaxValue) return null;
        try { return DateTimeOffset.FromFileTime(fileTime.Value); }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    private static DateTimeOffset? GetGeneralizedTime(SearchResult r, string name)
    {
        var raw = r.Properties.Contains(name) && r.Properties[name].Count > 0 ? r.Properties[name][0] : null;
        if (raw is DateTime dt) return new DateTimeOffset(dt, TimeSpan.Zero);
        var s = raw?.ToString();
        if (string.IsNullOrEmpty(s)) return null;
        var core = s.Split('.')[0];
        if (DateTime.TryParseExact(core, "yyyyMMddHHmmss", null,
                System.Globalization.DateTimeStyles.AssumeUniversal, out var parsed))
            return new DateTimeOffset(parsed, TimeSpan.Zero);
        return null;
    }

    private static long? ConvertLargeIntegerToInt64(object? value)
    {
        if (value is null) return null;
        if (value is long l) return l;
        if (long.TryParse(value.ToString(), out var p)) return p;
        var type = value.GetType();
        try
        {
            var high = Convert.ToInt64(type.InvokeMember("HighPart", System.Reflection.BindingFlags.GetProperty, null, value, null));
            var low = Convert.ToInt64(type.InvokeMember("LowPart", System.Reflection.BindingFlags.GetProperty, null, value, null));
            return (high << 32) + (low & 0xffffffffL);
        }
        catch { return null; }
    }

    private static string ExtractCn(string dn)
    {
        var p = dn.Split(',').FirstOrDefault() ?? dn;
        return p.StartsWith("CN=", StringComparison.OrdinalIgnoreCase) ? p[3..] : p;
    }
}
