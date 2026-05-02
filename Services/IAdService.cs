using ManageAdTool.Models;

namespace ManageAdTool.Services;

public interface IAdService
{
    IReadOnlyList<AdUser> SearchUsers(string keyword);
    AdUser? GetUser(string samAccountName);
    ChangeSet BuildChangeSet(AdUser current, string newMail, string newDepartment, string newTitle);
    void UpdateAttributes(string samAccountName, string mail, string department, string title);
}
