using System.DirectoryServices;
using System.Runtime.InteropServices;
using ManageAdTool.Models;

namespace ManageAdTool.Services;

public class DirectoryServicesAdUserAttributeWriteService : IAdUserAttributeWriteService
{
    private static readonly HashSet<string> AllowedFields =
        new(StringComparer.OrdinalIgnoreCase) { "Mail", "Department", "Title" };

    private static readonly Dictionary<string, string> FieldToLdapAttr =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Mail"] = "mail",
            ["Department"] = "department",
            ["Title"] = "title"
        };

    public UpdateResult UpdateUserAttributes(string targetDn, ChangeSet changeSet, string domainUser, string password)
    {
        var disallowed = changeSet.Changes.Select(c => c.Field).Where(f => !AllowedFields.Contains(f)).ToList();
        if (disallowed.Count > 0)
            return new UpdateResult(false, $"許可されていない属性への更新要求: {string.Join(", ", disallowed)}");

        try
        {
            using var entry = new DirectoryEntry($"LDAP://{targetDn}", domainUser, password, AuthenticationTypes.Secure);

            foreach (var change in changeSet.Changes)
            {
                var attr = FieldToLdapAttr[change.Field];
                entry.Properties[attr].Value = change.After;
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
