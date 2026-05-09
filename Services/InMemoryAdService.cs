using ManageAdTool.Models;

namespace ManageAdTool.Services;

public class InMemoryAdService : IAdService
{
    private readonly Dictionary<string, AdUser> _users = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sato.taro"] = new AdUser
        {
            SamAccountName = "sato.taro", DisplayName = "佐藤 太郎", Name = "Taro Sato",
            Surname = "佐藤", GivenName = "太郎",
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
            Surname = "田中", GivenName = "花",
            Mail = "hana.tanaka@example.local", Department = "総務課", Title = "担当",
            Enabled = true, UserAccountControl = 512,
            AccountExpiresAt = DateTimeOffset.UtcNow.AddDays(-2),
            DistinguishedName = "CN=Hana Tanaka,OU=Users,DC=example,DC=local",
            LastLogonAt = DateTimeOffset.UtcNow.AddDays(-3),
            LastLogonComputer = "PC-002",
            Groups = new[] { "GG_OfficeUsers" }
        }
    };

    private readonly Dictionary<string, AdComputer> _computers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PC-001"] = new AdComputer
        {
            Name = "PC-001", SamAccountName = "PC-001$",
            DnsHostName = "PC-001.example.local",
            OperatingSystem = "Windows 11 Pro",
            Description = "情報政策課端末",
            DistinguishedName = "CN=PC-001,OU=Computers,DC=example,DC=local",
            Enabled = true,
            LastLogonAt = DateTimeOffset.UtcNow.AddHours(-2),
            WhenCreated = DateTimeOffset.UtcNow.AddDays(-365),
            WhenChanged = DateTimeOffset.UtcNow.AddDays(-10),
            Groups = new[] { "GG_Workstations" }
        },
        ["PC-002"] = new AdComputer
        {
            Name = "PC-002", SamAccountName = "PC-002$",
            DnsHostName = "PC-002.example.local",
            OperatingSystem = "Windows 10 Pro",
            Description = string.Empty,
            DistinguishedName = "CN=PC-002,OU=Computers,DC=example,DC=local",
            Enabled = true,
            LastLogonAt = DateTimeOffset.UtcNow.AddDays(-3),
            WhenCreated = DateTimeOffset.UtcNow.AddDays(-730),
            WhenChanged = DateTimeOffset.UtcNow.AddDays(-30),
            Groups = new[] { "GG_Workstations" }
        },
        ["SRV-001"] = new AdComputer
        {
            Name = "SRV-001", SamAccountName = "SRV-001$",
            DnsHostName = "SRV-001.example.local",
            OperatingSystem = "Windows Server 2022 Standard",
            Description = "ファイルサーバー（本番）",
            DistinguishedName = "CN=SRV-001,OU=Servers,DC=example,DC=local",
            Enabled = true,
            LastLogonAt = DateTimeOffset.UtcNow.AddMinutes(-30),
            WhenCreated = DateTimeOffset.UtcNow.AddDays(-1000),
            WhenChanged = DateTimeOffset.UtcNow.AddDays(-1),
            Groups = new[] { "GG_Servers" }
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

    public IReadOnlyList<AdComputer> SearchComputers(AdComputerSearchCriteria criteria)
    {
        var keyword = criteria.Keyword.Trim();
        return _computers.Values.Where(c =>
            (keyword.Length == 0 ||
                c.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                c.DnsHostName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                c.SamAccountName.Contains(keyword, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(criteria.OperatingSystem) ||
                c.OperatingSystem.Contains(criteria.OperatingSystem.Trim(), StringComparison.OrdinalIgnoreCase)) &&
            (!criteria.HasDescription.HasValue || criteria.HasDescription.Value == !string.IsNullOrWhiteSpace(c.Description)) &&
            (criteria.IncludeDisabled || c.Enabled))
            .ToList();
    }

    public AdComputer? GetComputer(string name)
        => _computers.TryGetValue(name, out var computer) ? computer : null;

    public IReadOnlyList<string> GetComputerGroups(string name)
    {
        if (!_computers.TryGetValue(name, out var computer)) return Array.Empty<string>();
        return computer.Groups.ToList();
    }

    public ChangeSet BuildComputerChangeSet(AdComputer current, string newDescription)
    {
        var cs = new ChangeSet { TargetSamAccountName = current.Name, TargetDisplayName = current.DnsHostName };
        if (!string.Equals(current.Description, newDescription, StringComparison.Ordinal))
            cs.Changes.Add(new("説明 (description)", current.Description, newDescription) { LdapAttribute = "description" });
        return cs;
    }

    public ChangeSet BuildChangeSet(AdUser current, string newMail, string newDisplayName, string newSurname, string newGivenName)
    {
        var cs = new ChangeSet { TargetSamAccountName = current.SamAccountName, TargetDisplayName = current.DisplayName };
        if (!string.Equals(current.Mail, newMail, StringComparison.Ordinal))
            cs.Changes.Add(new(EditableAttributeDefs.Mail.DisplayName, current.Mail, newMail) { LdapAttribute = EditableAttributeDefs.Mail.LdapAttribute });
        if (!string.Equals(current.DisplayName, newDisplayName, StringComparison.Ordinal))
            cs.Changes.Add(new(EditableAttributeDefs.DisplayName.DisplayName, current.DisplayName, newDisplayName) { LdapAttribute = EditableAttributeDefs.DisplayName.LdapAttribute });
        if (!string.Equals(current.Surname, newSurname, StringComparison.Ordinal))
            cs.Changes.Add(new(EditableAttributeDefs.Surname.DisplayName, current.Surname, newSurname) { LdapAttribute = EditableAttributeDefs.Surname.LdapAttribute });
        if (!string.Equals(current.GivenName, newGivenName, StringComparison.Ordinal))
            cs.Changes.Add(new(EditableAttributeDefs.GivenName.DisplayName, current.GivenName, newGivenName) { LdapAttribute = EditableAttributeDefs.GivenName.LdapAttribute });
        return cs;
    }
}
