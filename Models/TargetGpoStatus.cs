namespace ManageAdTool.Models;

public class TargetGpoStatus
{
    public string UserSamAccountName { get; init; } = string.Empty;
    public string ComputerName { get; init; } = string.Empty;
    public string GpoDisplayName { get; init; } = string.Empty;
    public bool Enforced { get; init; }
    public string Scope { get; init; } = string.Empty;
}
