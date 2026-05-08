namespace ManageAdTool.Models;

public class EditorSession
{
    public string EditorUser { get; init; } = string.Empty;
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public bool IsActive => DateTimeOffset.UtcNow < ExpiresAt;
}
