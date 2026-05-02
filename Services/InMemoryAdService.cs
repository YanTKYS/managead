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
            Enabled = true, AccountExpiresAt = DateTimeOffset.UtcNow.AddDays(-1), DistinguishedName = "CN=Taro Sato,OU=Users,DC=example,DC=local",
            LastLogonAt = DateTimeOffset.UtcNow.AddHours(-5),
            LastLogonComputer = "PC-001",
            Groups = new[] { "GG_OfficeUsers", "GG_InfoPolicy" }
        },
        ["tanaka.hana"] = new AdUser
        {
            SamAccountName = "tanaka.hana", DisplayName = "田中 花", Name = "Hana Tanaka",
            Mail = "hana.tanaka@example.local", Department = "総務課", Title = "担当",
            Enabled = true, AccountExpiresAt = DateTimeOffset.UtcNow.AddDays(-2), DistinguishedName = "CN=Hana Tanaka,OU=Users,DC=example,DC=local",
            LastLogonAt = DateTimeOffset.UtcNow.AddDays(-3),
            LastLogonComputer = "PC-002",
            Groups = new[] { "GG_OfficeUsers" }
        }
    };


    private readonly List<AdGroup> _groups = new()
    {
        new() { Name = "GG_OfficeUsers", DistinguishedName = "CN=GG_OfficeUsers,OU=Groups,DC=example,DC=local" },
        new() { Name = "GG_InfoPolicy", DistinguishedName = "CN=GG_InfoPolicy,OU=Groups,DC=example,DC=local" }
    };

    private readonly Dictionary<string, HashSet<string>> _directGroupMembers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GG_OfficeUsers"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "sato.taro", "tanaka.hana" },
        ["GG_InfoPolicy"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "sato.taro" }
    };

    private readonly Dictionary<string, AdComputer> _computers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PC-001"] = new AdComputer { Name = "PC-001", DnsHostName = "pc-001.example.local", OperatingSystem = "Windows 11", DistinguishedName = "CN=PC-001,OU=Computers,DC=example,DC=local", Enabled = true, LastBootAt = DateTimeOffset.UtcNow.AddHours(-8), LastLoggedOnUser = "EXAMPLE\\sato.taro" },
        ["PC-002"] = new AdComputer { Name = "PC-002", DnsHostName = "pc-002.example.local", OperatingSystem = "Windows 10", DistinguishedName = "CN=PC-002,OU=Computers,DC=example,DC=local", Enabled = true, LastBootAt = DateTimeOffset.UtcNow.AddDays(-1), LastLoggedOnUser = "EXAMPLE\tanaka.hana" }
    };

    private readonly Dictionary<string, GpoPolicy> _gpos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["{1111-2222}"] = new GpoPolicy { Id = "{1111-2222}", DisplayName = "Default Workstation Policy", Description = "Base policy", UserSettingsEnabled = true, ComputerSettingsEnabled = true }
    };

    private readonly Dictionary<string, List<GroupGpoStatus>> _groupGpoMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GG_InfoPolicy"] = new List<GroupGpoStatus>
        {
            new() { GroupName = "GG_InfoPolicy", GpoDisplayName = "Default Workstation Policy", Enforced = true, LinkTarget = "OU=Users,DC=example,DC=local" },
            new() { GroupName = "GG_InfoPolicy", GpoDisplayName = "Security Baseline", Enforced = false, LinkTarget = "OU=Computers,DC=example,DC=local" }
        }
    };

    private readonly List<TargetGpoStatus> _targetGpoStatuses = new()
    {
        new() { UserSamAccountName = "sato.taro", ComputerName = "PC-001", GpoDisplayName = "Default Workstation Policy", Enforced = true, Scope = "User+Computer" },
        new() { UserSamAccountName = "sato.taro", ComputerName = "PC-001", GpoDisplayName = "Security Baseline", Enforced = false, Scope = "Computer" }
    };

    public IReadOnlyList<AdUser> SearchUsers(string keyword) => _users.Values.Where(u =>
        u.SamAccountName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
        u.DisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
        u.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
        u.Mail.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();

    public AdUser? GetUser(string samAccountName) => _users.TryGetValue(samAccountName, out var user) ? user : null;

    public ChangeSet BuildChangeSet(AdUser current, string newMail, string newDepartment, string newTitle)
    {
        var cs = new ChangeSet { TargetSamAccountName = current.SamAccountName };
        if (!string.Equals(current.Mail, newMail, StringComparison.Ordinal)) cs.Changes.Add(new("Mail", current.Mail, newMail));
        if (!string.Equals(current.Department, newDepartment, StringComparison.Ordinal)) cs.Changes.Add(new("Department", current.Department, newDepartment));
        if (!string.Equals(current.Title, newTitle, StringComparison.Ordinal)) cs.Changes.Add(new("Title", current.Title, newTitle));
        return cs;
    }

    public void UpdateAttributes(string samAccountName, string mail, string department, string title)
    {
        if (!_users.TryGetValue(samAccountName, out var u)) throw new InvalidOperationException("User not found");
        u.Mail = mail; u.Department = department; u.Title = title;
    }




    public IReadOnlyList<AdUser> GetExpiredUsers(DateTimeOffset now)
        => _users.Values.Where(u => u.AccountExpiresAt.HasValue && u.AccountExpiresAt.Value < now).ToList();

    public void ExtendAccountExpiration(IEnumerable<string> samAccountNames, DateTimeOffset newExpiry)
    {
        foreach (var sam in samAccountNames)
        {
            if (_users.TryGetValue(sam, out var user))
            {
                user.AccountExpiresAt = newExpiry;
                user.Enabled = true;
            }
        }
    }

    public void DisableUsers(IEnumerable<string> samAccountNames)
    {
        foreach (var sam in samAccountNames)
        {
            if (_users.TryGetValue(sam, out var user))
            {
                user.Enabled = false;
            }
        }
    }



    public IReadOnlyList<AdUser> GetDisabledUsers()
        => _users.Values.Where(u => !u.Enabled).ToList();

    public void RetireUsers(IEnumerable<string> samAccountNames, string retiredUsersOuDn)
    {
        foreach (var sam in samAccountNames)
        {
            if (!_users.TryGetValue(sam, out var user)) continue;
            var cn = user.DistinguishedName.Split(',').FirstOrDefault() ?? $"CN={user.Name}";
            user.DistinguishedName = $"{cn},{retiredUsersOuDn}";
            user.Enabled = false;
        }
    }

    public IReadOnlyList<AdUser> GetUsersNotLoggedInForDays(int days, DateTimeOffset now)
        => _users.Values.Where(u => u.LastLogonAt.HasValue && (now - u.LastLogonAt.Value).TotalDays >= days).ToList();

    public IReadOnlyList<AdComputer> GetComputersNotBootedForDays(int days, DateTimeOffset now)
        => _computers.Values.Where(c => c.LastBootAt.HasValue && (now - c.LastBootAt.Value).TotalDays >= days).ToList();

    public void DisableComputers(IEnumerable<string> computerNames)
    {
        foreach (var name in computerNames)
        {
            if (_computers.TryGetValue(name, out var c))
            {
                _computers[name] = new AdComputer
                {
                    Name = c.Name,
                    DnsHostName = c.DnsHostName,
                    OperatingSystem = c.OperatingSystem,
                    DistinguishedName = c.DistinguishedName,
                    Enabled = false,
                    LastBootAt = c.LastBootAt,
                    LastLoggedOnUser = c.LastLoggedOnUser
                };
            }
        }
    }


    public IReadOnlyList<AdGroup> GetGroups() => _groups;

    public IReadOnlyList<AdUser> GetDirectGroupMembers(string groupName)
    {
        if (!_directGroupMembers.TryGetValue(groupName, out var members)) return Array.Empty<AdUser>();
        return members.Where(_users.ContainsKey).Select(sam => _users[sam]).ToList();
    }

    public void AddDirectGroupMember(string groupName, string userSamAccountName)
    {
        if (!_users.ContainsKey(userSamAccountName)) return;
        if (!_directGroupMembers.ContainsKey(groupName)) _directGroupMembers[groupName] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _directGroupMembers[groupName].Add(userSamAccountName);
    }

    public void RemoveDirectGroupMember(string groupName, string userSamAccountName)
    {
        if (_directGroupMembers.TryGetValue(groupName, out var set)) set.Remove(userSamAccountName);
    }

    public IReadOnlyList<string> GetUserGroups(string samAccountName)
    {
        if (!_users.TryGetValue(samAccountName, out var user)) return Array.Empty<string>();
        return user.Groups.ToList();
    }

    public ChangeSet BuildGroupMembershipChangeSet(string samAccountName, IEnumerable<string> groupsToAdd, IEnumerable<string> groupsToRemove)
    {
        var cs = new ChangeSet { TargetSamAccountName = samAccountName };
        foreach (var g in groupsToAdd.Where(x => !string.IsNullOrWhiteSpace(x))) cs.Changes.Add(new("AddGroup", "-", g.Trim()));
        foreach (var g in groupsToRemove.Where(x => !string.IsNullOrWhiteSpace(x))) cs.Changes.Add(new("RemoveGroup", g.Trim(), "-"));
        return cs;
    }

    public void UpdateUserGroups(string samAccountName, IEnumerable<string> groupsToAdd, IEnumerable<string> groupsToRemove)
    {
        if (!_users.TryGetValue(samAccountName, out var user)) throw new InvalidOperationException("User not found");
        var list = user.Groups.ToList();
        foreach (var g in groupsToRemove.Where(x => !string.IsNullOrWhiteSpace(x)))
            list.RemoveAll(x => string.Equals(x, g.Trim(), StringComparison.OrdinalIgnoreCase));
        foreach (var g in groupsToAdd.Where(x => !string.IsNullOrWhiteSpace(x)))
            if (!list.Any(x => string.Equals(x, g.Trim(), StringComparison.OrdinalIgnoreCase))) list.Add(g.Trim());

        _users[samAccountName] = new AdUser
        {
            SamAccountName = user.SamAccountName,
            DisplayName = user.DisplayName,
            Name = user.Name,
            Mail = user.Mail,
            Department = user.Department,
            Title = user.Title,
            Enabled = user.Enabled,
            AccountExpiresAt = user.AccountExpiresAt,
            DistinguishedName = user.DistinguishedName,
            LastLogonAt = user.LastLogonAt,
            LastLogonComputer = user.LastLogonComputer,
            Groups = list
        };
    }

    public IReadOnlyList<AdComputer> SearchComputers(string keyword) => _computers.Values.Where(c =>
        c.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
        c.DnsHostName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
        c.OperatingSystem.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();

    public AdComputer? GetComputer(string name) => _computers.TryGetValue(name, out var c) ? c : null;

    public IReadOnlyList<GpoPolicy> SearchGpos(string keyword) => _gpos.Values.Where(g =>
        g.Id.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
        g.DisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();

    public ChangeSet BuildGpoChangeSet(GpoPolicy current, string newDescription, bool userEnabled, bool computerEnabled)
    {
        var cs = new ChangeSet { TargetSamAccountName = current.DisplayName };
        if (!string.Equals(current.Description, newDescription, StringComparison.Ordinal)) cs.Changes.Add(new("Description", current.Description, newDescription));
        if (current.UserSettingsEnabled != userEnabled) cs.Changes.Add(new("UserSettingsEnabled", current.UserSettingsEnabled.ToString(), userEnabled.ToString()));
        if (current.ComputerSettingsEnabled != computerEnabled) cs.Changes.Add(new("ComputerSettingsEnabled", current.ComputerSettingsEnabled.ToString(), computerEnabled.ToString()));
        return cs;
    }

    public void UpdateGpo(string id, string description, bool userEnabled, bool computerEnabled)
    {
        if (!_gpos.TryGetValue(id, out var g)) throw new InvalidOperationException("GPO not found");
        g.Description = description;
        g.UserSettingsEnabled = userEnabled;
        g.ComputerSettingsEnabled = computerEnabled;
    }

    public IReadOnlyList<GroupGpoStatus> GetAppliedGposForGroup(string groupName)
    {
        return _groupGpoMap.TryGetValue(groupName, out var list) ? list : new List<GroupGpoStatus>();
    }

    public IReadOnlyList<TargetGpoStatus> GetAppliedGposForUserAndComputer(string userSamAccountName, string computerName)
    {
        return _targetGpoStatuses.Where(x =>
            x.UserSamAccountName.Equals(userSamAccountName, StringComparison.OrdinalIgnoreCase) &&
            x.ComputerName.Equals(computerName, StringComparison.OrdinalIgnoreCase)).ToList();
    }
}
