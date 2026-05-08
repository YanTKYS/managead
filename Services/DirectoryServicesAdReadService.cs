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
        => SearchUsers(new AdUserSearchCriteria { Keyword = keyword });

    public IReadOnlyList<AdUser> SearchUsers(AdUserSearchCriteria criteria)
    {
        var keyword = criteria.Keyword.Trim();
        if (string.IsNullOrWhiteSpace(keyword) || keyword.Length <= 1) return Array.Empty<AdUser>();

        try
        {
            var list = new List<AdUser>();
            foreach (var baseDn in GetSearchBases())
            {
                using var root = new DirectoryEntry($"LDAP://{baseDn}");
                using var ds = new DirectorySearcher(root)
                {
                    Filter = BuildUserSearchFilter(criteria),
                    PageSize = 200,
                    SizeLimit = Policy.MaxSearchResults
                };
                AddUserProperties(ds);
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
                AddUserProperties(ds);
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


    public IReadOnlyList<AdGroup> SearchGroups(string keyword)
    {
        var term = keyword.Trim();
        if (string.IsNullOrWhiteSpace(term) || term.Length <= 1) return Array.Empty<AdGroup>();

        try
        {
            var list = new List<AdGroup>();
            foreach (var baseDn in GetSearchBases())
            {
                using var root = new DirectoryEntry($"LDAP://{baseDn}");
                using var ds = new DirectorySearcher(root)
                {
                    Filter = $"(&(objectClass=group)(|(cn=*{EscapeLdap(term)}*)(name=*{EscapeLdap(term)}*)(sAMAccountName=*{EscapeLdap(term)}*)))",
                    PageSize = 200
                };
                ds.PropertiesToLoad.AddRange(new[] { "cn", "name", "distinguishedName" });
                foreach (SearchResult r in ds.FindAll())
                {
                    list.Add(new AdGroup
                    {
                        Name = GetProperty(r, "cn", GetProperty(r, "name", string.Empty)),
                        DistinguishedName = GetProperty(r, "distinguishedName", string.Empty)
                    });
                }
            }

            return list.OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("ADグループ検索の実行中にエラーが発生しました。", ex);
        }
    }

    public IReadOnlyList<AdUser> GetGroupMembers(string groupName)
    {
        try
        {
            var groupDn = ResolveGroupDistinguishedName(groupName);
            if (string.IsNullOrWhiteSpace(groupDn)) return Array.Empty<AdUser>();

            var members = new List<AdUser>();
            foreach (var baseDn in GetSearchBases())
            {
                using var root = new DirectoryEntry($"LDAP://{baseDn}");
                using var ds = new DirectorySearcher(root)
                {
                    Filter = $"(&(objectClass=user)(!(objectClass=computer))(memberOf={EscapeLdap(groupDn)}))",
                    PageSize = 200
                };
                AddUserProperties(ds);
                foreach (SearchResult r in ds.FindAll())
                {
                    var user = DirectoryServicesUserMapper.MapUser(r);
                    if (IsExcluded(user.SamAccountName)) continue;
                    members.Add(user);
                }
            }

            return members.OrderBy(u => u.SamAccountName, StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("ADグループメンバー取得の実行中にエラーが発生しました。", ex);
        }
    }

    public ChangeSet BuildChangeSet(AdUser current, string newMail, string newDepartment, string newTitle)
    {
        var cs = new ChangeSet { TargetSamAccountName = current.SamAccountName };
        if (!string.Equals(current.Mail, newMail, StringComparison.Ordinal)) cs.Changes.Add(new("Mail", current.Mail, newMail));
        if (!string.Equals(current.Department, newDepartment, StringComparison.Ordinal)) cs.Changes.Add(new("Department", current.Department, newDepartment));
        if (!string.Equals(current.Title, newTitle, StringComparison.Ordinal)) cs.Changes.Add(new("Title", current.Title, newTitle));
        return cs;
    }


    private string BuildUserSearchFilter(AdUserSearchCriteria criteria)
    {
        var keyword = EscapeLdap(criteria.Keyword.Trim());
        var filters = new List<string>
        {
            "(objectClass=user)",
            "(!(objectClass=computer))",
            $"(|(sAMAccountName=*{keyword}*)(displayName=*{keyword}*)(name=*{keyword}*)(mail=*{keyword}*))"
        };

        if (!string.IsNullOrWhiteSpace(criteria.Department)) filters.Add($"(department=*{EscapeLdap(criteria.Department.Trim())}*)");
        if (criteria.HasMail == true) filters.Add("(mail=*)");
        if (criteria.HasMail == false) filters.Add("(!(mail=*))");
        if (!criteria.IncludeDisabled) filters.Add("(!(userAccountControl:1.2.840.113556.1.4.803:=2))");

        return $"(&{string.Concat(filters)})";
    }

    private string ResolveGroupDistinguishedName(string groupName)
    {
        if (groupName.Contains("=", StringComparison.Ordinal) && groupName.Contains(",", StringComparison.Ordinal)) return groupName;

        foreach (var group in SearchGroups(groupName))
        {
            if (string.Equals(group.Name, groupName, StringComparison.OrdinalIgnoreCase)) return group.DistinguishedName;
        }

        return SearchGroups(groupName).FirstOrDefault()?.DistinguishedName ?? string.Empty;
    }

    private static void AddUserProperties(DirectorySearcher ds)
        => ds.PropertiesToLoad.AddRange(new[] { "samAccountName", "displayName", "name", "mail", "department", "title", "distinguishedName", "memberOf", "lastLogonTimestamp", "accountExpires", "userAccountControl" });

    private static string GetProperty(SearchResult r, string name, string fallback)
        => r.Properties.Contains(name) && r.Properties[name].Count > 0 ? r.Properties[name][0]?.ToString() ?? fallback : fallback;

    private IEnumerable<string> GetSearchBases() => Policy.AllowedTargetOuDns.Count > 0 ? Policy.AllowedTargetOuDns : new[] { GetDefaultNamingContext() };

    private bool IsExcluded(string sam) => Policy.ExcludedSamAccountNames.Any(x => string.Equals(x, sam, StringComparison.OrdinalIgnoreCase));

    private static string GetDefaultNamingContext()
    {
        using var rootDse = new DirectoryEntry("LDAP://RootDSE");
        var value = rootDse.Properties["defaultNamingContext"]?.Value?.ToString();
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("defaultNamingContext を取得できませんでした。");
        return value;
    }

    private static string EscapeLdap(string value) => value.Replace("\\", "\\5c").Replace("*", "\\2a").Replace("(", "\\28").Replace(")", "\\29").Replace("\0", "\\00");
}
