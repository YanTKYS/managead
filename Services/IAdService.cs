using ManageAdTool.Models;

namespace ManageAdTool.Services;

public interface IAdService
{
    IReadOnlyList<AdUser> SearchUsers(string keyword);
    AdUser? GetUser(string samAccountName);
    ChangeSet BuildChangeSet(AdUser current, string newMail, string newDepartment, string newTitle);
    void UpdateAttributes(string samAccountName, string mail, string department, string title);

    IReadOnlyList<AdUser> GetExpiredUsers(DateTimeOffset now);
    void ExtendAccountExpiration(IEnumerable<string> samAccountNames, DateTimeOffset newExpiry);
    void DisableUsers(IEnumerable<string> samAccountNames);
    IReadOnlyList<AdUser> GetUsersNotLoggedInForDays(int days, DateTimeOffset now);

    IReadOnlyList<AdComputer> GetComputersNotBootedForDays(int days, DateTimeOffset now);
    void DisableComputers(IEnumerable<string> computerNames);

    IReadOnlyList<string> GetUserGroups(string samAccountName);

    IReadOnlyList<AdGroup> GetGroups();
    IReadOnlyList<AdUser> GetDirectGroupMembers(string groupName);
    void AddDirectGroupMember(string groupName, string userSamAccountName);
    void RemoveDirectGroupMember(string groupName, string userSamAccountName);

    ChangeSet BuildGroupMembershipChangeSet(string samAccountName, IEnumerable<string> groupsToAdd, IEnumerable<string> groupsToRemove);
    void UpdateUserGroups(string samAccountName, IEnumerable<string> groupsToAdd, IEnumerable<string> groupsToRemove);

    IReadOnlyList<AdComputer> SearchComputers(string keyword);
    AdComputer? GetComputer(string name);

    IReadOnlyList<GpoPolicy> SearchGpos(string keyword);
    ChangeSet BuildGpoChangeSet(GpoPolicy current, string newDescription, bool userEnabled, bool computerEnabled);
    void UpdateGpo(string id, string description, bool userEnabled, bool computerEnabled);

    IReadOnlyList<GroupGpoStatus> GetAppliedGposForGroup(string groupName);
    IReadOnlyList<TargetGpoStatus> GetAppliedGposForUserAndComputer(string userSamAccountName, string computerName);
}
