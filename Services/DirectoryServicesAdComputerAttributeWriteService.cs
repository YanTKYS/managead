using System.DirectoryServices;
using ManageAdTool.Models;

namespace ManageAdTool.Services;

public class DirectoryServicesAdComputerAttributeWriteService : IAdComputerAttributeWriteService
{
    private static readonly HashSet<string> AllowedLdapAttributes =
        new(StringComparer.OrdinalIgnoreCase) { "description" };

    public UpdateResult UpdateComputerDescription(string targetDn, ChangeSet changeSet, string domainUser, string password)
    {
        var disallowed = changeSet.Changes
            .Where(c => !AllowedLdapAttributes.Contains(c.LdapAttribute))
            .Select(c => string.IsNullOrEmpty(c.LdapAttribute) ? c.Field : c.LdapAttribute)
            .ToList();
        if (disallowed.Count > 0)
            return new UpdateResult(false, $"許可されていない属性への更新要求: {string.Join(", ", disallowed)}。コンピュータ編集は description のみ可能です。");

        try
        {
            using var entry = new DirectoryEntry($"LDAP://{targetDn}", domainUser, password);
            foreach (var change in changeSet.Changes)
            {
                if (string.IsNullOrWhiteSpace(change.After))
                    entry.Properties[change.LdapAttribute].Clear();
                else
                    entry.Properties[change.LdapAttribute].Value = change.After;
            }
            entry.CommitChanges();
            return new UpdateResult(true, null);
        }
        catch (System.Runtime.InteropServices.COMException ex) when ((uint)ex.HResult == 0x80072035)
        {
            return new UpdateResult(false, "ADのポリシーにより更新が拒否されました（UNWILLING_TO_PERFORM）。属性の値や権限を確認してください。");
        }
        catch (System.Runtime.InteropServices.COMException ex) when ((uint)ex.HResult == 0x8007052E || (uint)ex.HResult == 0x80070005)
        {
            return new UpdateResult(false, "認証エラーまたはアクセス権限がありません。ドメイン管理者アカウントで再試行してください。");
        }
        catch (Exception ex)
        {
            return new UpdateResult(false, $"更新処理中にエラーが発生しました（{ex.GetType().Name}）。ネットワーク接続またはAD設定を確認してください。");
        }
        finally
        {
            // パスワードは認証と書き込みに即時使用し、参照を切る。永続化しない。
        }
    }
}
