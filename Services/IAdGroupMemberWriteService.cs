using ManageAdTool.Models;

namespace ManageAdTool.Services;

public interface IAdGroupMemberWriteService
{
    UpdateResult UpdateGroupMembers(string groupDn, IReadOnlyList<string> addUserDns, IReadOnlyList<string> removeUserDns, string domainUser, string password);
}
