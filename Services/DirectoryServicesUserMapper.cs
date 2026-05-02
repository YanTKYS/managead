using System.DirectoryServices;
using ManageAdTool.Models;

namespace ManageAdTool.Services;

public static class DirectoryServicesUserMapper
{
    public static AdUser MapUser(SearchResult r)
    {
        string Get(string n) => r.Properties.Contains(n) && r.Properties[n].Count > 0 ? r.Properties[n][0]?.ToString() ?? string.Empty : string.Empty;
        var groups = new List<string>();
        if (r.Properties.Contains("memberOf"))
        {
            foreach (var v in r.Properties["memberOf"]) groups.Add(ExtractCn(v?.ToString() ?? string.Empty));
        }
        return new AdUser
        {
            SamAccountName = Get("samAccountName"),
            DisplayName = Get("displayName"),
            Name = Get("name"),
            Mail = Get("mail"),
            Department = Get("department"),
            Title = Get("title"),
            DistinguishedName = Get("distinguishedName"),
            Enabled = true,
            Groups = groups
        };
    }

    private static string ExtractCn(string dn)
    {
        var p = dn.Split(',').FirstOrDefault() ?? dn;
        return p.StartsWith("CN=", StringComparison.OrdinalIgnoreCase) ? p[3..] : p;
    }
}
