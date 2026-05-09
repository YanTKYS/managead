namespace ManageAdTool.Models;

public class AdGroupDetail
{
    public string Name { get; init; } = string.Empty;
    public string DistinguishedName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<AdUser> UserMembers { get; init; } = Array.Empty<AdUser>();
    public IReadOnlyList<string> ComputerMemberNames { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> GroupMemberNames { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> MemberOfNames { get; init; } = Array.Empty<string>();
}
