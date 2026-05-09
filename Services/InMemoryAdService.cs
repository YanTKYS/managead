using ManageAdTool.Models;

namespace ManageAdTool.Services;

public class InMemoryAdService : IAdService
{
    private readonly Dictionary<string, AdUser> _users = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sato.taro"] = new AdUser
        {
            SamAccountName = "sato.taro", DisplayName = "佐藤 太郎", Name = "Taro Sato",
            Mail = "taro.sato@example.local", Department = "情報政策課", Title = "主任",
            Enabled = true, UserAccountControl = 512,
            AccountExpiresAt = DateTimeOffset.UtcNow.AddDays(-1),
            DistinguishedName = "CN=Taro Sato,OU=Users,DC=example,DC=local",
            LastLogonAt = DateTimeOffset.UtcNow.AddHours(-5),
            LastLogonComputer = "PC-001",
            Groups = new[] { "GG_OfficeUsers", "GG_InfoPolicy" }
        },
        ["tanaka.hana"] = new AdUser
        {
            SamAccountName = "tanaka.hana", DisplayName = "田中 花", Name = "Hana Tanaka",
            Mail = "hana.tanaka@example.local", Department = "総務課", Title = "担当",
            Enabled = true, UserAccountControl = 512,
            AccountExpiresAt = DateTimeOffset.UtcNow.AddDays(-2),
            DistinguishedName = "CN=Hana Tanaka,OU=Users,DC=example,DC=local",
            LastLogonAt = DateTimeOffset.UtcNow.AddDays(-3),
            LastLogonComputer = "PC-002",
            Groups = new[] { "GG_OfficeUsers" }
        }
    };

    private readonly List<AdGroup> _groups = new()
    {
        new() { Name = "GG_OfficeUsers", DistinguishedName = "CN=GG_OfficeUsers,OU=Groups,DC=example,DC=local" },
        new() { Name = "GG_InfoPolicy",  DistinguishedName = "CN=GG_InfoPolicy,OU=Groups,DC=example,DC=local"  }
    };

    private readonly Dictionary<string, HashSet<string>> _directGroupMembers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GG_OfficeUsers"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "sato.taro", "tanaka.hana" },
        ["GG_InfoPolicy"]  = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "sato.taro" }
    };

    public IReadOnlyList<AdUser> SearchUsers(string keyword)
        => SearchUsers(new AdUserSearchCriteria { Keyword = keyword });

    public IReadOnlyList<AdUser> SearchUsers(AdUserSearchCriteria criteria)
    {
        var keyword = criteria.Keyword.Trim();
        return _users.Values.Where(u =>
            (keyword.Length == 0 ||
                u.SamAccountName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                u.DisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                u.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                u.Mail.Contains(keyword, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(criteria.Department) ||
                u.Department.Contains(criteria.Department.Trim(), StringComparison.OrdinalIgnoreCase)) &&
            (!criteria.HasMail.HasValue || criteria.HasMail.Value == !string.IsNullOrWhiteSpace(u.Mail)) &&
            (criteria.IncludeDisabled || u.Enabled))
            .ToList();
    }

    public AdUser? GetUser(string samAccountName)
        => _users.TryGetValue(samAccountName, out var user) ? user : null;

    public IReadOnlyList<string> GetUserGroups(string samAccountName)
    {
        if (!_users.TryGetValue(samAccountName, out var user)) return Array.Empty<string>();
        return user.Groups.ToList();
    }

    public IReadOnlyList<AdGroup> SearchGroups(string keyword)
    {
        var term = keyword.Trim();
        if (term.Length <= 1) return Array.Empty<AdGroup>();
        return _groups.Where(g =>
            g.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            g.DistinguishedName.Contains(term, StringComparison.OrdinalIgnoreCase))
            .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<AdUser> GetGroupMembers(string groupNameOrDn)
    {
        var name = groupNameOrDn.Contains(',', StringComparison.Ordinal)
            ? _groups.FirstOrDefault(g => string.Equals(g.DistinguishedName, groupNameOrDn, StringComparison.OrdinalIgnoreCase))?.Name ?? groupNameOrDn
            : groupNameOrDn;
        if (!_directGroupMembers.TryGetValue(name, out var members)) return Array.Empty<AdUser>();
        return members.Where(_users.ContainsKey).Select(sam => _users[sam]).ToList();
    }

    public ChangeSet BuildChangeSet(AdUser current, string newMail, string newDepartment, string newTitle)
    {
        var cs = new ChangeSet { TargetSamAccountName = current.SamAccountName, TargetDisplayName = current.DisplayName };
        if (!string.Equals(current.Mail, newMail, StringComparison.Ordinal))
            cs.Changes.Add(new("Mail", current.Mail, newMail));
        if (!string.Equals(current.Department, newDepartment, StringComparison.Ordinal))
            cs.Changes.Add(new("Department", current.Department, newDepartment));
        if (!string.Equals(current.Title, newTitle, StringComparison.Ordinal))
            cs.Changes.Add(new("Title", current.Title, newTitle));
        return cs;
    }
}
