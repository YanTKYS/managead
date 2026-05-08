namespace ManageAdTool.Models;

public class AdUserSearchCriteria
{
    public string Keyword { get; init; } = string.Empty;
    public string Department { get; init; } = string.Empty;
    public bool? HasMail { get; init; }
    public bool IncludeDisabled { get; init; }
}
