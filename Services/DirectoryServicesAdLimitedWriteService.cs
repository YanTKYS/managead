using System.DirectoryServices;
using ManageAdTool.Models;

namespace ManageAdTool.Services;

public class DirectoryServicesAdLimitedWriteService : DirectoryServicesAdReadService
{
    public DirectoryServicesAdLimitedWriteService(AppPolicy policy) : base(policy)
    {
    }

    public override void UpdateAttributes(string samAccountName, string mail, string department, string title)
    {
        if (Policy.AllowedTargetOuDns.Count == 0)
            throw new InvalidOperationException("AllowedTargetOuDns が未設定のため更新できません。");

        if (IsExcluded(samAccountName))
            throw new InvalidOperationException("除外アカウントのため更新できません。");

        var current = GetUser(samAccountName) ?? throw new InvalidOperationException("対象ユーザーが存在しないため更新できません。");
        if (!IsInAllowedTargetOu(current.DistinguishedName))
            throw new InvalidOperationException("対象ユーザーが許可OU外のため更新できません。");

        using var entry = new DirectoryEntry($"LDAP://{current.DistinguishedName}");
        SetStringProperty(entry, "mail", mail);
        SetStringProperty(entry, "department", department);
        SetStringProperty(entry, "title", title);
        entry.CommitChanges();
    }

    private static void SetStringProperty(DirectoryEntry entry, string name, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            entry.Properties[name].Clear();
            return;
        }

        entry.Properties[name].Value = value;
    }
}
