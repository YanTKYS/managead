using ManageAdTool.Models;

namespace ManageAdTool.Services;

public interface IAdService
{
    IReadOnlyList<AdUser> SearchUsers(string keyword);
    IReadOnlyList<AdUser> SearchUsers(AdUserSearchCriteria criteria);
    AdUser? GetUser(string samAccountName);
    IReadOnlyList<string> GetUserGroups(string samAccountName);
    IReadOnlyList<AdGroup> SearchGroups(string keyword);
    IReadOnlyList<AdUser> GetGroupMembers(string groupName);
    ChangeSet BuildChangeSet(AdUser current, string newMail, string newDisplayName, string newSurname, string newGivenName);
}
