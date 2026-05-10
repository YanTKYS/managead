namespace ManageAdTool.Models;

public class AppPolicy
{
    public List<string> AllowedTargetOuDns { get; set; } = new();
    public List<string> ExcludedSamAccountNames { get; set; } = new();
    public List<string> EditableAttributes { get; set; } = new() { "mail", "displayName", "sn", "givenName" };
    public List<string> UserDetailDisplayAttributes { get; set; } = new() { "SamAccountName", "DisplayName", "Name", "DistinguishedName", "Enabled", "UserAccountControl", "LastLogonTimestamp", "AccountExpires" };
    public string LogPath { get; set; } = @"C:\ProgramData\ManageAdTool\logs\audit.jsonl";
    public string RetiredUsersOuDn { get; set; } = "OU=RetiredUsers,DC=example,DC=local";
    public string ServiceMode { get; set; } = "InMemory";
    public int MaxSearchResults { get; set; } = 200;
    public string EditorAuthMode { get; set; } = "None";
    public string AdminGroupDn { get; set; } = string.Empty;
    public bool AllowNestedAdminGroupMembership { get; set; } = false;
    public int EditSessionMinutes { get; set; } = 15;
    public List<string> AllowedComputerOuDns { get; set; } = new();
    public List<string> ExcludedComputerNames { get; set; } = new();
    public List<string> EditableComputerAttributes { get; set; } = new() { "description" };
    public IReadOnlyList<string> EffectiveComputerOuDns
        => AllowedComputerOuDns.Count > 0 ? AllowedComputerOuDns : AllowedTargetOuDns;
    public List<string> EditableGroupOuDns { get; set; } = new();
    public List<string> ProtectedGroupNames { get; set; } = new();
    public List<string> ProtectedGroupDns { get; set; } = new();
    public bool EnableOperationSupport { get; set; } = true;
    public List<string> OperationChecklistItems { get; set; } = new();
    public int MaxLogDisplayRows { get; set; } = 1000;
}
