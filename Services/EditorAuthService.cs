using System.DirectoryServices;
using System.Runtime.InteropServices;
using ManageAdTool.Models;

namespace ManageAdTool.Services;

public class EditorAuthService
{
    public AuthResult TryAuthenticate(string domainUser, string password, AppPolicy policy)
    {
        if (string.IsNullOrWhiteSpace(domainUser) || string.IsNullOrWhiteSpace(password))
            return AuthResult.Fail("ユーザー名またはパスワードが未入力です");

        if (string.IsNullOrWhiteSpace(policy.AdminGroupDn))
            return AuthResult.Fail("AdminGroupDn が設定されていません（appsettings.json を確認してください）");

        var samAccountName = ExtractSamAccountName(domainUser);

        try
        {
            using var entry = new DirectoryEntry("LDAP://", domainUser, password, AuthenticationTypes.Secure);
            using var searcher = new DirectorySearcher(entry)
            {
                Filter = $"(&(objectClass=user)(samAccountName={EscapeLdap(samAccountName)}))",
                SizeLimit = 1
            };
            searcher.PropertiesToLoad.Add("samAccountName");

            var result = searcher.FindOne();
            if (result is null)
                return AuthResult.Fail("認証ユーザーが見つかりません");

            var resolvedUser = result.Properties["samAccountName"]?.Count > 0
                ? result.Properties["samAccountName"][0]?.ToString() ?? domainUser
                : domainUser;

            var isMember = policy.AllowNestedAdminGroupMembership
                ? CheckNestedMembership(entry, samAccountName, policy.AdminGroupDn)
                : CheckDirectMembership(entry, samAccountName, policy.AdminGroupDn);

            return isMember
                ? AuthResult.DomainAdmin(resolvedUser)
                : AuthResult.NotAdmin(resolvedUser);
        }
        catch (COMException ex) when (
            ex.HResult == unchecked((int)0x8007052E) ||
            ex.HResult == unchecked((int)0x80070056) ||
            ex.HResult == unchecked((int)0x80070005))
        {
            return AuthResult.Fail("ユーザー名またはパスワードが正しくありません");
        }
        catch (Exception ex)
        {
            return AuthResult.Fail($"認証エラー: {ex.Message}");
        }
    }

    private static bool CheckDirectMembership(DirectoryEntry boundEntry, string samAccountName, string adminGroupDn)
    {
        using var searcher = new DirectorySearcher(boundEntry)
        {
            Filter = $"(&(objectClass=user)(samAccountName={EscapeLdap(samAccountName)})(memberOf={EscapeLdap(adminGroupDn)}))",
            SizeLimit = 1
        };
        searcher.PropertiesToLoad.Add("samAccountName");
        return searcher.FindOne() is not null;
    }

    private static bool CheckNestedMembership(DirectoryEntry boundEntry, string samAccountName, string adminGroupDn)
    {
        using var searcher = new DirectorySearcher(boundEntry)
        {
            Filter = $"(&(objectClass=user)(samAccountName={EscapeLdap(samAccountName)})(memberOf:1.2.840.113556.1.4.1941:={EscapeLdap(adminGroupDn)}))",
            SizeLimit = 1
        };
        searcher.PropertiesToLoad.Add("samAccountName");
        return searcher.FindOne() is not null;
    }

    private static string ExtractSamAccountName(string domainUser)
    {
        if (domainUser.Contains('\\'))
            return domainUser.Split('\\', 2)[1];
        if (domainUser.Contains('@'))
            return domainUser.Split('@', 2)[0];
        return domainUser;
    }

    private static string EscapeLdap(string value)
        => value
            .Replace("\\", "\\5c")
            .Replace("*", "\\2a")
            .Replace("(", "\\28")
            .Replace(")", "\\29")
            .Replace("\0", "\\00");
}
