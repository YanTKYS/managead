using ManageAdTool.Models;

namespace ManageAdTool.Services;

public class UserAttributeCompareUseCase
{
    private readonly IAdService _ad;

    public UserAttributeCompareUseCase(IAdService ad)
    {
        _ad = ad;
    }

    public ChangeSet BuildChangeSet(AdUser current, string mail, string department, string title)
        => _ad.BuildChangeSet(current, mail, department, title);
}
