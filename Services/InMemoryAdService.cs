using ManageAdTool.Models;

namespace ManageAdTool.Services;

public class InMemoryAdService : IAdService
{
    private readonly Dictionary<string, AdUser> _users = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sato.taro"] = new AdUser
        {
            SamAccountName = "sato.taro",
            DisplayName = "佐藤 太郎",
            Name = "Taro Sato",
            Mail = "taro.sato@example.local",
            Department = "情報政策課",
            Title = "主任",
            Enabled = true,
            DistinguishedName = "CN=Taro Sato,OU=Users,DC=example,DC=local",
            Groups = new[] { "GG_OfficeUsers", "GG_InfoPolicy" }
        }
    };

    public IReadOnlyList<AdUser> SearchUsers(string keyword)
        => _users.Values
            .Where(u => u.SamAccountName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                     || u.DisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                     || u.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                     || u.Mail.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .ToList();

    public AdUser? GetUser(string samAccountName)
        => _users.TryGetValue(samAccountName, out var user) ? user : null;

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
        u.Mail = mail;
        u.Department = department;
        u.Title = title;
    }
}
