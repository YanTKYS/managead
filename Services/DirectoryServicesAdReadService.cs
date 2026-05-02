using System.DirectoryServices;
using ManageAdTool.Models;

namespace ManageAdTool.Services;

public class DirectoryServicesAdReadService : IAdService
{
    private readonly AppPolicy _policy;

    public DirectoryServicesAdReadService(AppPolicy policy)
    {
        _policy = policy;
    }

    public IReadOnlyList<AdUser> SearchUsers(string keyword)
    {
        var list = new List<AdUser>();
        foreach (var baseDn in GetSearchBases())
        {
            using var root = new DirectoryEntry($"LDAP://{baseDn}");
            using var ds = new DirectorySearcher(root)
            {
                Filter = $"(&(objectClass=user)(!(objectClass=computer))(|(sAMAccountName=*{EscapeLdap(keyword)}*)(displayName=*{EscapeLdap(keyword)}*)(name=*{EscapeLdap(keyword)}*)(mail=*{EscapeLdap(keyword)}*)))",
                PageSize = 200
            };
            ds.PropertiesToLoad.AddRange(new[] { "samAccountName", "displayName", "name", "mail", "department", "title", "distinguishedName", "memberOf" });
            foreach (SearchResult r in ds.FindAll())
            {
                var user = MapUser(r);
                if (IsExcluded(user.SamAccountName)) continue;
                list.Add(user);
            }
        }
        return list;
    }

    public AdUser? GetUser(string samAccountName)
    {
        foreach (var baseDn in GetSearchBases())
        {
            using var root = new DirectoryEntry($"LDAP://{baseDn}");
            using var ds = new DirectorySearcher(root)
            {
                Filter = $"(&(objectClass=user)(sAMAccountName={EscapeLdap(samAccountName)}))",
                PageSize = 1
            };
            ds.PropertiesToLoad.AddRange(new[] { "samAccountName", "displayName", "name", "mail", "department", "title", "distinguishedName", "memberOf" });
            var r = ds.FindOne();
            if (r is null) continue;
            var user = MapUser(r);
            if (IsExcluded(user.SamAccountName)) return null;
            return user;
        }
        return null;
    }

    public IReadOnlyList<string> GetUserGroups(string samAccountName)
    {
        var user = GetUser(samAccountName);
        return user?.Groups ?? Array.Empty<string>();
    }

    public ChangeSet BuildChangeSet(AdUser current, string newMail, string newDepartment, string newTitle)
    {
        var cs = new ChangeSet { TargetSamAccountName = current.SamAccountName };
        if (!string.Equals(current.Mail, newMail, StringComparison.Ordinal)) cs.Changes.Add(new("Mail", current.Mail, newMail));
        if (!string.Equals(current.Department, newDepartment, StringComparison.Ordinal)) cs.Changes.Add(new("Department", current.Department, newDepartment));
        if (!string.Equals(current.Title, newTitle, StringComparison.Ordinal)) cs.Changes.Add(new("Title", current.Title, newTitle));
        return cs;
    }

    public void UpdateAttributes(string samAccountName, string mail, string department, string title)
        => throw new NotSupportedException("DirectoryReadOnly mode does not support updates.");

    private AdUser MapUser(SearchResult r)
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

    private IEnumerable<string> GetSearchBases() => _policy.AllowedTargetOuDns.Count > 0 ? _policy.AllowedTargetOuDns : new[] { RootFromDomain() };
    private string RootFromDomain() => string.Join(',', Environment.UserDomainName.Split('.').Where(x => x.Length > 0).Select(x => $"DC={x}"));
    private bool IsExcluded(string sam) => _policy.ExcludedSamAccountNames.Any(x => string.Equals(x, sam, StringComparison.OrdinalIgnoreCase));
    private static string ExtractCn(string dn)
    {
        var p = dn.Split(',').FirstOrDefault() ?? dn;
        return p.StartsWith("CN=", StringComparison.OrdinalIgnoreCase) ? p[3..] : p;
    }
    private static string EscapeLdap(string value) => value.Replace("\\", "\\5c").Replace("*", "\\2a").Replace("(", "\\28").Replace(")", "\\29").Replace("\0", "\\00");
}
