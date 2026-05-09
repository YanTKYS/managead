using ManageAdTool.Models;

namespace ManageAdTool.Services;

public interface IAdComputerAttributeWriteService
{
    UpdateResult UpdateComputerDescription(string targetDn, ChangeSet changeSet, string domainUser, string password);
}
