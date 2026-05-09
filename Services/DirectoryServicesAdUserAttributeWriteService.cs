using System.DirectoryServices;
using System.Runtime.InteropServices;
using ManageAdTool.Models;

namespace ManageAdTool.Services;

public class DirectoryServicesAdUserAttributeWriteService : IAdUserAttributeWriteService
{
    // v0.4.2 更新可能な LDAP 属性のホワイトリスト。EditableAttributeDefs.All と対応する。
    // FieldChange.LdapAttribute で検証する。Field（表示名）では検証しない。
    private static readonly HashSet<string> AllowedLdapAttributes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "mail",        // メールアドレス
            "displayName", // 表示名
            "sn",          // 姓
            "givenName",   // 名
        };

    public UpdateResult UpdateUserAttributes(string targetDn, ChangeSet changeSet, string domainUser, string password)
    {
        var disallowed = changeSet.Changes
            .Where(c => !AllowedLdapAttributes.Contains(c.LdapAttribute))
            .Select(c => string.IsNullOrEmpty(c.LdapAttribute) ? c.Field : c.LdapAttribute)
            .ToList();
        if (disallowed.Count > 0)
            return new UpdateResult(false, $"許可されていない属性への更新要求: {string.Join(", ", disallowed)}");

        try
        {
            using var entry = new DirectoryEntry($"LDAP://{targetDn}", domainUser, password, AuthenticationTypes.Secure);
            foreach (var change in changeSet.Changes)
            {
                entry.Properties[change.LdapAttribute].Value = change.After;
            }
            entry.CommitChanges();
            return new UpdateResult(true);
        }
        catch (COMException ex) when (ex.HResult == unchecked((int)0x80072035))
        {
            return new UpdateResult(false, "ADが操作を拒否しました（制約違反の可能性があります）。");
        }
        catch (COMException ex) when (
            ex.HResult == unchecked((int)0x8007052E) ||
            ex.HResult == unchecked((int)0x80070005))
        {
            return new UpdateResult(false, "ADへの書き込み権限が不足している可能性があります。");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("AD属性更新の実行中にエラーが発生しました。", ex);
        }
    }
}
