namespace ManageAdTool.Models;

public record FieldChange(string Field, string Before, string After);

public class ChangeSet
{
    public string TargetSamAccountName { get; init; } = string.Empty;
    public List<FieldChange> Changes { get; init; } = new();
}
