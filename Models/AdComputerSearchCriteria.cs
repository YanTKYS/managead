namespace ManageAdTool.Models;

public class AdComputerSearchCriteria
{
    public string Keyword { get; init; } = string.Empty;
    public string OperatingSystem { get; init; } = string.Empty;
    public bool IncludeDisabled { get; init; }
    public bool? HasDescription { get; init; }
}
