namespace ManageAdTool.Models;

public class AdComputer
{
    public string Name { get; init; } = string.Empty;
    public string DnsHostName { get; init; } = string.Empty;
    public string OperatingSystem { get; init; } = string.Empty;
    public string DistinguishedName { get; init; } = string.Empty;
    public bool Enabled { get; init; }
    public DateTimeOffset? LastBootAt { get; init; }
    public string LastLoggedOnUser { get; init; } = string.Empty;
}
