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
            Enabled = true, DistinguishedName = "CN=Taro Sato,OU=Users,DC=example,DC=local",
            LastLogonAt = DateTimeOffset.UtcNow.AddHours(-5),
            LastLogonComputer = "PC-001",
            Groups = new[] { "GG_OfficeUsers", "GG_InfoPolicy" }
        }
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
}
