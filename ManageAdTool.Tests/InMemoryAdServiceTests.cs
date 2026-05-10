using Xunit;
using ManageAdTool.Models;
using ManageAdTool.Services;

namespace ManageAdTool.Tests;

public class InMemoryAdServiceTests
{
    private readonly InMemoryAdService _service = new();

    [Fact]
    public void SearchUsers_FindsMatchingUserWithoutAdConnection()
    {
        var users = _service.SearchUsers("sato");

        var user = Assert.Single(users);
        Assert.Equal("sato.taro", user.SamAccountName);
    }

    [Fact]
    public void SearchComputers_FindsMatchingComputerWithoutAdConnection()
    {
        var computers = _service.SearchComputers(new AdComputerSearchCriteria { Keyword = "PC-001" });

        var computer = Assert.Single(computers);
        Assert.Equal("PC-001", computer.Name);
    }

    [Fact]
    public void SearchGroups_FindsMatchingGroupWithoutAdConnection()
    {
        var groups = _service.SearchGroups("Office");

        var group = Assert.Single(groups);
        Assert.Equal("GG_OfficeUsers", group.Name);
    }

    [Fact]
    public void GetGroupMembers_ReturnsDirectUserMembers()
    {
        var members = _service.GetGroupMembers("GG_OfficeUsers");

        Assert.Equal(new[] { "sato.taro", "tanaka.hana" }, members.Select(m => m.SamAccountName).OrderBy(x => x).ToArray());
    }

    [Fact]
    public void SimulateGpo_ReturnsDeterministicDummyResults()
    {
        var results = _service.SimulateGpo("sato.taro", "PC-001");

        Assert.Contains(results, result => result.GpoName == "Default Domain Policy" && result.AppliesTo == "両方");
        Assert.Contains(results, result => result.GpoName == "User Desktop Policy" && result.AppliesTo == "ユーザー");
        Assert.Contains(results, result => result.GpoName == "Computer Security Policy" && result.AppliesTo == "コンピュータ" && result.Enforced);
    }
}
