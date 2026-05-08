using ManageAdTool.Models;

namespace ManageAdTool.Services;

public interface IAdService
{
    IReadOnlyList<AdUser> SearchUsers(string keyword);
    AdUser? GetUser(string samAccountName);
    IReadOnlyList<string> GetUserGroups(string samAccountName);
    ChangeSet BuildChangeSet(AdUser current, string newMail, string newDepartment, string newTitle);
}
