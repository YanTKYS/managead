using System.DirectoryServices;
using ManageAdTool.Models;

namespace ManageAdTool.Services;

public class DirectoryServicesAdGroupMemberWriteService : IAdGroupMemberWriteService
{
    public UpdateResult UpdateGroupMembers(string groupDn, IReadOnlyList<string> addUserDns, IReadOnlyList<string> removeUserDns, string domainUser, string password)
    {
        if (addUserDns.Count == 0 && removeUserDns.Count == 0)
            return new UpdateResult(false, "変更がありません。");

        try
        {
            using var entry = new DirectoryEntry($"LDAP://{groupDn}", domainUser, password);
            foreach (var dn in addUserDns)
                entry.Properties["member"].Add(dn);
            foreach (var dn in removeUserDns)
                entry.Properties["member"].Remove(dn);
            entry.CommitChanges();
            return new UpdateResult(true, null);
        }
        catch (System.Runtime.InteropServices.COMException ex) when ((uint)ex.HResult == 0x80072035)
        {
            return new UpdateResult(false, "ADのポリシーにより更新が拒否されました（UNWILLING_TO_PERFORM）。権限または制約（グループスコープ・タイプ等）を確認してください。");
        }
        catch (System.Runtime.InteropServices.COMException ex) when ((uint)ex.HResult == 0x8007052E || (uint)ex.HResult == 0x80070005)
        {
            return new UpdateResult(false, "認証エラーまたはアクセス権限がありません。ドメイン管理者アカウントで再試行してください。");
        }
        catch (Exception ex)
        {
            return new UpdateResult(false, $"更新処理中にエラーが発生しました（{ex.GetType().Name}）。ネットワーク接続またはAD設定を確認してください。");
        }
    }
}
