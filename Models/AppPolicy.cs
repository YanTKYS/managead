namespace ManageAdTool.Models;

public class AppPolicy
{
    public List<string> AllowedTargetOuDns { get; set; } = new();
    public List<string> ExcludedSamAccountNames { get; set; } = new();
    public List<string> EditableAttributes { get; set; } = new() { "mail", "department", "title" };
    public string LogPath { get; set; } = @"C:\ProgramData\ManageAdTool\logs\audit.jsonl";
}
