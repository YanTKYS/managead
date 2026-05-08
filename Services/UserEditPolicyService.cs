using ManageAdTool.Models;

namespace ManageAdTool.Services;

public class UserEditPolicyService
{
    public (bool canEdit, string reason) Evaluate(AdUser? user, AppPolicy policy, bool isSessionActive = false)
    {
        if (user is null) return (false, "ユーザー未選択");

        if (policy.ExcludedSamAccountNames.Any(x => string.Equals(x, user.SamAccountName, StringComparison.OrdinalIgnoreCase)))
            return (false, "編集不可: 除外アカウント");

        if (policy.AllowedTargetOuDns.Count > 0 && !policy.AllowedTargetOuDns.Any(ou => IsUnderOu(user.DistinguishedName, ou)))
            return (false, "編集不可: 許可OU外");

        var editable = new HashSet<string>(policy.EditableAttributes, StringComparer.OrdinalIgnoreCase);
        var required = new[] { "mail", "department", "title" };
        var missing = required.Where(r => !editable.Contains(r)).ToList();
        if (missing.Count > 0)
            return (false, $"編集不可: EditableAttributes不足 ({string.Join(",", missing)})");

        if (string.Equals(policy.ServiceMode, "DirectoryReadOnly", StringComparison.OrdinalIgnoreCase) && !isSessionActive)
            return (false, "DirectoryReadOnly モードのため参照のみ（編集にはログインが必要）");

        return (true, "編集可能");
    }

    private static bool IsUnderOu(string distinguishedName, string ouDn)
        => !string.IsNullOrWhiteSpace(distinguishedName)
            && !string.IsNullOrWhiteSpace(ouDn)
            && distinguishedName.EndsWith($",{ouDn}", StringComparison.OrdinalIgnoreCase);
}
