using ManageAdTool.Models;

namespace ManageAdTool.Services;

public interface IAdUserAttributeWriteService
{
    UpdateResult UpdateUserAttributes(string targetDn, ChangeSet changeSet, string domainUser, string password);
}
