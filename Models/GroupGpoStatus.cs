namespace ManageAdTool.Models;

public class GroupGpoStatus
{
    public string GroupName { get; init; } = string.Empty;
    public string GpoDisplayName { get; init; } = string.Empty;
    public bool Enforced { get; init; }
    public string LinkTarget { get; init; } = string.Empty;
}
