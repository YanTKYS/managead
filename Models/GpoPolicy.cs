namespace ManageAdTool.Models;

public class GpoPolicy
{
    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool UserSettingsEnabled { get; set; } = true;
    public bool ComputerSettingsEnabled { get; set; } = true;
}
