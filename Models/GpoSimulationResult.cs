namespace ManageAdTool.Models;

public class GpoSimulationResult
{
    public string GpoName { get; set; } = string.Empty;
    public string GpoId { get; set; } = string.Empty;
    public string AppliesTo { get; set; } = string.Empty;
    public string LinkedOuDn { get; set; } = string.Empty;
    public bool LinkEnabled { get; set; }
    public bool Enforced { get; set; }
    public string Remarks { get; set; } = string.Empty;
}
