using ManageAdTool.Models;

namespace ManageAdTool.Services;

// 将来機能（MVP非対象）
public interface IAdFutureOperations
{
    IReadOnlyList<AdUser> GetExpiredUsers(DateTimeOffset now);
    void ExtendAccountExpiration(IEnumerable<string> samAccountNames, DateTimeOffset newExpiry);
    void DisableUsers(IEnumerable<string> samAccountNames);
    IReadOnlyList<AdUser> GetDisabledUsers();
    void RetireUsers(IEnumerable<string> samAccountNames, string retiredUsersOuDn);

    IReadOnlyList<AdUser> GetUsersNotLoggedInForDays(int days, DateTimeOffset now);
    IReadOnlyList<AdComputer> GetComputersNotBootedForDays(int days, DateTimeOffset now);
    void DisableComputers(IEnumerable<string> computerNames);

    IReadOnlyList<AdGroup> GetGroups();
    IReadOnlyList<AdUser> GetDirectGroupMembers(string groupName);
    void AddDirectGroupMember(string groupName, string userSamAccountName);
    void RemoveDirectGroupMember(string groupName, string userSamAccountName);

    IReadOnlyList<GpoPolicy> SearchGpos(string keyword);
    ChangeSet BuildGpoChangeSet(GpoPolicy current, string newDescription, bool userEnabled, bool computerEnabled);
    void UpdateGpo(string id, string description, bool userEnabled, bool computerEnabled);
    IReadOnlyList<GroupGpoStatus> GetAppliedGposForGroup(string groupName);
    IReadOnlyList<TargetGpoStatus> GetAppliedGposForUserAndComputer(string userSamAccountName, string computerName);
}
