namespace ManageAdTool.Models;

public class AdUser
{
    public string SamAccountName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Mail { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool Enabled { get; init; }
    public string DistinguishedName { get; init; } = string.Empty;
    public IReadOnlyList<string> Groups { get; init; } = Array.Empty<string>();
}
