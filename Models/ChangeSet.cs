namespace ManageAdTool.Models;

public record FieldChange(string Field, string Before, string After)
{
    // LDAP属性名。監査ログへの記録と書き込みサービスの振り分けに使用する。
    public string LdapAttribute { get; init; } = string.Empty;
}

public class ChangeSet
{
    public string TargetSamAccountName { get; init; } = string.Empty;
    public string TargetDisplayName { get; init; } = string.Empty;
    public List<FieldChange> Changes { get; init; } = new();
}
