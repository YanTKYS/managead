using System.DirectoryServices;
using ManageAdTool.Models;

namespace ManageAdTool.Services;

public class DirectoryServicesAdReadService : IAdService
{
    private readonly AppPolicy Policy;

    public DirectoryServicesAdReadService(AppPolicy policy)
    {
        Policy = policy;
    }

    public IReadOnlyList<AdUser> SearchUsers(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword) || keyword.Trim().Length <= 1) return Array.Empty<AdUser>();

        try
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
                    var user = DirectoryServicesUserMapper.MapUser(r);
                    if (IsExcluded(user.SamAccountName)) continue;
                    list.Add(user);
                }
            }
            return list;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("AD検索の実行中にエラーが発生しました。", ex);
        }
    }

    public AdUser? GetUser(string samAccountName)
    {
        try
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
                var user = DirectoryServicesUserMapper.MapUser(r);
                if (IsExcluded(user.SamAccountName)) return null;
                return user;
            }
            return null;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("ADユーザー取得の実行中にエラーが発生しました。", ex);
        }
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

    private IEnumerable<string> GetSearchBases() => Policy.AllowedTargetOuDns.Count > 0 ? Policy.AllowedTargetOuDns : new[] { GetDefaultNamingContext() };

    private bool IsExcluded(string sam) => Policy.ExcludedSamAccountNames.Any(x => string.Equals(x, sam, StringComparison.OrdinalIgnoreCase));

    private bool IsExcluded(string sam) => _policy.ExcludedSamAccountNames.Any(x => string.Equals(x, sam, StringComparison.OrdinalIgnoreCase));

    private static string GetDefaultNamingContext()
    {
        using var rootDse = new DirectoryEntry("LDAP://RootDSE");
        var value = rootDse.Properties["defaultNamingContext"]?.Value?.ToString();
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("defaultNamingContext を取得できませんでした。");
        return value;
    }

    private static string EscapeLdap(string value) => value.Replace("\\", "\\5c").Replace("*", "\\2a").Replace("(", "\\28").Replace(")", "\\29").Replace("\0", "\\00");
}
