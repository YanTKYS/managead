namespace ManageAdTool.Models;

public class AppPolicy
{
    public List<string> AllowedTargetOuDns { get; init; } = new();
    public List<string> ExcludedSamAccountNames { get; init; } = new();
    public List<string> EditableAttributes { get; init; } = new() { "mail", "department", "title" };
    public string LogPath { get; init; } = @"C:\ProgramData\ManageAdTool\logs\audit.jsonl";
}
