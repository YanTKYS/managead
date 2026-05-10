using Xunit;
using ManageAdTool.Models;
using ManageAdTool.Services;

namespace ManageAdTool.Tests;

public class DirectoryServicesWriteServiceValidationTests
{
    [Fact]
    public void UpdateUserAttributes_RejectsDisallowedLdapAttributeBeforeAdConnection()
    {
        var service = new DirectoryServicesAdUserAttributeWriteService();
        var changeSet = new ChangeSet
        {
            Changes = new()
            {
                new FieldChange("部署", "old", "new") { LdapAttribute = "department" }
            }
        };

        var result = service.UpdateUserAttributes("CN=NoConnect,DC=example,DC=local", changeSet, "user", "password");

        Assert.False(result.Success);
        Assert.Contains("許可されていない属性", result.ErrorMessage);
        Assert.Contains("department", result.ErrorMessage);
    }

    [Fact]
    public void UpdateComputerDescription_RejectsAttributesOtherThanDescriptionBeforeAdConnection()
    {
        var service = new DirectoryServicesAdComputerAttributeWriteService();
        var changeSet = new ChangeSet
        {
            Changes = new()
            {
                new FieldChange("OS", "old", "new") { LdapAttribute = "operatingSystem" }
            }
        };

        var result = service.UpdateComputerDescription("CN=NoConnect,DC=example,DC=local", changeSet, "user", "password");

        Assert.False(result.Success);
        Assert.Contains("description のみ", result.ErrorMessage);
        Assert.Contains("operatingSystem", result.ErrorMessage);
    }

    [Fact]
    public void UpdateComputerDescription_RejectsBlankDescriptionBeforeAdConnection()
    {
        var service = new DirectoryServicesAdComputerAttributeWriteService();
        var changeSet = new ChangeSet
        {
            Changes = new()
            {
                new FieldChange("説明 (description)", "old", " ") { LdapAttribute = "description" }
            }
        };

        var result = service.UpdateComputerDescription("CN=NoConnect,DC=example,DC=local", changeSet, "user", "password");

        Assert.False(result.Success);
        Assert.Contains("空文字更新は禁止", result.ErrorMessage);
        Assert.Contains("description", result.ErrorMessage);
    }
}
