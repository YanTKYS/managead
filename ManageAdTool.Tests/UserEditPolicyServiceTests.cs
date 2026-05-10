using Xunit;
using ManageAdTool.Models;
using ManageAdTool.Services;

namespace ManageAdTool.Tests;

public class UserEditPolicyServiceTests
{
    private readonly UserEditPolicyService _service = new();

    [Fact]
    public void Evaluate_DisallowsWhenUserIsNotSelected()
    {
        var result = _service.Evaluate(null, EditablePolicy());

        Assert.False(result.canEdit);
        Assert.Contains("ユーザー未選択", result.reason);
    }

    [Fact]
    public void Evaluate_DisallowsExcludedUser()
    {
        var policy = EditablePolicy();
        policy.ExcludedSamAccountNames.Add("sato.taro");

        var result = _service.Evaluate(AllowedUser(), policy);

        Assert.False(result.canEdit);
        Assert.Contains("除外", result.reason);
    }

    [Fact]
    public void Evaluate_DisallowsUserOutsideAllowedOu()
    {
        var policy = EditablePolicy();
        policy.AllowedTargetOuDns.Clear();
        policy.AllowedTargetOuDns.Add("OU=Managed,DC=example,DC=local");

        var result = _service.Evaluate(AllowedUser(), policy);

        Assert.False(result.canEdit);
        Assert.Contains("許可OU外", result.reason);
    }

    [Fact]
    public void Evaluate_DisallowsWhenEditableAttributesAreMissing()
    {
        var policy = EditablePolicy();
        policy.EditableAttributes.Remove("givenName");

        var result = _service.Evaluate(AllowedUser(), policy);

        Assert.False(result.canEdit);
        Assert.Contains("EditableAttributes不足", result.reason);
    }

    [Fact]
    public void Evaluate_AllowsOnlyWhenPolicyConditionsAreSatisfied()
    {
        var result = _service.Evaluate(AllowedUser(), EditablePolicy());

        Assert.True(result.canEdit);
        Assert.Equal("編集可能", result.reason);
    }

    [Fact]
    public void Evaluate_DisallowsDirectoryReadOnlyWhenSessionIsNotActive()
    {
        var policy = EditablePolicy();
        policy.ServiceMode = "DirectoryReadOnly";

        var result = _service.Evaluate(AllowedUser(), policy, isSessionActive: false);

        Assert.False(result.canEdit);
        Assert.Contains("DirectoryReadOnly", result.reason);
    }

    [Fact]
    public void Evaluate_AllowsDirectoryReadOnlyWhenSessionIsActiveAndConditionsAreSatisfied()
    {
        var policy = EditablePolicy();
        policy.ServiceMode = "DirectoryReadOnly";

        var result = _service.Evaluate(AllowedUser(), policy, isSessionActive: true);

        Assert.True(result.canEdit);
        Assert.Equal("編集可能", result.reason);
    }

    [Fact]
    public void EvaluateWrite_DisallowsWhenSessionIsNotActive()
    {
        var result = _service.EvaluateWrite(AllowedUser(), EditablePolicy(), isSessionActive: false);

        Assert.False(result.canWrite);
        Assert.Contains("期限切れ", result.reason);
    }

    [Fact]
    public void EvaluateWrite_DisallowsWhenAllowedTargetOuDnsIsEmpty()
    {
        var policy = EditablePolicy();
        policy.AllowedTargetOuDns.Clear();

        var result = _service.EvaluateWrite(AllowedUser(), policy, isSessionActive: true);

        Assert.False(result.canWrite);
        Assert.Contains("AllowedTargetOuDns", result.reason);
    }

    [Fact]
    public void EvaluateWrite_DisallowsWhenUserIsNotSelected()
    {
        var result = _service.EvaluateWrite(null, EditablePolicy(), isSessionActive: true);

        Assert.False(result.canWrite);
        Assert.Contains("ユーザー未選択", result.reason);
    }

    [Fact]
    public void EvaluateWrite_DisallowsExcludedUser()
    {
        var policy = EditablePolicy();
        policy.ExcludedSamAccountNames.Add("sato.taro");

        var result = _service.EvaluateWrite(AllowedUser(), policy, isSessionActive: true);

        Assert.False(result.canWrite);
        Assert.Contains("除外", result.reason);
    }

    [Fact]
    public void EvaluateWrite_DisallowsUserOutsideAllowedOu()
    {
        var policy = EditablePolicy();
        policy.AllowedTargetOuDns.Clear();
        policy.AllowedTargetOuDns.Add("OU=Managed,DC=example,DC=local");

        var result = _service.EvaluateWrite(AllowedUser(), policy, isSessionActive: true);

        Assert.False(result.canWrite);
        Assert.Contains("許可OU外", result.reason);
    }

    [Fact]
    public void EvaluateWrite_AllowsOnlyWhenWriteConditionsAreSatisfied()
    {
        var result = _service.EvaluateWrite(AllowedUser(), EditablePolicy(), isSessionActive: true);

        Assert.True(result.canWrite);
        Assert.Equal("更新可能", result.reason);
    }

    private static AppPolicy EditablePolicy() => new()
    {
        ServiceMode = "InMemory",
        AllowedTargetOuDns = new() { "OU=Users,DC=example,DC=local" },
        EditableAttributes = new() { "mail", "displayName", "sn", "givenName" }
    };

    private static AdUser AllowedUser() => new()
    {
        SamAccountName = "sato.taro",
        DistinguishedName = "CN=Taro Sato,OU=Users,DC=example,DC=local"
    };
}
