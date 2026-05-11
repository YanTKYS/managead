using System.DirectoryServices;
using ManageAdTool.Models;

namespace ManageAdTool.Services;

public class DirectoryServicesAdReadService : IAdService
{
    private readonly AppPolicy _policy;

    public DirectoryServicesAdReadService(AppPolicy policy)
    {
        _policy = policy;
    }

    public IReadOnlyList<AdUser> SearchUsers(string keyword)
        => SearchUsers(new AdUserSearchCriteria { Keyword = keyword });

    public IReadOnlyList<AdUser> SearchUsers(AdUserSearchCriteria criteria)
    {
        var keyword = criteria.Keyword.Trim();
        if (string.IsNullOrWhiteSpace(keyword) || keyword.Length <= 1) return Array.Empty<AdUser>();

        try
        {
            var list = new List<AdUser>();
            foreach (var baseDn in GetSearchBases())
            {
                using var root = new DirectoryEntry($"LDAP://{baseDn}");
                using var ds = new DirectorySearcher(root)
                {
                    Filter = BuildUserSearchFilter(criteria),
                    PageSize = 200,
                    SizeLimit = _policy.MaxSearchResults
                };
                AddSearchUserProperties(ds);
                foreach (SearchResult r in ds.FindAll())
                {
                    var user = DirectoryServicesUserMapper.MapUser(r);
                    if (IsExcluded(user.SamAccountName)) continue;
                    list.Add(user);
                }
            }
            return list;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("AD検索の実行中にエラーが発生しました。", ex);
        }
    }

    public AdUser? GetUser(string samAccountName)
    {
        try
        {
            foreach (var baseDn in GetSearchBases())
            {
                using var root = new DirectoryEntry($"LDAP://{baseDn}");
                using var ds = new DirectorySearcher(root)
                {
                    Filter = $"(&(objectClass=user)(sAMAccountName={EscapeLdap(samAccountName)}))",
                    PageSize = 1
                };
                AddDetailUserProperties(ds);
                var r = ds.FindOne();
                if (r is null) continue;
                var user = DirectoryServicesUserMapper.MapUser(r);
                if (IsExcluded(user.SamAccountName)) return null;
                return user;
            }
            return null;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("ADユーザー取得の実行中にエラーが発生しました。", ex);
        }
    }

    public IReadOnlyList<string> GetUserGroups(string samAccountName)
    {
        var user = GetUser(samAccountName);
        return user?.Groups ?? Array.Empty<string>();
    }


    public IReadOnlyList<AdGroup> SearchGroups(string keyword)
    {
        var term = keyword.Trim();
        if (string.IsNullOrWhiteSpace(term) || term.Length <= 1) return Array.Empty<AdGroup>();

        try
        {
            var list = new List<AdGroup>();
            foreach (var baseDn in GetGroupSearchBases())
            {
                using var root = new DirectoryEntry($"LDAP://{baseDn}");
                using var ds = new DirectorySearcher(root)
                {
                    Filter = $"(&(objectClass=group)(|(cn=*{EscapeLdap(term)}*)(name=*{EscapeLdap(term)}*)(sAMAccountName=*{EscapeLdap(term)}*)))",
                    PageSize = 200,
                    SizeLimit = _policy.MaxSearchResults
                };
                ds.PropertiesToLoad.AddRange(new[] { "cn", "name", "distinguishedName" });
                foreach (SearchResult r in ds.FindAll())
                {
                    list.Add(new AdGroup
                    {
                        Name = GetProperty(r, "cn", GetProperty(r, "name", string.Empty)),
                        DistinguishedName = GetProperty(r, "distinguishedName", string.Empty)
                    });
                }
            }

            return list.OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("ADグループ検索の実行中にエラーが発生しました。", ex);
        }
    }

    public IReadOnlyList<AdUser> GetGroupMembers(string groupName)
    {
        try
        {
            var groupDn = ResolveGroupDistinguishedName(groupName);
            if (string.IsNullOrWhiteSpace(groupDn)) return Array.Empty<AdUser>();

            var members = new List<AdUser>();
            foreach (var baseDn in GetSearchBases())
            {
                using var root = new DirectoryEntry($"LDAP://{baseDn}");
                using var ds = new DirectorySearcher(root)
                {
                    Filter = $"(&(objectClass=user)(!(objectClass=computer))(memberOf={EscapeLdap(groupDn)}))",
                    PageSize = 200,
                    SizeLimit = _policy.MaxSearchResults
                };
                AddSearchUserProperties(ds);
                foreach (SearchResult r in ds.FindAll())
                {
                    var user = DirectoryServicesUserMapper.MapUser(r);
                    if (IsExcluded(user.SamAccountName)) continue;
                    members.Add(user);
                }
            }

            return members.OrderBy(u => u.SamAccountName, StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("ADグループメンバー取得の実行中にエラーが発生しました。", ex);
        }
    }

    public ChangeSet BuildChangeSet(AdUser current, string newMail, string newDisplayName, string newSurname, string newGivenName)
    {
        var cs = new ChangeSet { TargetSamAccountName = current.SamAccountName, TargetDisplayName = current.DisplayName };
        if (!string.Equals(current.Mail, newMail, StringComparison.Ordinal))
            cs.Changes.Add(new(EditableAttributeDefs.Mail.DisplayName, current.Mail, newMail) { LdapAttribute = EditableAttributeDefs.Mail.LdapAttribute });
        if (!string.Equals(current.DisplayName, newDisplayName, StringComparison.Ordinal))
            cs.Changes.Add(new(EditableAttributeDefs.DisplayName.DisplayName, current.DisplayName, newDisplayName) { LdapAttribute = EditableAttributeDefs.DisplayName.LdapAttribute });
        if (!string.Equals(current.Surname, newSurname, StringComparison.Ordinal))
            cs.Changes.Add(new(EditableAttributeDefs.Surname.DisplayName, current.Surname, newSurname) { LdapAttribute = EditableAttributeDefs.Surname.LdapAttribute });
        if (!string.Equals(current.GivenName, newGivenName, StringComparison.Ordinal))
            cs.Changes.Add(new(EditableAttributeDefs.GivenName.DisplayName, current.GivenName, newGivenName) { LdapAttribute = EditableAttributeDefs.GivenName.LdapAttribute });
        return cs;
    }

    public IReadOnlyList<AdComputer> SearchComputers(AdComputerSearchCriteria criteria)
    {
        var keyword = criteria.Keyword.Trim();
        if (string.IsNullOrWhiteSpace(keyword) || keyword.Length <= 1) return Array.Empty<AdComputer>();

        try
        {
            var list = new List<AdComputer>();
            foreach (var baseDn in GetComputerSearchBases())
            {
                using var root = new DirectoryEntry($"LDAP://{baseDn}");
                using var ds = new DirectorySearcher(root)
                {
                    Filter = BuildComputerSearchFilter(criteria),
                    PageSize = 200,
                    SizeLimit = _policy.MaxSearchResults
                };
                AddSearchComputerProperties(ds);
                foreach (SearchResult r in ds.FindAll())
                {
                    var computer = DirectoryServicesComputerMapper.MapComputer(r);
                    if (IsComputerExcluded(computer.Name)) continue;
                    list.Add(computer);
                }
            }
            return list;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("ADコンピュータ検索の実行中にエラーが発生しました。", ex);
        }
    }

    public IReadOnlyList<AdUser> SearchInactiveUsers(int inactiveDays)
    {
        var cutoffFileTime = DateTimeOffset.UtcNow.AddDays(-inactiveDays).ToFileTime();

        try
        {
            var list = new List<AdUser>();
            foreach (var baseDn in GetSearchBases())
            {
                using var root = new DirectoryEntry($"LDAP://{baseDn}");
                using var ds = new DirectorySearcher(root)
                {
                    Filter = $"(&(objectClass=user)(!(objectClass=computer))(|(!(lastLogonTimestamp=*))(lastLogonTimestamp<={cutoffFileTime})))",
                    PageSize = 200,
                    SizeLimit = _policy.MaxSearchResults
                };
                AddSearchUserProperties(ds);
                foreach (SearchResult r in ds.FindAll())
                {
                    var user = DirectoryServicesUserMapper.MapUser(r);
                    if (IsExcluded(user.SamAccountName)) continue;
                    list.Add(user);
                }
            }

            return list
                .OrderBy(u => u.LastLogonAt ?? DateTimeOffset.MinValue)
                .ThenBy(u => u.SamAccountName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("未ログインユーザー検索の実行中にエラーが発生しました。", ex);
        }
    }

    public IReadOnlyList<AdComputer> SearchInactiveComputers(int inactiveDays)
    {
        var cutoffFileTime = DateTimeOffset.UtcNow.AddDays(-inactiveDays).ToFileTime();

        try
        {
            var list = new List<AdComputer>();
            foreach (var baseDn in GetComputerSearchBases())
            {
                using var root = new DirectoryEntry($"LDAP://{baseDn}");
                using var ds = new DirectorySearcher(root)
                {
                    Filter = $"(&(objectClass=computer)(|(!(lastLogonTimestamp=*))(lastLogonTimestamp<={cutoffFileTime})))",
                    PageSize = 200,
                    SizeLimit = _policy.MaxSearchResults
                };
                AddSearchComputerProperties(ds);
                foreach (SearchResult r in ds.FindAll())
                {
                    var computer = DirectoryServicesComputerMapper.MapComputer(r);
                    if (IsComputerExcluded(computer.Name)) continue;
                    list.Add(computer);
                }
            }

            return list
                .OrderBy(c => c.LastLogonAt ?? DateTimeOffset.MinValue)
                .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("未ログインコンピュータ検索の実行中にエラーが発生しました。", ex);
        }
    }

    public AdComputer? GetComputer(string name)
    {
        try
        {
            foreach (var baseDn in GetComputerSearchBases())
            {
                using var root = new DirectoryEntry($"LDAP://{baseDn}");
                using var ds = new DirectorySearcher(root)
                {
                    Filter = $"(&(objectClass=computer)(name={EscapeLdap(name)}))",
                    PageSize = 1
                };
                AddDetailComputerProperties(ds);
                var r = ds.FindOne();
                if (r is null) continue;
                var computer = DirectoryServicesComputerMapper.MapComputer(r);
                if (IsComputerExcluded(computer.Name)) return null;
                return computer;
            }
            return null;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("ADコンピュータ取得の実行中にエラーが発生しました。", ex);
        }
    }

    public IReadOnlyList<string> GetComputerGroups(string name)
    {
        var computer = GetComputer(name);
        return computer?.Groups ?? Array.Empty<string>();
    }

    public AdGroupDetail? GetGroupDetail(string groupNameOrDn)
    {
        try
        {
            var groupDn = groupNameOrDn.Contains('=', StringComparison.Ordinal) && groupNameOrDn.Contains(',', StringComparison.Ordinal)
                ? groupNameOrDn
                : ResolveGroupDistinguishedName(groupNameOrDn);
            if (string.IsNullOrWhiteSpace(groupDn)) return null;

            string groupName;
            string groupDescription;
            int? primaryGroupToken;
            IReadOnlyList<string> memberOfNames;

            using (var root = new DirectoryEntry($"LDAP://{groupDn}"))
            using (var ds = new DirectorySearcher(root) { Filter = "(objectClass=group)", SearchScope = SearchScope.Base })
            {
                ds.PropertiesToLoad.AddRange(new[] { "cn", "description", "memberOf", "primaryGroupToken" });
                var r = ds.FindOne();
                if (r is null) return null;
                groupName = GetProperty(r, "cn", groupNameOrDn);
                groupDescription = GetProperty(r, "description", string.Empty);
                primaryGroupToken = GetNullableInt(r, "primaryGroupToken");
                memberOfNames = r.Properties.Contains("memberOf")
                    ? r.Properties["memberOf"].Cast<string>()
                        .Select(dn => dn.Split(',')[0].Replace("CN=", string.Empty, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList()
                    : Array.Empty<string>();
            }

            var userMembers = new List<AdUser>();
            var computerNames = new List<string>();
            var groupNames = new List<string>();
            var directMemberDns = ReadGroupMemberDns(groupDn);

            foreach (var memberDn in directMemberDns)
            {
                var member = ReadGroupMember(memberDn);
                if (member.User is not null) userMembers.Add(member.User);
                else if (!string.IsNullOrWhiteSpace(member.ComputerName)) computerNames.Add(member.ComputerName);
                else if (!string.IsNullOrWhiteSpace(member.GroupName)) groupNames.Add(member.GroupName);
            }

            // member 属性が読めない環境向けのフォールバック。
            // 直接メンバーを優先し、0件の場合のみ backlink(memberOf) 検索を試す。
            if (directMemberDns.Count == 0)
            {
                var defaultBase = GetDefaultNamingContext();
                using var root = new DirectoryEntry($"LDAP://{defaultBase}");

                using var uds = new DirectorySearcher(root)
                {
                    Filter = $"(&(objectClass=user)(!(objectClass=computer))(memberOf={EscapeLdap(groupDn)}))",
                    PageSize = 200,
                    SizeLimit = _policy.MaxSearchResults
                };
                AddSearchUserProperties(uds);
                foreach (SearchResult r in uds.FindAll())
                    userMembers.Add(DirectoryServicesUserMapper.MapUser(r));

                using var cds = new DirectorySearcher(root)
                {
                    Filter = $"(&(objectClass=computer)(memberOf={EscapeLdap(groupDn)}))",
                    PageSize = 200
                };
                cds.PropertiesToLoad.AddRange(new[] { "name" });
                foreach (SearchResult r in cds.FindAll())
                    computerNames.Add(GetProperty(r, "name", string.Empty));

                using var gds = new DirectorySearcher(root)
                {
                    Filter = $"(&(objectClass=group)(memberOf={EscapeLdap(groupDn)}))",
                    PageSize = 200
                };
                gds.PropertiesToLoad.AddRange(new[] { "cn" });
                foreach (SearchResult r in gds.FindAll())
                    groupNames.Add(GetProperty(r, "cn", string.Empty));
            }

            if (primaryGroupToken.HasValue)
            {
                var existingSams = new HashSet<string>(userMembers.Select(u => u.SamAccountName), StringComparer.OrdinalIgnoreCase);
                foreach (var primaryMember in SearchUsersByPrimaryGroupId(primaryGroupToken.Value))
                {
                    if (existingSams.Add(primaryMember.SamAccountName)) userMembers.Add(primaryMember);
                }
            }

            return new AdGroupDetail
            {
                Name = groupName,
                DistinguishedName = groupDn,
                Description = groupDescription,
                UserMembers = userMembers.OrderBy(u => u.SamAccountName, StringComparer.OrdinalIgnoreCase).ToList(),
                ComputerMemberNames = computerNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList(),
                GroupMemberNames = groupNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList(),
                MemberOfNames = memberOfNames
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("ADグループ詳細取得の実行中にエラーが発生しました。", ex);
        }
    }

    public AdUser? FindUserForGroupAdd(string samAccountName)
    {
        try
        {
            var defaultBase = GetDefaultNamingContext();
            using var root = new DirectoryEntry($"LDAP://{defaultBase}");
            using var ds = new DirectorySearcher(root)
            {
                Filter = $"(&(objectClass=user)(!(objectClass=computer))(sAMAccountName={EscapeLdap(samAccountName)}))",
                PageSize = 1
            };
            AddSearchUserProperties(ds);
            var r = ds.FindOne();
            return r is null ? null : DirectoryServicesUserMapper.MapUser(r);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("ADユーザー検索の実行中にエラーが発生しました。", ex);
        }
    }

    public ChangeSet BuildComputerChangeSet(AdComputer current, string newDescription)
    {
        var cs = new ChangeSet { TargetSamAccountName = current.Name, TargetDisplayName = current.DnsHostName };
        if (!string.Equals(current.Description, newDescription, StringComparison.Ordinal))
            cs.Changes.Add(new("説明 (description)", current.Description, newDescription) { LdapAttribute = "description" });
        return cs;
    }

    public IReadOnlyList<GpoSimulationResult> SimulateGpo(string? userSam, string? computerName)
    {
        try
        {
            var defaultBase = GetDefaultNamingContext();
            var containerChain = new List<string>();

            if (!string.IsNullOrWhiteSpace(userSam))
            {
                var user = FindUserForGroupAdd(userSam);
                if (user is null || string.IsNullOrWhiteSpace(user.DistinguishedName))
                    throw new InvalidOperationException($"ユーザー「{userSam}」がADに見つかりません。sAMAccountName を確認してください。");
                foreach (var ou in ExtractOuChain(user.DistinguishedName, defaultBase))
                    if (!containerChain.Any(c => string.Equals(c, ou, StringComparison.OrdinalIgnoreCase)))
                        containerChain.Add(ou);
            }

            if (!string.IsNullOrWhiteSpace(computerName))
            {
                var computerDn = FindObjectDnForSimulation(computerName, isComputer: true, defaultBase);
                if (string.IsNullOrWhiteSpace(computerDn))
                    throw new InvalidOperationException($"コンピュータ「{computerName}」がADに見つかりません。コンピュータ名を確認してください。");
                foreach (var ou in ExtractOuChain(computerDn, defaultBase))
                    if (!containerChain.Any(c => string.Equals(c, ou, StringComparison.OrdinalIgnoreCase)))
                        containerChain.Add(ou);
            }

            if (containerChain.Count == 0) return Array.Empty<GpoSimulationResult>();

            var results = new List<GpoSimulationResult>();
            foreach (var container in containerChain)
            {
                foreach (var (gpoDn, linkFlags) in ReadGpoLinks(container))
                {
                    var info = ReadGpoInfo(gpoDn);
                    if (info is null) continue;

                    var linkEnabled = (linkFlags & 1) == 0;
                    var enforced = (linkFlags & 2) == 2;
                    var appliesTo = info.GpoFlags switch
                    {
                        1 => "コンピュータ",
                        2 => "ユーザー",
                        3 => "(全設定無効)",
                        _ => "両方"
                    };
                    var remarksItems = new List<string>();
                    if (!linkEnabled) remarksItems.Add("リンク無効");
                    if (info.GpoFlags == 3) remarksItems.Add("GPO全体無効");

                    results.Add(new GpoSimulationResult
                    {
                        GpoName = info.DisplayName,
                        GpoId = info.Guid,
                        AppliesTo = appliesTo,
                        LinkedOuDn = container,
                        LinkEnabled = linkEnabled,
                        Enforced = enforced,
                        Remarks = string.Join("; ", remarksItems)
                    });
                }
            }
            return results;
        }
        catch (InvalidOperationException) { throw; }
        catch (Exception ex)
        {
            throw new InvalidOperationException("GPOシミュレーションの実行中にエラーが発生しました。", ex);
        }
    }

    private string FindObjectDnForSimulation(string name, bool isComputer, string defaultBase)
    {
        try
        {
            var filter = isComputer
                ? $"(&(objectClass=computer)(name={EscapeLdap(name)}))"
                : $"(&(objectClass=user)(!(objectClass=computer))(sAMAccountName={EscapeLdap(name)}))";
            using var root = new DirectoryEntry($"LDAP://{defaultBase}");
            using var ds = new DirectorySearcher(root) { Filter = filter, PageSize = 1 };
            ds.PropertiesToLoad.Add("distinguishedName");
            var r = ds.FindOne();
            return r is null ? string.Empty : GetProperty(r, "distinguishedName", string.Empty);
        }
        catch { return string.Empty; }
    }

    private static List<string> ExtractOuChain(string objectDn, string domainBase)
    {
        var parts = objectDn.Split(',');
        var dcIdx = Array.FindIndex(parts, p => p.StartsWith("DC=", StringComparison.OrdinalIgnoreCase));
        if (dcIdx < 0) return new List<string>();

        var ous = parts[..dcIdx]
            .Where(p => p.StartsWith("OU=", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var chain = new List<string> { domainBase };
        for (int i = ous.Length - 1; i >= 0; i--)
        {
            var suffix = i < ous.Length - 1
                ? string.Join(",", ous[(i + 1)..]) + "," + domainBase
                : domainBase;
            chain.Add(ous[i] + "," + suffix);
        }
        return chain;
    }

    private static List<(string GpoDn, int Flags)> ReadGpoLinks(string containerDn)
    {
        try
        {
            using var root = new DirectoryEntry($"LDAP://{containerDn}");
            using var ds = new DirectorySearcher(root) { Filter = "(objectClass=*)", SearchScope = SearchScope.Base };
            ds.PropertiesToLoad.Add("gpLink");
            var r = ds.FindOne();
            if (r is null || !r.Properties.Contains("gpLink") || r.Properties["gpLink"].Count == 0)
                return new List<(string, int)>();
            var gpLink = r.Properties["gpLink"][0]?.ToString() ?? string.Empty;
            return ParseGpLink(gpLink);
        }
        catch { return new List<(string, int)>(); }
    }

    private static List<(string GpoDn, int Flags)> ParseGpLink(string gpLink)
    {
        var results = new List<(string, int)>();
        foreach (var entry in gpLink.Split(new[] { '[', ']' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var sep = entry.LastIndexOf(';');
            if (sep < 0) continue;
            var ldapPath = entry[..sep];
            if (!int.TryParse(entry[(sep + 1)..], out var flags)) continue;
            var dn = ldapPath.StartsWith("LDAP://", StringComparison.OrdinalIgnoreCase) ? ldapPath[7..] : ldapPath;
            if (!string.IsNullOrWhiteSpace(dn)) results.Add((dn, flags));
        }
        return results;
    }

    private record GpoInfo(string DisplayName, string Guid, int GpoFlags);

    private static GpoInfo? ReadGpoInfo(string gpoDn)
    {
        try
        {
            using var root = new DirectoryEntry($"LDAP://{gpoDn}");
            using var ds = new DirectorySearcher(root) { Filter = "(objectClass=groupPolicyContainer)", SearchScope = SearchScope.Base };
            ds.PropertiesToLoad.AddRange(new[] { "cn", "displayName", "flags" });
            var r = ds.FindOne();
            if (r is null) return null;
            var cn = GetProperty(r, "cn", string.Empty);
            var displayName = GetProperty(r, "displayName", cn);
            var flagsRaw = r.Properties.Contains("flags") && r.Properties["flags"].Count > 0 ? r.Properties["flags"][0] : null;
            var gpoFlags = flagsRaw is int f ? f : (flagsRaw is not null ? Convert.ToInt32(flagsRaw) : 0);
            return new GpoInfo(displayName, cn, gpoFlags);
        }
        catch { return null; }
    }

    private string BuildUserSearchFilter(AdUserSearchCriteria criteria)
    {
        var keyword = EscapeLdap(criteria.Keyword.Trim());
        var filters = new List<string>
        {
            "(objectClass=user)",
            "(!(objectClass=computer))",
            $"(|(sAMAccountName=*{keyword}*)(displayName=*{keyword}*)(name=*{keyword}*)(mail=*{keyword}*))"
        };

        if (!string.IsNullOrWhiteSpace(criteria.Department)) filters.Add($"(department=*{EscapeLdap(criteria.Department.Trim())}*)");
        if (criteria.HasMail == true) filters.Add("(mail=*)");
        if (criteria.HasMail == false) filters.Add("(!(mail=*))");
        if (!criteria.IncludeDisabled) filters.Add("(!(userAccountControl:1.2.840.113556.1.4.803:=2))");

        return $"(&{string.Concat(filters)})";
    }

    private IReadOnlyList<AdUser> SearchUsersByPrimaryGroupId(int primaryGroupId)
    {
        var list = new List<AdUser>();
        var defaultBase = GetDefaultNamingContext();
        using var root = new DirectoryEntry($"LDAP://{defaultBase}");
        using var ds = new DirectorySearcher(root)
        {
            Filter = $"(&(objectClass=user)(!(objectClass=computer))(primaryGroupID={primaryGroupId}))",
            PageSize = 200,
            SizeLimit = _policy.MaxSearchResults
        };
        AddSearchUserProperties(ds);
        foreach (SearchResult r in ds.FindAll())
        {
            var user = DirectoryServicesUserMapper.MapUser(r);
            if (!IsExcluded(user.SamAccountName)) list.Add(user);
        }
        return list;
    }

    private static int? GetNullableInt(SearchResult r, string prop)
    {
        if (!r.Properties.Contains(prop) || r.Properties[prop].Count == 0) return null;
        var value = r.Properties[prop][0];
        if (value is int i) return i;
        return int.TryParse(value?.ToString(), out var parsed) ? parsed : null;
    }

    private static IReadOnlyList<string> ReadGroupMemberDns(string groupDn)
    {
        var members = new List<string>();
        const int pageSize = 1500;
        var start = 0;

        while (true)
        {
            using var root = new DirectoryEntry($"LDAP://{groupDn}");
            using var ds = new DirectorySearcher(root)
            {
                Filter = "(objectClass=group)",
                SearchScope = SearchScope.Base,
                PageSize = 1
            };

            var requested = $"member;range={start}-{start + pageSize - 1}";
            ds.PropertiesToLoad.Add(requested);
            var result = ds.FindOne();
            if (result is null) break;

            var rangePropertyName = result.Properties.PropertyNames
                .Cast<string>()
                .FirstOrDefault(name => name.StartsWith("member;range=", StringComparison.OrdinalIgnoreCase));

            if (rangePropertyName is null)
            {
                if (result.Properties.Contains("member"))
                {
                    members.AddRange(result.Properties["member"].Cast<object>().Select(x => x?.ToString() ?? string.Empty));
                }
                break;
            }

            members.AddRange(result.Properties[rangePropertyName].Cast<object>().Select(x => x?.ToString() ?? string.Empty));
            if (rangePropertyName.EndsWith("-*", StringComparison.Ordinal)) break;
            start += pageSize;
        }

        return members
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static (AdUser? User, string ComputerName, string GroupName) ReadGroupMember(string memberDn)
    {
        using var root = new DirectoryEntry($"LDAP://{memberDn}");
        using var ds = new DirectorySearcher(root) { Filter = "(objectClass=*)", SearchScope = SearchScope.Base };
        ds.PropertiesToLoad.AddRange(new[]
        {
            "objectClass", "samAccountName", "displayName", "name", "sn", "givenName", "mail", "department",
            "title", "distinguishedName", "lastLogonTimestamp", "accountExpires", "userAccountControl", "cn"
        });

        var r = ds.FindOne();
        if (r is null) return (null, string.Empty, string.Empty);

        var classes = r.Properties.Contains("objectClass")
            ? r.Properties["objectClass"].Cast<object>().Select(x => x.ToString() ?? string.Empty).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (classes.Contains("user") && !classes.Contains("computer")) return (DirectoryServicesUserMapper.MapUser(r), string.Empty, string.Empty);
        if (classes.Contains("computer")) return (null, GetProperty(r, "name", GetProperty(r, "cn", memberDn)), string.Empty);
        if (classes.Contains("group")) return (null, string.Empty, GetProperty(r, "cn", GetProperty(r, "name", memberDn)));

        return (null, string.Empty, string.Empty);
    }

    private string ResolveGroupDistinguishedName(string groupName)
    {
        if (groupName.Contains("=", StringComparison.Ordinal) && groupName.Contains(",", StringComparison.Ordinal)) return groupName;

        foreach (var group in SearchGroups(groupName))
        {
            if (string.Equals(group.Name, groupName, StringComparison.OrdinalIgnoreCase)) return group.DistinguishedName;
        }

        return SearchGroups(groupName).FirstOrDefault()?.DistinguishedName ?? string.Empty;
    }

    private static void AddSearchUserProperties(DirectorySearcher ds)
        => ds.PropertiesToLoad.AddRange(new[] { "samAccountName", "displayName", "name", "sn", "givenName", "mail", "department", "title", "distinguishedName", "lastLogonTimestamp", "accountExpires", "userAccountControl" });

    private static void AddDetailUserProperties(DirectorySearcher ds)
        => ds.PropertiesToLoad.AddRange(new[] { "samAccountName", "displayName", "name", "sn", "givenName", "mail", "department", "title", "distinguishedName", "memberOf", "lastLogonTimestamp", "accountExpires", "userAccountControl" });

    private static string GetProperty(SearchResult r, string name, string fallback)
        => r.Properties.Contains(name) && r.Properties[name].Count > 0 ? r.Properties[name][0]?.ToString() ?? fallback : fallback;

    private string BuildComputerSearchFilter(AdComputerSearchCriteria criteria)
    {
        var keyword = EscapeLdap(criteria.Keyword.Trim());
        var filters = new List<string>
        {
            "(objectClass=computer)",
            $"(|(name=*{keyword}*)(dNSHostName=*{keyword}*)(sAMAccountName=*{keyword}*))"
        };
        if (!string.IsNullOrWhiteSpace(criteria.OperatingSystem))
            filters.Add($"(operatingSystem=*{EscapeLdap(criteria.OperatingSystem.Trim())}*)");
        if (criteria.HasDescription == true) filters.Add("(description=*)");
        if (criteria.HasDescription == false) filters.Add("(!(description=*))");
        if (!criteria.IncludeDisabled) filters.Add("(!(userAccountControl:1.2.840.113556.1.4.803:=2))");
        return $"(&{string.Concat(filters)})";
    }

    private static void AddSearchComputerProperties(DirectorySearcher ds)
        => ds.PropertiesToLoad.AddRange(new[] { "name", "sAMAccountName", "dNSHostName", "operatingSystem", "description", "distinguishedName", "userAccountControl", "lastLogonTimestamp", "whenCreated", "whenChanged" });

    private static void AddDetailComputerProperties(DirectorySearcher ds)
        => ds.PropertiesToLoad.AddRange(new[] { "name", "sAMAccountName", "dNSHostName", "operatingSystem", "description", "distinguishedName", "userAccountControl", "lastLogonTimestamp", "whenCreated", "whenChanged", "memberOf" });

    private IEnumerable<string> GetComputerSearchBases()
        => _policy.EffectiveComputerOuDns.Count > 0 ? _policy.EffectiveComputerOuDns : new[] { GetDefaultNamingContext() };

    private bool IsComputerExcluded(string name)
        => _policy.ExcludedComputerNames.Any(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));

    private IEnumerable<string> GetSearchBases() => _policy.AllowedTargetOuDns.Count > 0 ? _policy.AllowedTargetOuDns : new[] { GetDefaultNamingContext() };

    private IEnumerable<string> GetGroupSearchBases()
        => new[] { GetDefaultNamingContext() };

    private bool IsExcluded(string sam) => _policy.ExcludedSamAccountNames.Any(x => string.Equals(x, sam, StringComparison.OrdinalIgnoreCase));

    private static string GetDefaultNamingContext()
    {
        using var rootDse = new DirectoryEntry("LDAP://RootDSE");
        var value = rootDse.Properties["defaultNamingContext"]?.Value?.ToString();
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("defaultNamingContext を取得できませんでした。");
        return value;
    }

    private static string EscapeLdap(string value) => value.Replace("\\", "\\5c").Replace("*", "\\2a").Replace("(", "\\28").Replace(")", "\\29").Replace("\0", "\\00");
}
