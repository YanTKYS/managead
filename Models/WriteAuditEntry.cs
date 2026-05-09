namespace ManageAdTool.Models;

public class WriteAuditEntry
{
    public string OperationId { get; init; } = Guid.NewGuid().ToString();
    public string ServiceMode { get; init; } = string.Empty;
    public string Executor { get; init; } = string.Empty;
    public string MachineName { get; init; } = string.Empty;
    public string EditorUser { get; init; } = string.Empty;
    public string TargetSamAccountName { get; init; } = string.Empty;
    public string TargetDisplayName { get; init; } = string.Empty;
    public string TargetDn { get; init; } = string.Empty;
    public IReadOnlyList<FieldChange> Changes { get; init; } = Array.Empty<FieldChange>();
    public bool Success { get; init; }
    public string? Error { get; init; }
    public Dictionary<string, string>? VerifiedAfterUpdate { get; init; }
    public Dictionary<string, string>? RevertCandidate { get; init; }
    public bool AllowedTargetOuMatched { get; init; }
    public bool ExcludedAccountMatched { get; init; }
    public string TargetType { get; init; } = "User";
    public string TargetName { get; init; } = string.Empty;
    public string OperationName { get; init; } = "UpdateUserAttributes";
}
