namespace ManageAdTool.Models;

public record AuthResult(bool AuthSucceeded, bool IsDomainAdmin, string ResolvedUser, string? ErrorMessage)
{
    public static AuthResult Fail(string message) => new(false, false, string.Empty, message);
    public static AuthResult NotAdmin(string resolvedUser) => new(true, false, resolvedUser, null);
    public static AuthResult DomainAdmin(string resolvedUser) => new(true, true, resolvedUser, null);
}
