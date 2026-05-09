namespace ManageAdTool.Models;

public record AttributeDef(string DisplayName, string LdapAttribute);

/// <summary>
/// v0.4.2 更新可能属性の中央定義。表示名とLDAP属性名の対応はここだけで管理する。
/// </summary>
public static class EditableAttributeDefs
{
    public static readonly AttributeDef Mail = new("メールアドレス", "mail");
    public static readonly AttributeDef DisplayName = new("表示名", "displayName");
    public static readonly AttributeDef Surname = new("姓", "sn");
    public static readonly AttributeDef GivenName = new("名", "givenName");

    public static readonly IReadOnlyList<AttributeDef> All = new[]
    {
        Mail, DisplayName, Surname, GivenName
    };

    // v0.4.2 更新対象外（設計中）
    // ユーザー名 (sAMAccountName / userPrincipalName / cn): 高リスクのため別途設計
    // 有効期限 (accountExpires): 専用UI・強い確認が必要のため別途設計
}
