using Xunit;
using ManageAdTool.Models;
using ManageAdTool.Services;

namespace ManageAdTool.Tests;

public class ChangeSetTests
{
    private readonly InMemoryAdService _service = new();

    [Fact]
    public void BuildChangeSet_ReturnsNoChangesWhenValuesAreUnchanged()
    {
        var user = SampleUser();

        var changeSet = _service.BuildChangeSet(user, user.Mail, user.DisplayName, user.Surname, user.GivenName);

        Assert.Empty(changeSet.Changes);
    }

    [Fact]
    public void BuildChangeSet_CreatesMailDisplayNameSurnameAndGivenNameChangesWithLdapAttributes()
    {
        var changeSet = _service.BuildChangeSet(
            SampleUser(),
            "new.mail@example.local",
            "New Display",
            "NewSurname",
            "NewGiven");

        Assert.Equal("sato.taro", changeSet.TargetSamAccountName);
        Assert.Collection(changeSet.Changes,
            change => AssertFieldChange(change, "メールアドレス", "mail", "old.mail@example.local", "new.mail@example.local"),
            change => AssertFieldChange(change, "表示名", "displayName", "Old Display", "New Display"),
            change => AssertFieldChange(change, "姓", "sn", "OldSurname", "NewSurname"),
            change => AssertFieldChange(change, "名", "givenName", "OldGiven", "NewGiven"));
    }

    [Fact]
    public void BuildComputerChangeSet_CreatesDescriptionChangeWithLdapAttribute()
    {
        var computer = new AdComputer
        {
            Name = "PC-TEST",
            DnsHostName = "pc-test.example.local",
            Description = "before"
        };

        var changeSet = _service.BuildComputerChangeSet(computer, "after");

        var change = Assert.Single(changeSet.Changes);
        Assert.Equal("説明 (description)", change.Field);
        Assert.Equal("description", change.LdapAttribute);
        Assert.Equal("before", change.Before);
        Assert.Equal("after", change.After);
    }

    private static AdUser SampleUser() => new()
    {
        SamAccountName = "sato.taro",
        DisplayName = "Old Display",
        Surname = "OldSurname",
        GivenName = "OldGiven",
        Mail = "old.mail@example.local"
    };

    private static void AssertFieldChange(FieldChange change, string field, string ldapAttribute, string before, string after)
    {
        Assert.Equal(field, change.Field);
        Assert.Equal(ldapAttribute, change.LdapAttribute);
        Assert.Equal(before, change.Before);
        Assert.Equal(after, change.After);
    }
}
