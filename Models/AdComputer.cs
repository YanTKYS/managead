namespace ManageAdTool.Models;

public class AdComputer
{
    public string Name { get; init; } = string.Empty;
    public string SamAccountName { get; init; } = string.Empty;
    public string DnsHostName { get; init; } = string.Empty;
    public string OperatingSystem { get; init; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DistinguishedName { get; init; } = string.Empty;
    public bool Enabled { get; init; }
    public DateTimeOffset? LastLogonAt { get; init; }
    public DateTimeOffset? WhenCreated { get; init; }
    public DateTimeOffset? WhenChanged { get; init; }
    public IReadOnlyList<string> Groups { get; init; } = Array.Empty<string>();
}
