using Xunit;
using ManageAdTool.Models;
using ManageAdTool.Services;

namespace ManageAdTool.Tests;

public class AppPolicyProviderTests
{
    [Fact]
    public void Load_ReadsPrimaryPolicyValues()
    {
        var path = WritePolicyJson("""
        {
          "AppPolicy": {
            "ServiceMode": "DirectoryReadOnly",
            "LogPath": "C:\\logs\\audit.jsonl",
            "RetiredUsersOuDn": "OU=Retired,DC=example,DC=local",
            "EditorAuthMode": "DomainAdmins",
            "AdminGroupDn": "CN=Domain Admins,CN=Users,DC=example,DC=local",
            "AllowNestedAdminGroupMembership": true,
            "MaxSearchResults": 50,
            "MaxLogDisplayRows": 25,
            "EditSessionMinutes": 10,
            "EnableOperationSupport": false,
            "AllowedTargetOuDns": ["OU=Users,DC=example,DC=local"],
            "ExcludedSamAccountNames": ["svc.noedit"],
            "EditableAttributes": ["mail", "displayName", "sn", "givenName"],
            "AllowedComputerOuDns": ["OU=Computers,DC=example,DC=local"],
            "EditableGroupOuDns": ["OU=Groups,DC=example,DC=local"],
            "ProtectedGroupNames": ["Domain Admins"]
          }
        }
        """);

        var policy = AppPolicyProvider.Load(path);

        Assert.Equal("DirectoryReadOnly", policy.ServiceMode);
        Assert.Equal(@"C:\logs\audit.jsonl", policy.LogPath);
        Assert.Equal("OU=Retired,DC=example,DC=local", policy.RetiredUsersOuDn);
        Assert.Equal("DomainAdmins", policy.EditorAuthMode);
        Assert.Equal("CN=Domain Admins,CN=Users,DC=example,DC=local", policy.AdminGroupDn);
        Assert.True(policy.AllowNestedAdminGroupMembership);
        Assert.Equal(50, policy.MaxSearchResults);
        Assert.Equal(25, policy.MaxLogDisplayRows);
        Assert.Equal(10, policy.EditSessionMinutes);
        Assert.False(policy.EnableOperationSupport);
        Assert.Equal(new[] { "OU=Users,DC=example,DC=local" }, policy.AllowedTargetOuDns);
        Assert.Equal(new[] { "svc.noedit" }, policy.ExcludedSamAccountNames);
        Assert.Equal(new[] { "mail", "displayName", "sn", "givenName" }, policy.EditableAttributes);
        Assert.Equal(new[] { "OU=Computers,DC=example,DC=local" }, policy.AllowedComputerOuDns);
        Assert.Equal(new[] { "OU=Groups,DC=example,DC=local" }, policy.EditableGroupOuDns);
        Assert.Equal(new[] { "Domain Admins" }, policy.ProtectedGroupNames);
    }

    [Fact]
    public void Load_FallsBackForNonPositiveAndNonNumericLimits()
    {
        var nonPositivePath = WritePolicyJson("""
        {
          "AppPolicy": {
            "MaxSearchResults": 0,
            "MaxLogDisplayRows": -1,
            "EditSessionMinutes": 0
          }
        }
        """);
        var invalidTypePath = WritePolicyJson("""
        {
          "AppPolicy": {
            "MaxSearchResults": "100",
            "MaxLogDisplayRows": "1000",
            "EditSessionMinutes": "15"
          }
        }
        """);

        var defaults = new AppPolicy();
        var nonPositive = AppPolicyProvider.Load(nonPositivePath);
        var invalidType = AppPolicyProvider.Load(invalidTypePath);

        Assert.Equal(defaults.MaxSearchResults, nonPositive.MaxSearchResults);
        Assert.Equal(defaults.MaxLogDisplayRows, nonPositive.MaxLogDisplayRows);
        Assert.Equal(defaults.EditSessionMinutes, nonPositive.EditSessionMinutes);
        Assert.Equal(defaults.MaxSearchResults, invalidType.MaxSearchResults);
        Assert.Equal(defaults.MaxLogDisplayRows, invalidType.MaxLogDisplayRows);
        Assert.Equal(defaults.EditSessionMinutes, invalidType.EditSessionMinutes);
    }

    private static string WritePolicyJson(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"managead-policy-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }
}
