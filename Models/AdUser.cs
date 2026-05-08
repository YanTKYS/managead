namespace ManageAdTool.Models;

public class AdUser
{
    public string SamAccountName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Mail { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public int? UserAccountControl { get; init; }
    public DateTimeOffset? AccountExpiresAt { get; set; }
    public string DistinguishedName { get; set; } = string.Empty;
    public DateTimeOffset? LastLogonAt { get; init; }
    public string LastLogonComputer { get; init; } = string.Empty;
    public IReadOnlyList<string> Groups { get; init; } = Array.Empty<string>();
}
