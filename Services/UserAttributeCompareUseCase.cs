using ManageAdTool.Models;

namespace ManageAdTool.Services;

public class UserAttributeCompareUseCase
{
    private readonly IAdService _ad;

    public UserAttributeCompareUseCase(IAdService ad)
    {
        _ad = ad;
    }

    public ChangeSet BuildChangeSet(AdUser current, string newMail, string newDisplayName, string newSurname, string newGivenName)
        => _ad.BuildChangeSet(current, newMail, newDisplayName, newSurname, newGivenName);
}
