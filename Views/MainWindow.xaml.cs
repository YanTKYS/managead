using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ManageAdTool.Models;
using ManageAdTool.Services;
using Microsoft.Win32;

namespace ManageAdTool.Views;

public partial class MainWindow : Window
{
    private readonly AppPolicy _policy;
    private readonly IAdService _ad;
    private readonly IAdUserAttributeWriteService? _writeService;
    private readonly IAdComputerAttributeWriteService? _computerWriteService;
    private readonly IAdGroupMemberWriteService? _groupWriteService;
    private readonly UserEditPolicyService _policyService = new();
    private readonly UserAttributeCompareUseCase _useCase;
    private readonly ReferenceAuditLogger _auditLogger;
    private readonly AuthAuditLogger _authAuditLogger;
    private readonly WriteAuditLogger _writeAuditLogger;
    private readonly EditorAuthService _authService = new();
    private readonly MainWindowViewModel _vm;
    private readonly DispatcherTimer _sessionTimer;
    private readonly bool _logPathWritable;
    private IReadOnlyList<AdUser> _lastSearchResults = Array.Empty<AdUser>();
    private AdUser? _selected;
    private ChangeSet? _pending;
    private string _revertMemoText = string.Empty;
    private IReadOnlyList<AdComputer> _lastComputerSearchResults = Array.Empty<AdComputer>();
    private AdComputer? _selectedComputer;
    private ChangeSet? _pendingComputer;
    private string _computerRevertMemoText = string.Empty;

    private record StagedUser(string Sam, string DisplayName, string Dn);
    private AdGroup? _selectedGroup;
    private AdGroupDetail? _selectedGroupDetail;
    private readonly List<StagedUser> _groupAddStaging = new();
    private readonly List<StagedUser> _groupRemoveStaging = new();

    // オペレーション支援
    private AdUser? _opSelectedUser;
    private readonly List<string> _opGroupAddPlanned = new();
    private readonly List<string> _opGroupRemovePlanned = new();

    // GPOシミュレーション
    private IReadOnlyList<GpoSimulationResult> _lastGpoResults = Array.Empty<GpoSimulationResult>();

    // 未ログイン確認
    private IReadOnlyList<AdUser> _lastInactiveUsers = Array.Empty<AdUser>();
    private IReadOnlyList<AdComputer> _lastInactiveComputers = Array.Empty<AdComputer>();
    private int _lastInactiveDays = 90;

    private static readonly string Executor =
        $"{Environment.UserDomainName}\\{Environment.UserName}";

    public MainWindow() : this(AppPolicyProvider.Load())
    {
    }

    public MainWindow(AppPolicy policy)
    {
        _policy = policy;
        var isReadOnly = string.Equals(_policy.ServiceMode, "DirectoryReadOnly", StringComparison.OrdinalIgnoreCase);
        _ad = isReadOnly ? new DirectoryServicesAdReadService(_policy) : new InMemoryAdService();
        _writeService = isReadOnly ? new DirectoryServicesAdUserAttributeWriteService() : null;
        _computerWriteService = isReadOnly ? new DirectoryServicesAdComputerAttributeWriteService() : null;
        _groupWriteService = isReadOnly ? new DirectoryServicesAdGroupMemberWriteService() : null;
        _useCase = new UserAttributeCompareUseCase(_ad);
        _auditLogger = new ReferenceAuditLogger(_policy.LogPath, Executor, Environment.MachineName);
        _authAuditLogger = new AuthAuditLogger(_policy.LogPath);
        _writeAuditLogger = new WriteAuditLogger(_policy.LogPath);
        _logPathWritable = TryCheckLogPathWritable(_policy.LogPath);
        _vm = new MainWindowViewModel(_policy);
        _sessionTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _sessionTimer.Tick += SessionTimer_Tick;
        _sessionTimer.Start();
        InitializeComponent();
        DataContext = _vm;
        if (!_policy.EnableOperationSupport)
            OperationSupportTab.Visibility = Visibility.Collapsed;
        ApplyEditability(false, _vm.IsReadOnlyMode ? _vm.ReadOnlyModeLabel : "ユーザー未選択");
        if (!_logPathWritable)
            OutputBox.Text = $"警告: 監査ログディレクトリへの書き込みができません（{Path.GetDirectoryName(_policy.LogPath)}）。\n監査ログは記録されません。参照機能は引き続き使用できます。";
    }

    private static bool TryCheckLogPathWritable(string logPath)
    {
        try
        {
            var dir = Path.GetDirectoryName(logPath);
            if (string.IsNullOrWhiteSpace(dir)) return false;
            Directory.CreateDirectory(dir);
            var testFile = Path.Combine(dir, $".write-test-{Guid.NewGuid():N}");
            File.WriteAllText(testFile, string.Empty);
            File.Delete(testFile);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void SessionTimer_Tick(object? sender, EventArgs e)
    {
        _vm.RefreshSessionStatus();
        if (_selected is not null) EvaluateEditability();
        if (_selectedComputer is not null) EvaluateComputerEditability();
        if (_selectedGroup is not null) EvaluateGroupEditability();
    }

    private void Login_Click(object sender, RoutedEventArgs e)
    {
        var domainUser = EditorUserBox.Text.Trim();
        var password = EditorPasswordBox.Password;
        EditorPasswordBox.Clear();

        if (!_vm.IsAuthSupported)
        {
            OutputBox.Text = "InMemory モードでは認証は使用できません";
            return;
        }

        var result = _authService.TryAuthenticate(domainUser, password, _policy);

        if (!result.AuthSucceeded)
        {
            OutputBox.Text = $"ログイン失敗: ユーザー名またはパスワードを確認してください。";
            _authAuditLogger.Log("LoginFailed", domainUser, success: false, result.ErrorMessage ?? string.Empty);
            return;
        }

        if (!result.IsDomainAdmin)
        {
            OutputBox.Text = $"ログイン失敗: {result.ResolvedUser} は {_policy.AdminGroupDn} のメンバーではありません。";
            _authAuditLogger.Log("LoginDenied", result.ResolvedUser, success: false, "Domain Admins メンバー外");
            return;
        }

        _vm.StartSession(result.ResolvedUser);
        if (_selected is not null) EvaluateEditability();
        OutputBox.Text = $"ログイン成功: {result.ResolvedUser}（編集セッション {_policy.EditSessionMinutes} 分）";
        _authAuditLogger.Log("LoginSuccess", result.ResolvedUser, success: true);
        EditorUserBox.Text = string.Empty;
    }

    private void Logout_Click(object sender, RoutedEventArgs e)
    {
        var user = _vm.CurrentEditorUser;
        _vm.EndSession();
        _pending = null;
        if (_selected is not null) EvaluateEditability();
        OutputBox.Text = "ログアウトしました";
        if (!string.IsNullOrEmpty(user))
            _authAuditLogger.Log("Logout", user, success: true);
    }

    private void Search_Click(object sender, RoutedEventArgs e)
    {
        var criteria = BuildUserSearchCriteria();
        if (string.IsNullOrWhiteSpace(criteria.Keyword) || criteria.Keyword.Length <= 1)
        {
            OutputBox.Text = "検索語は2文字以上入力してください";
            ClearSearchResults();
            return;
        }

        try
        {
            var results = _ad.SearchUsers(criteria);
            _selected = null;
            _pending = null;
            _vm.SetPendingReady(false);
            _lastSearchResults = results;
            SearchResultGrid.ItemsSource = results;
            UserDetailBox.Text = string.Empty;
            GroupListBox.Text = string.Empty;
            var exceeded = results.Count >= _policy.MaxSearchResults;
            OutputBox.Text = exceeded
                ? $"検索結果: {results.Count}件（上限 {_policy.MaxSearchResults} 件に達しました。検索条件を絞り込んでください）"
                : $"検索結果: {results.Count}件";
            _auditLogger.Log("UserSearch", FormatCriteria(criteria), results.Count, success: true);
        }
        catch (Exception ex)
        {
            ClearSearchResults();
            OutputBox.Text = $"検索に失敗しました。ネットワーク接続またはAD設定を確認してください。";
            _auditLogger.Log("UserSearch", FormatCriteria(criteria), 0, success: false, FormatErrorForLog(ex));
        }
    }

    private void SearchResultGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        _selected = SearchResultGrid.SelectedItem as AdUser;
        _pending = null;
        _vm.SetPendingReady(false);
        _revertMemoText = string.Empty;
        CopyRevertMemoButton.IsEnabled = false;
        if (_selected is null) return;

        EditMailBox.Text = _selected.Mail;
        EditDisplayNameBox.Text = _selected.DisplayName;
        EditSurnameBox.Text = _selected.Surname;
        EditGivenNameBox.Text = _selected.GivenName;
        SamAccountNameReadBox.Text = _selected.SamAccountName;
        UserDetailBox.Text = FormatUserDetails(_selected);
        try
        {
            var groups = _ad.GetUserGroups(_selected.SamAccountName)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
            GroupListBox.Text = string.Join(Environment.NewLine, groups);
            OutputBox.Text = $"詳細表示: {_selected.SamAccountName} / 所属グループ {groups.Count}件";
            _auditLogger.Log("UserDetail", _selected.SamAccountName, 1, success: true);
            _auditLogger.Log("UserGroups", _selected.SamAccountName, groups.Count, success: true);
        }
        catch (Exception ex)
        {
            OutputBox.Text = $"詳細取得に失敗しました。ネットワーク接続を確認してください。";
            GroupListBox.Text = string.Empty;
            _auditLogger.Log("UserDetail", _selected.SamAccountName, 0, success: false, FormatErrorForLog(ex));
        }

        EvaluateEditability();
    }

    private void EditInput_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_pending is not null)
        {
            _pending = null;
            _vm.SetPendingReady(false);
        }
    }

    private void GroupSearch_Click(object sender, RoutedEventArgs e)
    {
        var keyword = GroupSearchBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(keyword) || keyword.Length <= 1)
        {
            OutputBox.Text = "グループ検索語は2文字以上入力してください";
            ClearGroupResults();
            return;
        }

        try
        {
            var groups = _ad.SearchGroups(keyword);
            _selectedGroup = null;
            _selectedGroupDetail = null;
            ClearGroupDetail();
            ClearGroupStaging();
            GroupSearchResultGrid.ItemsSource = groups;
            var groupExceeded = groups.Count >= _policy.MaxSearchResults;
            OutputBox.Text = groupExceeded
                ? $"グループ検索結果: {groups.Count}件（上限 {_policy.MaxSearchResults} 件に達しました。検索条件を絞り込んでください）"
                : $"グループ検索結果: {groups.Count}件";
            _auditLogger.Log("GroupSearch", keyword, groups.Count, success: true);
        }
        catch (Exception ex)
        {
            ClearGroupResults();
            OutputBox.Text = "グループ検索に失敗しました。ネットワーク接続またはAD設定を確認してください。";
            _auditLogger.Log("GroupSearch", keyword, 0, success: false, FormatErrorForLog(ex));
        }
    }

    private void GroupSearchResultGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        _selectedGroup = GroupSearchResultGrid.SelectedItem as AdGroup;
        _selectedGroupDetail = null;
        ClearGroupDetail();
        ClearGroupStaging();
        if (_selectedGroup is null) return;

        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            var detail = _ad.GetGroupDetail(_selectedGroup.DistinguishedName);
            _selectedGroupDetail = detail;

            if (detail is null)
            {
                OutputBox.Text = $"グループ詳細を取得できませんでした: {_selectedGroup.Name}";
                return;
            }

            GroupUserMemberGrid.ItemsSource = detail.UserMembers;
            GroupDetailBox.Text = FormatGroupDetail(detail);
            GroupOtherMembersBox.Text = FormatGroupOtherMembers(detail);

            var total = detail.UserMembers.Count + detail.ComputerMemberNames.Count + detail.GroupMemberNames.Count;
            OutputBox.Text = $"グループ詳細表示: {detail.Name} / ユーザー{detail.UserMembers.Count}名 / コンピュータ{detail.ComputerMemberNames.Count}台 / ネストグループ{detail.GroupMemberNames.Count}件 / 合計{total}件";
            _auditLogger.Log("GroupDetail", _selectedGroup.Name, total, success: true);
        }
        catch (Exception ex)
        {
            OutputBox.Text = "グループ詳細の取得に失敗しました。ネットワーク接続を確認してください。";
            _auditLogger.Log("GroupDetail", _selectedGroup.Name, 0, success: false, FormatErrorForLog(ex));
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }

        EvaluateGroupEditability();
    }

    // ── Group member staging ────────────────────────────────────────────────

    private void GroupAddUserToStaging_Click(object sender, RoutedEventArgs e)
    {
        var sam = GroupAddUserBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(sam))
        {
            OutputBox.Text = "SamAccountName を入力してください";
            return;
        }

        if (_groupAddStaging.Any(u => string.Equals(u.Sam, sam, StringComparison.OrdinalIgnoreCase)))
        {
            OutputBox.Text = $"「{sam}」は既に追加予定リストにあります";
            return;
        }

        if (_groupRemoveStaging.Any(u => string.Equals(u.Sam, sam, StringComparison.OrdinalIgnoreCase)))
        {
            OutputBox.Text = $"「{sam}」は削除予定リストにあります。矛盾する操作は追加できません";
            return;
        }

        if (_selectedGroupDetail is not null &&
            _selectedGroupDetail.UserMembers.Any(u => string.Equals(u.SamAccountName, sam, StringComparison.OrdinalIgnoreCase)))
        {
            OutputBox.Text = $"「{sam}」は既にグループメンバーです";
            return;
        }

        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            var user = _ad.FindUserForGroupAdd(sam);
            if (user is null)
            {
                OutputBox.Text = $"ユーザー「{sam}」がADに見つかりません。SamAccountName を確認してください。";
                return;
            }
            _groupAddStaging.Add(new StagedUser(user.SamAccountName, user.DisplayName, user.DistinguishedName));
            RefreshGroupStagingLists();
            ResetGroupPending();
            GroupAddUserBox.Text = string.Empty;
            OutputBox.Text = $"追加予定に追加: {user.SamAccountName}（{user.DisplayName}）";
        }
        catch (Exception ex)
        {
            OutputBox.Text = "ユーザー検索に失敗しました。ネットワーク接続を確認してください。";
            _ = FormatErrorForLog(ex);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private void GroupAddToRemoveStaging_Click(object sender, RoutedEventArgs e)
    {
        if (GroupUserMemberGrid.SelectedItem is not AdUser user) return;

        if (_groupRemoveStaging.Any(u => string.Equals(u.Sam, user.SamAccountName, StringComparison.OrdinalIgnoreCase)))
        {
            OutputBox.Text = $"「{user.SamAccountName}」は既に削除予定リストにあります";
            return;
        }

        if (_groupAddStaging.Any(u => string.Equals(u.Sam, user.SamAccountName, StringComparison.OrdinalIgnoreCase)))
        {
            OutputBox.Text = $"「{user.SamAccountName}」は追加予定リストにあります。矛盾する操作は追加できません";
            return;
        }

        if (string.IsNullOrWhiteSpace(user.DistinguishedName))
        {
            OutputBox.Text = $"「{user.SamAccountName}」の DN が取得できていません。グループを再選択してください";
            return;
        }

        _groupRemoveStaging.Add(new StagedUser(user.SamAccountName, user.DisplayName, user.DistinguishedName));
        RefreshGroupStagingLists();
        ResetGroupPending();
        OutputBox.Text = $"削除予定に追加: {user.SamAccountName}（{user.DisplayName}）";
    }

    private void GroupRemoveFromAddStaging_Click(object sender, RoutedEventArgs e)
    {
        var idx = GroupAddStagingList.SelectedIndex;
        if (idx < 0 || idx >= _groupAddStaging.Count) return;
        var removed = _groupAddStaging[idx];
        _groupAddStaging.RemoveAt(idx);
        RefreshGroupStagingLists();
        ResetGroupPending();
        OutputBox.Text = $"追加予定から除去: {removed.Sam}";
    }

    private void GroupRemoveFromRemoveStaging_Click(object sender, RoutedEventArgs e)
    {
        var idx = GroupRemoveStagingList.SelectedIndex;
        if (idx < 0 || idx >= _groupRemoveStaging.Count) return;
        var removed = _groupRemoveStaging[idx];
        _groupRemoveStaging.RemoveAt(idx);
        RefreshGroupStagingLists();
        ResetGroupPending();
        OutputBox.Text = $"削除予定から除去: {removed.Sam}";
    }

    private void GroupPreview_Click(object sender, RoutedEventArgs e)
    {
        if (!_vm.GroupCanEdit || _selectedGroup is null) return;

        if (_groupAddStaging.Count == 0 && _groupRemoveStaging.Count == 0)
        {
            OutputBox.Text = "差分なし（追加予定・削除予定がありません）";
            _vm.SetGroupPendingReady(false);
            return;
        }

        _vm.SetGroupPendingReady(true);

        var lines = new List<string> { $"グループメンバー差分確認: {_selectedGroup.Name}" };
        if (_groupAddStaging.Count > 0)
        {
            lines.Add($"追加 ({_groupAddStaging.Count}名):");
            lines.AddRange(_groupAddStaging.Select(u => $"  + {u.Sam}（{u.DisplayName}）"));
        }
        if (_groupRemoveStaging.Count > 0)
        {
            lines.Add($"削除 ({_groupRemoveStaging.Count}名):");
            lines.AddRange(_groupRemoveStaging.Select(u => $"  - {u.Sam}（{u.DisplayName}）"));
        }
        lines.Add("「限定更新実行」ボタンで AD を更新します（再認証・確認ダイアログあり）");
        OutputBox.Text = string.Join(Environment.NewLine, lines);
    }

    private void GroupExecute_Click(object sender, RoutedEventArgs e)
    {
        if (_groupWriteService is null)
        {
            OutputBox.Text = "ADグループ更新はこのモードでは使用できません（DirectoryReadOnly が必要です）";
            return;
        }

        if (!_vm.IsEditSessionActive)
        {
            OutputBox.Text = "編集セッションが期限切れです。再ログインしてください。";
            return;
        }

        if (_policy.EditableGroupOuDns.Count == 0)
        {
            OutputBox.Text = "EditableGroupOuDns が未設定のため更新できません。appsettings.json を確認してください。";
            return;
        }

        if (_selectedGroup is null) return;

        if (_groupAddStaging.Count == 0 && _groupRemoveStaging.Count == 0)
        {
            OutputBox.Text = "変更予定がありません。追加または削除するユーザーを指定してください。";
            return;
        }

        if (!_logPathWritable)
        {
            var proceed = MessageBox.Show(
                $"監査ログディレクトリへの書き込みができません。\n({Path.GetDirectoryName(_policy.LogPath)})\n\n更新を実行すると監査ログ（write-audit.jsonl）が記録されません。\n続行しますか？",
                "監査ログ警告", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (proceed != MessageBoxResult.Yes) return;
        }

        var reAuthDlg = new ReAuthDialog(_vm.CurrentEditorUser) { Owner = this };
        if (reAuthDlg.ShowDialog() != true) return;

        var reAuthUser = reAuthDlg.DomainUser!;
        var reAuthPassword = reAuthDlg.Password!;

        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            var authResult = _authService.TryAuthenticate(reAuthUser, reAuthPassword, _policy);

            if (!authResult.AuthSucceeded)
            {
                OutputBox.Text = "再認証に失敗しました。ユーザー名またはパスワードを確認してください。";
                _authAuditLogger.Log("ReAuthFailed", reAuthUser, success: false, authResult.ErrorMessage ?? string.Empty);
                return;
            }

            if (!authResult.IsDomainAdmin)
            {
                OutputBox.Text = $"再認証したユーザー（{authResult.ResolvedUser}）は Domain Admins のメンバーではありません。";
                _authAuditLogger.Log("ReAuthDenied", authResult.ResolvedUser, success: false, "Domain Admins 非メンバー");
                return;
            }

            if (!string.Equals(authResult.ResolvedUser, _vm.CurrentEditorUser, StringComparison.OrdinalIgnoreCase))
            {
                OutputBox.Text = "再認証ユーザーと編集セッションユーザーが一致しません。ログアウトして再度ログインしてください。";
                _authAuditLogger.Log("ReAuthUserMismatch", authResult.ResolvedUser, success: false,
                    $"session={_vm.CurrentEditorUser} reauth={authResult.ResolvedUser}");
                return;
            }

            Mouse.OverrideCursor = null;

            var ouMatched = _policy.EditableGroupOuDns.Any(ou => IsUnderOu(_selectedGroup.DistinguishedName, ou));
            if (!ouMatched)
            {
                OutputBox.Text = "対象グループが EditableGroupOuDns の許可OU外のため更新できません。";
                LogGroupWriteFailure(_selectedGroup.Name, _selectedGroup.DistinguishedName, authResult.ResolvedUser,
                    "許可OU外", false);
                return;
            }

            var isProtectedByDn = _policy.ProtectedGroupDns.Any(dn => string.Equals(dn, _selectedGroup.DistinguishedName, StringComparison.OrdinalIgnoreCase));
            var isProtectedByName = _policy.ProtectedGroupNames.Any(n => string.Equals(n, _selectedGroup.Name, StringComparison.OrdinalIgnoreCase));
            if (isProtectedByDn || isProtectedByName)
            {
                OutputBox.Text = "対象グループは ProtectedGroupDns / ProtectedGroupNames により保護されているため更新できません。";
                LogGroupWriteFailure(_selectedGroup.Name, _selectedGroup.DistinguishedName, authResult.ResolvedUser,
                    "保護グループ", false);
                return;
            }

            var addDisplayTexts = _groupAddStaging.Select(u => $"追加: {u.Sam}（{u.DisplayName}）").ToList();
            var removeDisplayTexts = _groupRemoveStaging.Select(u => $"削除: {u.Sam}（{u.DisplayName}）").ToList();

            var confirmDlg = new ConfirmGroupMemberUpdateDialog(
                _selectedGroup.Name, _selectedGroup.DistinguishedName,
                authResult.ResolvedUser, Executor, Environment.MachineName,
                addDisplayTexts, removeDisplayTexts) { Owner = this };
            if (confirmDlg.ShowDialog() != true) return;

            Mouse.OverrideCursor = Cursors.Wait;

            AdGroupDetail? currentDetail;
            try
            {
                currentDetail = _ad.GetGroupDetail(_selectedGroup.DistinguishedName);
            }
            catch (Exception ex)
            {
                OutputBox.Text = "更新前のグループ情報取得に失敗しました。ネットワーク接続を確認してください。";
                LogGroupWriteFailure(_selectedGroup.Name, _selectedGroup.DistinguishedName, authResult.ResolvedUser,
                    FormatErrorForLog(ex), false);
                return;
            }

            if (currentDetail is null)
            {
                OutputBox.Text = "更新対象グループがADに見つかりません。更新を中止しました。";
                LogGroupWriteFailure(_selectedGroup.Name, _selectedGroup.DistinguishedName, authResult.ResolvedUser,
                    "対象グループがADに存在しない", false);
                return;
            }

            var currentMemberSams = new HashSet<string>(currentDetail.UserMembers.Select(u => u.SamAccountName), StringComparer.OrdinalIgnoreCase);

            foreach (var staged in _groupAddStaging)
            {
                if (currentMemberSams.Contains(staged.Sam))
                {
                    OutputBox.Text = $"「{staged.Sam}」は既にグループメンバーです（AD現在値）。再度「差分確認」から実行してください。";
                    LogGroupWriteFailure(_selectedGroup.Name, _selectedGroup.DistinguishedName, authResult.ResolvedUser,
                        $"整合性エラー: {staged.Sam} は既にメンバー", true);
                    return;
                }
            }

            foreach (var staged in _groupRemoveStaging)
            {
                if (!currentMemberSams.Contains(staged.Sam))
                {
                    OutputBox.Text = $"「{staged.Sam}」はグループメンバーではありません（AD現在値）。再度「差分確認」から実行してください。";
                    LogGroupWriteFailure(_selectedGroup.Name, _selectedGroup.DistinguishedName, authResult.ResolvedUser,
                        $"整合性エラー: {staged.Sam} はメンバーでない", true);
                    return;
                }
            }

            var addDns = _groupAddStaging.Select(u => u.Dn).ToList();
            var removeDns = _groupRemoveStaging.Select(u => u.Dn).ToList();

            UpdateResult writeResult;
            try
            {
                writeResult = _groupWriteService.UpdateGroupMembers(
                    _selectedGroup.DistinguishedName, addDns, removeDns, reAuthUser, reAuthPassword);
            }
            catch (Exception ex)
            {
                OutputBox.Text = "更新処理中にエラーが発生しました。監査ログを確認してください。";
                LogGroupWriteFailure(_selectedGroup.Name, _selectedGroup.DistinguishedName, authResult.ResolvedUser,
                    FormatErrorForLog(ex), true);
                return;
            }

            if (!writeResult.Success)
            {
                OutputBox.Text = $"更新に失敗しました。\n{writeResult.ErrorMessage}\n詳細は監査ログを確認してください。";
                LogGroupWriteFailure(_selectedGroup.Name, _selectedGroup.DistinguishedName, authResult.ResolvedUser,
                    writeResult.ErrorMessage ?? "不明なエラー", true);
                return;
            }

            var changes = new List<FieldChange>();
            changes.AddRange(_groupAddStaging.Select(u => new FieldChange($"追加: {u.Sam}（{u.DisplayName}）", string.Empty, u.Dn) { LdapAttribute = "member" }));
            changes.AddRange(_groupRemoveStaging.Select(u => new FieldChange($"削除: {u.Sam}（{u.DisplayName}）", u.Dn, string.Empty) { LdapAttribute = "member" }));

            var auditEntry = new WriteAuditEntry
            {
                ServiceMode = _policy.ServiceMode,
                Executor = Executor,
                MachineName = Environment.MachineName,
                EditorUser = authResult.ResolvedUser,
                TargetType = "Group",
                TargetName = _selectedGroup.Name,
                OperationName = "UpdateGroupMembers",
                TargetSamAccountName = _selectedGroup.Name,
                TargetDisplayName = _selectedGroup.Name,
                TargetDn = _selectedGroup.DistinguishedName,
                Changes = changes,
                Success = true,
                AllowedTargetOuMatched = true,
                ExcludedAccountMatched = false
            };
            var auditSaved = _writeAuditLogger.Log(auditEntry);
            _authAuditLogger.Log("WriteExecuted", authResult.ResolvedUser, success: true,
                $"target={_selectedGroup.Name} targetType=Group adds={addDns.Count} removes={removeDns.Count}");

            if (!auditSaved)
                _authAuditLogger.Log("WriteAuditSaveFailed", authResult.ResolvedUser, success: false,
                    $"target={_selectedGroup.Name} write-audit.jsonl への保存失敗");

            ClearGroupStaging();
            _vm.SetGroupPendingReady(false);

            AdGroupDetail? verifiedDetail = null;
            try { verifiedDetail = _ad.GetGroupDetail(_selectedGroup.DistinguishedName); } catch { }

            if (verifiedDetail is not null)
            {
                _selectedGroupDetail = verifiedDetail;
                GroupUserMemberGrid.ItemsSource = verifiedDetail.UserMembers;
                GroupDetailBox.Text = FormatGroupDetail(verifiedDetail);
                GroupOtherMembersBox.Text = FormatGroupOtherMembers(verifiedDetail);
            }

            OutputBox.Text = BuildGroupSuccessOutput(_selectedGroup.Name, _selectedGroup.DistinguishedName,
                changes, verifiedDetail, auditSaved);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private void EvaluateGroupEditability()
    {
        if (_selectedGroup is null) { ApplyGroupEditability(false, "グループ未選択"); return; }
        if (!_vm.IsReadOnlyMode) { ApplyGroupEditability(false, "DirectoryReadOnly モードが必要です"); return; }
        if (!_vm.IsEditSessionActive) { ApplyGroupEditability(false, "編集セッション未開始（ログインしてください）"); return; }
        if (_policy.EditableGroupOuDns.Count == 0) { ApplyGroupEditability(false, "EditableGroupOuDns 未設定のため更新不可"); return; }
        if (!_policy.EditableGroupOuDns.Any(ou => IsUnderOu(_selectedGroup.DistinguishedName, ou))) { ApplyGroupEditability(false, "対象グループが許可OU外"); return; }
        if (_policy.ProtectedGroupDns.Any(dn => string.Equals(dn, _selectedGroup.DistinguishedName, StringComparison.OrdinalIgnoreCase))) { ApplyGroupEditability(false, "保護グループのため編集不可"); return; }
        if (_policy.ProtectedGroupNames.Any(n => string.Equals(n, _selectedGroup.Name, StringComparison.OrdinalIgnoreCase))) { ApplyGroupEditability(false, "保護グループ名のため編集不可"); return; }
        ApplyGroupEditability(true, string.Empty);
    }

    private void ApplyGroupEditability(bool canEdit, string reason)
    {
        _vm.GroupCanEdit = canEdit;
        _vm.GroupEditBlockedReason = reason;
    }

    private void ResetGroupPending()
    {
        _vm.SetGroupPendingReady(false);
    }

    private void RefreshGroupStagingLists()
    {
        GroupAddStagingList.ItemsSource = null;
        GroupAddStagingList.ItemsSource = _groupAddStaging.Select(u => $"{u.Sam}（{u.DisplayName}）").ToList();
        GroupRemoveStagingList.ItemsSource = null;
        GroupRemoveStagingList.ItemsSource = _groupRemoveStaging.Select(u => $"{u.Sam}（{u.DisplayName}）").ToList();
    }

    private void ClearGroupResults()
    {
        GroupSearchResultGrid.ItemsSource = Array.Empty<AdGroup>();
        ClearGroupDetail();
        ClearGroupStaging();
    }

    private void ClearGroupDetail()
    {
        GroupUserMemberGrid.ItemsSource = Array.Empty<AdUser>();
        GroupDetailBox.Text = string.Empty;
        GroupOtherMembersBox.Text = string.Empty;
        _vm.GroupCanEdit = false;
        _vm.GroupEditBlockedReason = "グループ未選択";
    }

    private void ClearGroupStaging()
    {
        _groupAddStaging.Clear();
        _groupRemoveStaging.Clear();
        RefreshGroupStagingLists();
        _vm.SetGroupPendingReady(false);
    }

    private static string FormatGroupDetail(AdGroupDetail detail)
    {
        var lines = new List<string>
        {
            $"Name: {detail.Name}",
            $"DistinguishedName: {detail.DistinguishedName}",
        };
        if (!string.IsNullOrWhiteSpace(detail.Description))
            lines.Add($"Description: {detail.Description}");
        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatGroupOtherMembers(AdGroupDetail detail)
    {
        var lines = new List<string>();
        if (detail.ComputerMemberNames.Count > 0)
            lines.Add($"コンピュータメンバー ({detail.ComputerMemberNames.Count}台): {string.Join(", ", detail.ComputerMemberNames)}");
        if (detail.GroupMemberNames.Count > 0)
            lines.Add($"ネストグループ ({detail.GroupMemberNames.Count}件): {string.Join(", ", detail.GroupMemberNames)}");
        if (detail.MemberOfNames.Count > 0)
            lines.Add($"所属グループ/memberOf ({detail.MemberOfNames.Count}件): {string.Join(", ", detail.MemberOfNames)}");
        return lines.Count == 0 ? "(コンピュータ・ネストグループ・memberOf なし)" : string.Join(Environment.NewLine, lines);
    }

    private static string BuildGroupSuccessOutput(string groupName, string groupDn, IReadOnlyList<FieldChange> changes,
        AdGroupDetail? verifiedDetail, bool auditSaved)
    {
        var lines = new List<string>
        {
            $"更新成功: {groupName}",
            string.Empty,
            $"対象DN: {groupDn}",
            string.Empty,
            "変更内容:"
        };

        foreach (var change in changes)
            lines.Add($"  - {change.Field}");

        if (verifiedDetail is not null)
        {
            lines.Add(string.Empty);
            lines.Add($"AD再取得: ユーザーメンバー {verifiedDetail.UserMembers.Count}名 / コンピュータ {verifiedDetail.ComputerMemberNames.Count}台 / ネストグループ {verifiedDetail.GroupMemberNames.Count}件");
        }
        else
        {
            lines.Add(string.Empty);
            lines.Add("※ 更新後のAD再取得に失敗しました。ADUCで手動確認してください。");
        }

        lines.Add(string.Empty);
        lines.Add(auditSaved
            ? "監査ログ: write-audit.jsonl に保存済み（operationName: UpdateGroupMembers, targetType: Group）"
            : "監査ログ: ⚠ write-audit.jsonl への保存に失敗しました。管理者に連絡してください。");

        return string.Join(Environment.NewLine, lines);
    }

    private void LogGroupWriteFailure(string groupName, string groupDn, string editorUser, string error, bool ouMatched)
    {
        _writeAuditLogger.Log(new WriteAuditEntry
        {
            ServiceMode = _policy.ServiceMode,
            Executor = Executor,
            MachineName = Environment.MachineName,
            EditorUser = editorUser,
            TargetType = "Group",
            TargetName = groupName,
            OperationName = "UpdateGroupMembers",
            TargetSamAccountName = groupName,
            TargetDisplayName = groupName,
            TargetDn = groupDn,
            Changes = Array.Empty<FieldChange>(),
            Success = false,
            Error = error,
            AllowedTargetOuMatched = ouMatched,
            ExcludedAccountMatched = false
        });
    }

    private void EvaluateEditability()
    {
        var result = _policyService.Evaluate(_selected, _policy, _vm.IsEditSessionActive);
        ApplyEditability(result.canEdit, result.reason);
    }

    private void ApplyEditability(bool canEdit, string reason)
    {
        _vm.CanEdit = canEdit;
        _vm.EditBlockedReason = reason;
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        if (!_vm.CanEdit || _selected is null) return;
        _pending = _useCase.BuildChangeSet(_selected,
            EditMailBox.Text.Trim(), EditDisplayNameBox.Text.Trim(),
            EditSurnameBox.Text.Trim(), EditGivenNameBox.Text.Trim());
        _vm.SetPendingReady(_pending.Changes.Count > 0);
        OutputBox.Text = FormatChangePreview(_pending, "差分確認");
    }

    private void CopyRevertMemo_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_revertMemoText))
            Clipboard.SetText(_revertMemoText);
    }

    private void Execute_Click(object sender, RoutedEventArgs e)
    {
        if (_writeService is null)
        {
            OutputBox.Text = "AD更新はこのモードでは使用できません（DirectoryReadOnly が必要です）";
            return;
        }

        if (!_vm.IsEditSessionActive)
        {
            OutputBox.Text = "編集セッションが期限切れです。再ログインしてください。";
            return;
        }

        if (_policy.AllowedTargetOuDns.Count == 0)
        {
            OutputBox.Text = "AllowedTargetOuDns が未設定のため更新できません。appsettings.json を確認してください。";
            return;
        }

        if (_selected is null) return;

        // 監査ログディレクトリへの書き込み不可の場合は事前に警告する
        if (!_logPathWritable)
        {
            var proceed = MessageBox.Show(
                $"監査ログディレクトリへの書き込みができません。\n({Path.GetDirectoryName(_policy.LogPath)})\n\n更新を実行すると監査ログ（write-audit.jsonl）が記録されません。\n続行しますか？",
                "監査ログ警告", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (proceed != MessageBoxResult.Yes) return;
        }

        var changeSet = _useCase.BuildChangeSet(_selected,
            EditMailBox.Text.Trim(), EditDisplayNameBox.Text.Trim(),
            EditSurnameBox.Text.Trim(), EditGivenNameBox.Text.Trim());

        var emptyUpdates = changeSet.Changes.Where(c => string.IsNullOrWhiteSpace(c.After)).ToList();
        if (emptyUpdates.Count > 0)
        {
            OutputBox.Text = $"空文字への更新は禁止されています: {string.Join(", ", emptyUpdates.Select(c => c.Field))}（属性クリアは将来機能です）";
            return;
        }

        if (changeSet.Changes.Count == 0)
        {
            OutputBox.Text = "差分なし（更新不要）";
            _vm.SetPendingReady(false);
            return;
        }

        var reAuthDlg = new ReAuthDialog(_vm.CurrentEditorUser) { Owner = this };
        if (reAuthDlg.ShowDialog() != true) return;

        var reAuthUser = reAuthDlg.DomainUser!;
        // パスワードは認証と書き込みに即時使用し、finally 終了後に参照を切る。永続化しない。
        var reAuthPassword = reAuthDlg.Password!;

        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            var authResult = _authService.TryAuthenticate(reAuthUser, reAuthPassword, _policy);

            if (!authResult.AuthSucceeded)
            {
                OutputBox.Text = "再認証に失敗しました。ユーザー名またはパスワードを確認してください。";
                _authAuditLogger.Log("ReAuthFailed", reAuthUser, success: false, authResult.ErrorMessage ?? string.Empty);
                return;
            }

            if (!authResult.IsDomainAdmin)
            {
                OutputBox.Text = $"再認証したユーザー（{authResult.ResolvedUser}）は Domain Admins のメンバーではありません。";
                _authAuditLogger.Log("ReAuthDenied", authResult.ResolvedUser, success: false, "Domain Admins 非メンバー");
                return;
            }

            if (!string.Equals(authResult.ResolvedUser, _vm.CurrentEditorUser, StringComparison.OrdinalIgnoreCase))
            {
                OutputBox.Text = "再認証ユーザーと編集セッションユーザーが一致しません。ログアウトして再度ログインしてください。";
                _authAuditLogger.Log("ReAuthUserMismatch", authResult.ResolvedUser, success: false,
                    $"session={_vm.CurrentEditorUser} reauth={authResult.ResolvedUser}");
                return;
            }

            Mouse.OverrideCursor = null;

            var confirmDlg = new ConfirmUpdateDialog(
                changeSet, _selected.DistinguishedName, _selected.DisplayName,
                authResult.ResolvedUser, Executor, Environment.MachineName) { Owner = this };
            if (confirmDlg.ShowDialog() != true) return;

            Mouse.OverrideCursor = Cursors.Wait;

            AdUser? currentUser;
            try
            {
                currentUser = _ad.GetUser(_selected.SamAccountName);
            }
            catch (Exception ex)
            {
                OutputBox.Text = "更新前の情報取得に失敗しました。ネットワーク接続を確認してください。";
                LogWriteFailure(changeSet, _selected.DistinguishedName, authResult.ResolvedUser, FormatErrorForLog(ex), false, false);
                return;
            }

            if (currentUser is null)
            {
                OutputBox.Text = "更新対象ユーザーがADに見つかりません。更新を中止しました。";
                LogWriteFailure(changeSet, _selected.DistinguishedName, authResult.ResolvedUser, "対象ユーザーがADに存在しない", false, false);
                return;
            }

            var ouMatched = _policy.AllowedTargetOuDns.Any(ou => IsUnderOu(currentUser.DistinguishedName, ou));
            if (!ouMatched)
            {
                OutputBox.Text = "対象ユーザーが許可OU外のため更新できません。（更新前の再確認結果）";
                LogWriteFailure(changeSet, currentUser.DistinguishedName, authResult.ResolvedUser, "許可OU外（再取得確認）", false, false);
                return;
            }

            var excluded = _policy.ExcludedSamAccountNames.Any(x => string.Equals(x, currentUser.SamAccountName, StringComparison.OrdinalIgnoreCase));
            if (excluded)
            {
                OutputBox.Text = "対象ユーザーは除外アカウントのため更新できません。（更新前の再確認結果）";
                LogWriteFailure(changeSet, currentUser.DistinguishedName, authResult.ResolvedUser, "除外アカウント（再取得確認）", true, true);
                return;
            }

            foreach (var change in changeSet.Changes)
            {
                var adValue = change.LdapAttribute switch
                {
                    "mail" => currentUser.Mail,
                    "displayName" => currentUser.DisplayName,
                    "sn" => currentUser.Surname,
                    "givenName" => currentUser.GivenName,
                    _ => null
                };
                if (!string.Equals(adValue, change.Before, StringComparison.Ordinal))
                {
                    OutputBox.Text = $"AD上の値が変更されているため更新を中止しました。\n再度「差分確認」から実行してください。\n（{change.Field}: AD現在値「{adValue}」/ 確認時点「{change.Before}」）";
                    LogWriteFailure(changeSet, currentUser.DistinguishedName, authResult.ResolvedUser,
                        $"AD値不一致 ldap={change.LdapAttribute}", true, false);
                    return;
                }
            }

            UpdateResult writeResult;
            try
            {
                writeResult = _writeService.UpdateUserAttributes(currentUser.DistinguishedName, changeSet, reAuthUser, reAuthPassword);
            }
            catch (Exception ex)
            {
                OutputBox.Text = "更新処理中にエラーが発生しました。監査ログを確認してください。";
                LogWriteFailure(changeSet, currentUser.DistinguishedName, authResult.ResolvedUser,
                    FormatErrorForLog(ex), true, false);
                return;
            }

            if (!writeResult.Success)
            {
                OutputBox.Text = $"更新に失敗しました。\n{writeResult.ErrorMessage}\n詳細は監査ログを確認してください。";
                LogWriteFailure(changeSet, currentUser.DistinguishedName, authResult.ResolvedUser,
                    writeResult.ErrorMessage ?? "不明なエラー", true, false);
                return;
            }

            AdUser? verifiedUser = null;
            try { verifiedUser = _ad.GetUser(currentUser.SamAccountName); } catch { }

            var revertCandidate = changeSet.Changes.ToDictionary(c => c.Field, c => c.Before);

            var auditEntry = new WriteAuditEntry
            {
                ServiceMode = _policy.ServiceMode,
                Executor = Executor,
                MachineName = Environment.MachineName,
                EditorUser = authResult.ResolvedUser,
                TargetSamAccountName = currentUser.SamAccountName,
                TargetDisplayName = currentUser.DisplayName,
                TargetDn = currentUser.DistinguishedName,
                Changes = changeSet.Changes,
                Success = true,
                VerifiedAfterUpdate = verifiedUser is null ? null : new Dictionary<string, string>
                {
                    ["mail"] = verifiedUser.Mail,
                    ["displayName"] = verifiedUser.DisplayName,
                    ["sn"] = verifiedUser.Surname,
                    ["givenName"] = verifiedUser.GivenName
                },
                RevertCandidate = revertCandidate,
                AllowedTargetOuMatched = true,
                ExcludedAccountMatched = false
            };
            var auditSaved = _writeAuditLogger.Log(auditEntry);
            _authAuditLogger.Log("WriteExecuted", authResult.ResolvedUser, success: true,
                $"target={currentUser.SamAccountName}");

            if (!auditSaved)
                _authAuditLogger.Log("WriteAuditSaveFailed", authResult.ResolvedUser, success: false,
                    $"target={currentUser.SamAccountName} write-audit.jsonl への保存失敗");

            _selected = verifiedUser ?? currentUser;
            EditMailBox.Text = _selected.Mail;
            EditDisplayNameBox.Text = _selected.DisplayName;
            EditSurnameBox.Text = _selected.Surname;
            EditGivenNameBox.Text = _selected.GivenName;
            SamAccountNameReadBox.Text = _selected.SamAccountName;
            UserDetailBox.Text = FormatUserDetails(_selected);
            _pending = null;
            _vm.SetPendingReady(false);

            // 戻し用メモを保存してボタンを有効化
            _revertMemoText = BuildRevertMemo(currentUser.SamAccountName, currentUser.DisplayName, revertCandidate);
            CopyRevertMemoButton.IsEnabled = true;

            OutputBox.Text = BuildSuccessOutput(currentUser, changeSet, verifiedUser, auditSaved);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private static string BuildSuccessOutput(AdUser target, ChangeSet changeSet, AdUser? verifiedUser, bool auditSaved)
    {
        var lines = new List<string>
        {
            $"更新成功: {target.SamAccountName}（{target.DisplayName}）",
            string.Empty,
            "対象DN:",
            $"  {target.DistinguishedName}",
            string.Empty,
            "更新内容:"
        };

        foreach (var change in changeSet.Changes)
        {
            string verifiedVal;
            if (verifiedUser is null)
                verifiedVal = "（AD再取得失敗）";
            else
                verifiedVal = change.LdapAttribute switch
                {
                    "mail" => verifiedUser.Mail,
                    "displayName" => verifiedUser.DisplayName,
                    "sn" => verifiedUser.Surname,
                    "givenName" => verifiedUser.GivenName,
                    _ => "（不明）"
                };

            lines.Add($"  - {change.Field}");
            lines.Add($"    変更前: {change.Before}");
            lines.Add($"    変更後: {change.After}");
            lines.Add($"    AD再取得: {verifiedVal}");
        }

        if (verifiedUser is null)
        {
            lines.Add(string.Empty);
            lines.Add("※ 更新後のAD再取得に失敗しました。ADUCで手動確認してください。");
        }

        lines.Add(string.Empty);
        lines.Add("戻し候補（変更前の値）:");
        foreach (var change in changeSet.Changes)
            lines.Add($"  {change.Field} = {change.Before}");
        lines.Add("  ↑「戻し用メモをコピー」ボタンでクリップボードにコピーできます");

        lines.Add(string.Empty);
        lines.Add(auditSaved
            ? "監査ログ: write-audit.jsonl に保存済み"
            : "監査ログ: ⚠ write-audit.jsonl への保存に失敗しました。管理者に連絡してください。");

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildRevertMemo(string sam, string displayName, Dictionary<string, string> revertValues)
    {
        var lines = new List<string>
        {
            "戻し候補メモ (ManageAdTool v0.4.2)",
            $"対象: {sam}（{displayName}）",
            $"記録日時: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}",
            "戻し値:"
        };
        foreach (var kv in revertValues)
            lines.Add($"  {kv.Key} = {kv.Value}");
        return string.Join(Environment.NewLine, lines);
    }

    private void LogWriteFailure(ChangeSet changeSet, string targetDn, string editorUser, string error,
        bool ouMatched, bool excludedMatched)
    {
        _writeAuditLogger.Log(new WriteAuditEntry
        {
            ServiceMode = _policy.ServiceMode,
            Executor = Executor,
            MachineName = Environment.MachineName,
            EditorUser = editorUser,
            TargetSamAccountName = changeSet.TargetSamAccountName,
            TargetDisplayName = changeSet.TargetDisplayName,
            TargetDn = targetDn,
            Changes = changeSet.Changes,
            Success = false,
            Error = error,
            AllowedTargetOuMatched = ouMatched,
            ExcludedAccountMatched = excludedMatched
        });
    }

    private void ExportSearchResults_Click(object sender, RoutedEventArgs e)
    {
        if (_lastSearchResults.Count == 0)
        {
            OutputBox.Text = "CSV出力できる検索結果がありません";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "検索結果CSV出力",
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            FileName = $"ManageAdTool-Users-{DateTime.Now:yyyyMMdd-HHmmss}.csv"
        };

        if (dialog.ShowDialog(this) != true) return;

        File.WriteAllText(dialog.FileName, BuildSearchResultsCsv(_lastSearchResults), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        OutputBox.Text = $"検索結果CSVを出力しました: {dialog.FileName}";
        _auditLogger.Log("UserSearchCsvExport", dialog.FileName, _lastSearchResults.Count, success: true);
    }

    private void CopyGroups_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(GroupListBox.Text)) Clipboard.SetText(GroupListBox.Text);
    }

    private void CopyOutput_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(OutputBox.Text)) Clipboard.SetText(OutputBox.Text);
    }

    private AdUserSearchCriteria BuildUserSearchCriteria()
        => new()
        {
            Keyword = SearchBox.Text.Trim(),
            IncludeDisabled = IncludeDisabledUsersBox.IsChecked == true
        };

    private void ClearSearchResults()
    {
        _lastSearchResults = Array.Empty<AdUser>();
        SearchResultGrid.ItemsSource = Array.Empty<AdUser>();
        UserDetailBox.Text = string.Empty;
        GroupListBox.Text = string.Empty;
    }

    private string FormatUserDetails(AdUser user)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["SamAccountName"] = user.SamAccountName,
            ["DisplayName"] = user.DisplayName,
            ["Surname"] = user.Surname,
            ["GivenName"] = user.GivenName,
            ["Name"] = user.Name,
            ["Mail"] = user.Mail,
            ["Department"] = user.Department,
            ["Title"] = user.Title,
            ["DistinguishedName"] = user.DistinguishedName,
            ["Enabled"] = FormatBool(user.Enabled),
            ["UserAccountControl"] = FormatNullable(user.UserAccountControl),
            ["LastLogonTimestamp"] = FormatDateTime(user.LastLogonAt),
            ["AccountExpires"] = FormatDateTime(user.AccountExpiresAt),
            ["LastLogonComputer"] = user.LastLogonComputer
        };

        return string.Join(Environment.NewLine, _policy.UserDetailDisplayAttributes.Select(attribute =>
            values.TryGetValue(attribute, out var value) ? $"{attribute}: {value}" : $"{attribute}: (未対応)"));
    }

    private static string BuildSearchResultsCsv(IEnumerable<AdUser> users)
    {
        var lines = new List<string>
        {
            string.Join(",", new[]
            {
                "SamAccountName", "DisplayName", "Surname", "GivenName", "Name", "Mail",
                "Enabled", "UserAccountControl", "LastLogonTimestamp", "AccountExpires", "DistinguishedName"
            }.Select(CsvEscape))
        };

        lines.AddRange(users.Select(user => string.Join(",", new[]
        {
            user.SamAccountName, user.DisplayName, user.Surname, user.GivenName, user.Name, user.Mail,
            FormatBool(user.Enabled), FormatNullable(user.UserAccountControl),
            FormatDateTime(user.LastLogonAt), FormatDateTime(user.AccountExpiresAt), user.DistinguishedName
        }.Select(CsvEscape))));

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static string FormatChangePreview(ChangeSet cs, string operation)
    {
        if (cs.Changes.Count == 0) return "差分なし（更新不要）";
        var lines = new List<string> { $"対象: {cs.TargetSamAccountName}（{cs.TargetDisplayName}）", $"操作: {operation}" };
        lines.AddRange(cs.Changes.Select(c => $"- {c.Field}（{c.LdapAttribute}）: 「{c.Before}」→「{c.After}」"));
        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatCriteria(AdUserSearchCriteria criteria)
    {
        return $"keyword={criteria.Keyword}; includeDisabled={criteria.IncludeDisabled}";
    }

    private static string FormatDateTime(DateTimeOffset? value)
        => value.HasValue ? value.Value.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss") : "(未設定)";

    private static string FormatNullable(int? value) => value.HasValue ? value.Value.ToString() : "(未取得)";
    private static string FormatBool(bool value) => value ? "True" : "False";
    private static string CsvEscape(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
    private static string FormatErrorForLog(Exception ex)
        => ex.InnerException is null ? ex.Message : $"{ex.Message} / {ex.InnerException.Message}";

    private static bool IsUnderOu(string distinguishedName, string ouDn)
        => !string.IsNullOrWhiteSpace(distinguishedName)
            && !string.IsNullOrWhiteSpace(ouDn)
            && distinguishedName.EndsWith($",{ouDn}", StringComparison.OrdinalIgnoreCase);

    // ── Computer tab ───────────────────────────────────────────────────────────

    private void ComputerSearch_Click(object sender, RoutedEventArgs e)
    {
        var criteria = BuildComputerSearchCriteria();
        if (string.IsNullOrWhiteSpace(criteria.Keyword) || criteria.Keyword.Length <= 1)
        {
            OutputBox.Text = "検索語は2文字以上入力してください";
            ClearComputerSearchResults();
            return;
        }

        try
        {
            var results = _ad.SearchComputers(criteria);
            _selectedComputer = null;
            _pendingComputer = null;
            _vm.SetComputerPendingReady(false);
            _lastComputerSearchResults = results;
            ComputerSearchResultGrid.ItemsSource = results;
            ComputerDetailBox.Text = string.Empty;
            ComputerGroupListBox.Text = string.Empty;
            var exceeded = results.Count >= _policy.MaxSearchResults;
            OutputBox.Text = exceeded
                ? $"コンピュータ検索結果: {results.Count}件（上限 {_policy.MaxSearchResults} 件に達しました。検索条件を絞り込んでください）"
                : $"コンピュータ検索結果: {results.Count}件";
            _auditLogger.Log("ComputerSearch", FormatComputerCriteria(criteria), results.Count, success: true);
        }
        catch (Exception ex)
        {
            ClearComputerSearchResults();
            OutputBox.Text = "コンピュータ検索に失敗しました。ネットワーク接続またはAD設定を確認してください。";
            _auditLogger.Log("ComputerSearch", FormatComputerCriteria(criteria), 0, success: false, FormatErrorForLog(ex));
        }
    }

    private void ComputerSearchResultGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        _selectedComputer = ComputerSearchResultGrid.SelectedItem as AdComputer;
        _pendingComputer = null;
        _vm.SetComputerPendingReady(false);
        _computerRevertMemoText = string.Empty;
        CopyComputerRevertMemoButton.IsEnabled = false;
        if (_selectedComputer is null) return;

        EditComputerDescriptionBox.Text = _selectedComputer.Description;
        ComputerDetailBox.Text = FormatComputerDetails(_selectedComputer);
        try
        {
            var groups = _ad.GetComputerGroups(_selectedComputer.Name)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
            ComputerGroupListBox.Text = string.Join(Environment.NewLine, groups);
            OutputBox.Text = $"コンピュータ詳細表示: {_selectedComputer.Name} / 所属グループ {groups.Count}件";
            _auditLogger.Log("ComputerDetail", _selectedComputer.Name, 1, success: true);
            _auditLogger.Log("ComputerGroups", _selectedComputer.Name, groups.Count, success: true);
        }
        catch (Exception ex)
        {
            OutputBox.Text = "コンピュータ詳細取得に失敗しました。ネットワーク接続を確認してください。";
            ComputerGroupListBox.Text = string.Empty;
            _auditLogger.Log("ComputerDetail", _selectedComputer.Name, 0, success: false, FormatErrorForLog(ex));
        }

        EvaluateComputerEditability();
    }

    private void ComputerDescriptionInput_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_pendingComputer is not null)
        {
            _pendingComputer = null;
            _vm.SetComputerPendingReady(false);
        }
    }

    private void ComputerPreview_Click(object sender, RoutedEventArgs e)
    {
        if (!_vm.ComputerCanEdit || _selectedComputer is null) return;
        _pendingComputer = _ad.BuildComputerChangeSet(_selectedComputer, EditComputerDescriptionBox.Text.Trim());
        _vm.SetComputerPendingReady(_pendingComputer.Changes.Count > 0);
        OutputBox.Text = FormatComputerChangePreview(_pendingComputer, "差分確認");
    }

    private void CopyComputerRevertMemo_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_computerRevertMemoText))
            Clipboard.SetText(_computerRevertMemoText);
    }

    private void CopyComputerGroups_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(ComputerGroupListBox.Text))
            Clipboard.SetText(ComputerGroupListBox.Text);
    }

    private void ExportComputerSearchResults_Click(object sender, RoutedEventArgs e)
    {
        if (_lastComputerSearchResults.Count == 0)
        {
            OutputBox.Text = "CSV出力できるコンピュータ検索結果がありません";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "コンピュータ検索結果CSV出力",
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            FileName = $"ManageAdTool-Computers-{DateTime.Now:yyyyMMdd-HHmmss}.csv"
        };

        if (dialog.ShowDialog(this) != true) return;

        File.WriteAllText(dialog.FileName, BuildComputerSearchResultsCsv(_lastComputerSearchResults), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        OutputBox.Text = $"コンピュータ検索結果CSVを出力しました: {dialog.FileName}";
        _auditLogger.Log("ComputerSearchCsvExport", dialog.FileName, _lastComputerSearchResults.Count, success: true);
    }

    private void ComputerExecute_Click(object sender, RoutedEventArgs e)
    {
        if (_computerWriteService is null)
        {
            OutputBox.Text = "ADコンピュータ更新はこのモードでは使用できません（DirectoryReadOnly が必要です）";
            return;
        }

        if (!_vm.IsEditSessionActive)
        {
            OutputBox.Text = "編集セッションが期限切れです。再ログインしてください。";
            return;
        }

        if (_policy.EffectiveComputerOuDns.Count == 0)
        {
            OutputBox.Text = "AllowedComputerOuDns / AllowedTargetOuDns が未設定のため更新できません。appsettings.json を確認してください。";
            return;
        }

        if (_selectedComputer is null) return;

        if (!_logPathWritable)
        {
            var proceed = MessageBox.Show(
                $"監査ログディレクトリへの書き込みができません。\n({Path.GetDirectoryName(_policy.LogPath)})\n\n更新を実行すると監査ログ（write-audit.jsonl）が記録されません。\n続行しますか？",
                "監査ログ警告", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (proceed != MessageBoxResult.Yes) return;
        }

        var changeSet = _ad.BuildComputerChangeSet(_selectedComputer, EditComputerDescriptionBox.Text.Trim());

        var emptyUpdates = changeSet.Changes.Where(c => string.IsNullOrWhiteSpace(c.After)).ToList();
        if (emptyUpdates.Count > 0)
        {
            OutputBox.Text = $"空文字への更新は禁止されています: {string.Join(", ", emptyUpdates.Select(c => c.Field))}（属性クリアは将来機能です）";
            return;
        }

        if (changeSet.Changes.Count == 0)
        {
            OutputBox.Text = "差分なし（更新不要）";
            _vm.SetComputerPendingReady(false);
            return;
        }

        var reAuthDlg = new ReAuthDialog(_vm.CurrentEditorUser) { Owner = this };
        if (reAuthDlg.ShowDialog() != true) return;

        var reAuthUser = reAuthDlg.DomainUser!;
        var reAuthPassword = reAuthDlg.Password!;

        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            var authResult = _authService.TryAuthenticate(reAuthUser, reAuthPassword, _policy);

            if (!authResult.AuthSucceeded)
            {
                OutputBox.Text = "再認証に失敗しました。ユーザー名またはパスワードを確認してください。";
                _authAuditLogger.Log("ReAuthFailed", reAuthUser, success: false, authResult.ErrorMessage ?? string.Empty);
                return;
            }

            if (!authResult.IsDomainAdmin)
            {
                OutputBox.Text = $"再認証したユーザー（{authResult.ResolvedUser}）は Domain Admins のメンバーではありません。";
                _authAuditLogger.Log("ReAuthDenied", authResult.ResolvedUser, success: false, "Domain Admins 非メンバー");
                return;
            }

            if (!string.Equals(authResult.ResolvedUser, _vm.CurrentEditorUser, StringComparison.OrdinalIgnoreCase))
            {
                OutputBox.Text = "再認証ユーザーと編集セッションユーザーが一致しません。ログアウトして再度ログインしてください。";
                _authAuditLogger.Log("ReAuthUserMismatch", authResult.ResolvedUser, success: false,
                    $"session={_vm.CurrentEditorUser} reauth={authResult.ResolvedUser}");
                return;
            }

            Mouse.OverrideCursor = null;

            var confirmDlg = new ConfirmComputerUpdateDialog(
                changeSet, _selectedComputer.DistinguishedName, _selectedComputer.Name,
                _selectedComputer.DnsHostName, authResult.ResolvedUser, Executor, Environment.MachineName) { Owner = this };
            if (confirmDlg.ShowDialog() != true) return;

            Mouse.OverrideCursor = Cursors.Wait;

            AdComputer? currentComputer;
            try
            {
                currentComputer = _ad.GetComputer(_selectedComputer.Name);
            }
            catch (Exception ex)
            {
                OutputBox.Text = "更新前の情報取得に失敗しました。ネットワーク接続を確認してください。";
                LogComputerWriteFailure(changeSet, _selectedComputer.DistinguishedName, authResult.ResolvedUser, FormatErrorForLog(ex), false, false);
                return;
            }

            if (currentComputer is null)
            {
                OutputBox.Text = "更新対象コンピュータがADに見つかりません。更新を中止しました。";
                LogComputerWriteFailure(changeSet, _selectedComputer.DistinguishedName, authResult.ResolvedUser, "対象コンピュータがADに存在しない", false, false);
                return;
            }

            var ouMatched = _policy.EffectiveComputerOuDns.Any(ou => IsUnderOu(currentComputer.DistinguishedName, ou));
            if (!ouMatched)
            {
                OutputBox.Text = "対象コンピュータが許可OU外のため更新できません。（更新前の再確認結果）";
                LogComputerWriteFailure(changeSet, currentComputer.DistinguishedName, authResult.ResolvedUser, "許可OU外（再取得確認）", false, false);
                return;
            }

            var excluded = _policy.ExcludedComputerNames.Any(x => string.Equals(x, currentComputer.Name, StringComparison.OrdinalIgnoreCase));
            if (excluded)
            {
                OutputBox.Text = "対象コンピュータは除外リストのため更新できません。（更新前の再確認結果）";
                LogComputerWriteFailure(changeSet, currentComputer.DistinguishedName, authResult.ResolvedUser, "除外コンピュータ（再取得確認）", true, true);
                return;
            }

            foreach (var change in changeSet.Changes)
            {
                var adValue = change.LdapAttribute == "description" ? currentComputer.Description : null;
                if (!string.Equals(adValue, change.Before, StringComparison.Ordinal))
                {
                    OutputBox.Text = $"AD上の値が変更されているため更新を中止しました。\n再度「差分確認」から実行してください。\n（{change.Field}: AD現在値「{adValue}」/ 確認時点「{change.Before}」）";
                    LogComputerWriteFailure(changeSet, currentComputer.DistinguishedName, authResult.ResolvedUser,
                        $"AD値不一致 ldap={change.LdapAttribute}", true, false);
                    return;
                }
            }

            UpdateResult writeResult;
            try
            {
                writeResult = _computerWriteService.UpdateComputerDescription(currentComputer.DistinguishedName, changeSet, reAuthUser, reAuthPassword);
            }
            catch (Exception ex)
            {
                OutputBox.Text = "更新処理中にエラーが発生しました。監査ログを確認してください。";
                LogComputerWriteFailure(changeSet, currentComputer.DistinguishedName, authResult.ResolvedUser,
                    FormatErrorForLog(ex), true, false);
                return;
            }

            if (!writeResult.Success)
            {
                OutputBox.Text = $"更新に失敗しました。\n{writeResult.ErrorMessage}\n詳細は監査ログを確認してください。";
                LogComputerWriteFailure(changeSet, currentComputer.DistinguishedName, authResult.ResolvedUser,
                    writeResult.ErrorMessage ?? "不明なエラー", true, false);
                return;
            }

            AdComputer? verifiedComputer = null;
            try { verifiedComputer = _ad.GetComputer(currentComputer.Name); } catch { }

            var revertCandidate = changeSet.Changes.ToDictionary(c => c.Field, c => c.Before);

            var auditEntry = new WriteAuditEntry
            {
                ServiceMode = _policy.ServiceMode,
                Executor = Executor,
                MachineName = Environment.MachineName,
                EditorUser = authResult.ResolvedUser,
                TargetType = "Computer",
                TargetName = currentComputer.Name,
                OperationName = "UpdateComputerDescription",
                TargetSamAccountName = currentComputer.SamAccountName,
                TargetDisplayName = currentComputer.Name,
                TargetDn = currentComputer.DistinguishedName,
                Changes = changeSet.Changes,
                Success = true,
                VerifiedAfterUpdate = verifiedComputer is null ? null : new Dictionary<string, string>
                {
                    ["description"] = verifiedComputer.Description
                },
                RevertCandidate = revertCandidate,
                AllowedTargetOuMatched = true,
                ExcludedAccountMatched = false
            };
            var auditSaved = _writeAuditLogger.Log(auditEntry);
            _authAuditLogger.Log("WriteExecuted", authResult.ResolvedUser, success: true,
                $"target={currentComputer.Name} targetType=Computer");

            if (!auditSaved)
                _authAuditLogger.Log("WriteAuditSaveFailed", authResult.ResolvedUser, success: false,
                    $"target={currentComputer.Name} write-audit.jsonl への保存失敗");

            _selectedComputer = verifiedComputer ?? currentComputer;
            EditComputerDescriptionBox.Text = _selectedComputer.Description;
            ComputerDetailBox.Text = FormatComputerDetails(_selectedComputer);
            _pendingComputer = null;
            _vm.SetComputerPendingReady(false);

            _computerRevertMemoText = BuildComputerRevertMemo(currentComputer.Name, revertCandidate);
            CopyComputerRevertMemoButton.IsEnabled = true;

            OutputBox.Text = BuildComputerSuccessOutput(currentComputer, changeSet, verifiedComputer, auditSaved);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private void EvaluateComputerEditability()
    {
        if (_selectedComputer is null) { ApplyComputerEditability(false, "コンピュータ未選択"); return; }
        if (!_vm.IsReadOnlyMode) { ApplyComputerEditability(false, "DirectoryReadOnly モードが必要です"); return; }
        if (!_vm.IsEditSessionActive) { ApplyComputerEditability(false, "編集セッション未開始（ログインしてください）"); return; }
        if (_policy.EffectiveComputerOuDns.Count == 0) { ApplyComputerEditability(false, "AllowedComputerOuDns / AllowedTargetOuDns 未設定のため更新不可"); return; }
        if (!_policy.EffectiveComputerOuDns.Any(ou => IsUnderOu(_selectedComputer.DistinguishedName, ou))) { ApplyComputerEditability(false, "対象コンピュータが許可OU外"); return; }
        if (_policy.ExcludedComputerNames.Any(x => string.Equals(x, _selectedComputer.Name, StringComparison.OrdinalIgnoreCase))) { ApplyComputerEditability(false, "除外コンピュータ名のため更新不可"); return; }
        ApplyComputerEditability(true, string.Empty);
    }

    private void ApplyComputerEditability(bool canEdit, string reason)
    {
        _vm.ComputerCanEdit = canEdit;
        _vm.ComputerEditBlockedReason = reason;
    }

    private void ClearComputerSearchResults()
    {
        _lastComputerSearchResults = Array.Empty<AdComputer>();
        ComputerSearchResultGrid.ItemsSource = Array.Empty<AdComputer>();
        ComputerDetailBox.Text = string.Empty;
        ComputerGroupListBox.Text = string.Empty;
    }

    private AdComputerSearchCriteria BuildComputerSearchCriteria()
        => new()
        {
            Keyword = ComputerSearchBox.Text.Trim(),
            OperatingSystem = ComputerOsFilterBox.Text.Trim(),
            HasDescription = ComputerDescFilterBox.SelectedIndex switch { 1 => true, 2 => false, _ => null },
            IncludeDisabled = IncludeDisabledComputersBox.IsChecked == true
        };

    private static string FormatComputerDetails(AdComputer computer)
    {
        var lines = new List<string>
        {
            $"Name: {computer.Name}",
            $"SamAccountName: {computer.SamAccountName}",
            $"DNSHostName: {computer.DnsHostName}",
            $"OperatingSystem: {computer.OperatingSystem}",
            $"Description: {computer.Description}",
            $"Enabled: {FormatBool(computer.Enabled)}",
            $"DistinguishedName: {computer.DistinguishedName}",
            $"LastLogon: {FormatDateTime(computer.LastLogonAt)}",
            $"WhenCreated: {FormatDateTime(computer.WhenCreated)}",
            $"WhenChanged: {FormatDateTime(computer.WhenChanged)}"
        };
        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatComputerChangePreview(ChangeSet cs, string operation)
    {
        if (cs.Changes.Count == 0) return "差分なし（更新不要）";
        var lines = new List<string> { $"対象コンピュータ: {cs.TargetSamAccountName}（{cs.TargetDisplayName}）", $"操作: {operation}" };
        lines.AddRange(cs.Changes.Select(c => $"- {c.Field}（{c.LdapAttribute}）: 「{c.Before}」→「{c.After}」"));
        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildComputerSuccessOutput(AdComputer target, ChangeSet changeSet, AdComputer? verifiedComputer, bool auditSaved)
    {
        var lines = new List<string>
        {
            $"更新成功: {target.Name}（{target.DnsHostName}）",
            string.Empty,
            "対象DN:",
            $"  {target.DistinguishedName}",
            string.Empty,
            "更新内容:"
        };

        foreach (var change in changeSet.Changes)
        {
            var verifiedVal = verifiedComputer is null ? "（AD再取得失敗）"
                : change.LdapAttribute == "description" ? verifiedComputer.Description : "（不明）";
            lines.Add($"  - {change.Field}");
            lines.Add($"    変更前: {change.Before}");
            lines.Add($"    変更後: {change.After}");
            lines.Add($"    AD再取得: {verifiedVal}");
        }

        if (verifiedComputer is null)
        {
            lines.Add(string.Empty);
            lines.Add("※ 更新後のAD再取得に失敗しました。ADUCで手動確認してください。");
        }

        lines.Add(string.Empty);
        lines.Add("戻し候補（変更前の値）:");
        foreach (var change in changeSet.Changes)
            lines.Add($"  {change.Field} = {change.Before}");
        lines.Add("  ↑「戻し用メモをコピー」ボタンでクリップボードにコピーできます");

        lines.Add(string.Empty);
        lines.Add(auditSaved
            ? "監査ログ: write-audit.jsonl に保存済み"
            : "監査ログ: ⚠ write-audit.jsonl への保存に失敗しました。管理者に連絡してください。");

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildComputerRevertMemo(string computerName, Dictionary<string, string> revertValues)
    {
        var lines = new List<string>
        {
            "戻し候補メモ (ManageAdTool v0.5.0 - Computer)",
            $"対象コンピュータ: {computerName}",
            $"記録日時: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}",
            "戻し値:"
        };
        foreach (var kv in revertValues)
            lines.Add($"  {kv.Key} = {kv.Value}");
        return string.Join(Environment.NewLine, lines);
    }

    private void LogComputerWriteFailure(ChangeSet changeSet, string targetDn, string editorUser, string error,
        bool ouMatched, bool excludedMatched)
    {
        _writeAuditLogger.Log(new WriteAuditEntry
        {
            ServiceMode = _policy.ServiceMode,
            Executor = Executor,
            MachineName = Environment.MachineName,
            EditorUser = editorUser,
            TargetType = "Computer",
            TargetName = changeSet.TargetSamAccountName,
            OperationName = "UpdateComputerDescription",
            TargetSamAccountName = changeSet.TargetSamAccountName,
            TargetDisplayName = changeSet.TargetDisplayName,
            TargetDn = targetDn,
            Changes = changeSet.Changes,
            Success = false,
            Error = error,
            AllowedTargetOuMatched = ouMatched,
            ExcludedAccountMatched = excludedMatched
        });
    }

    private static string BuildComputerSearchResultsCsv(IEnumerable<AdComputer> computers)
    {
        var lines = new List<string>
        {
            string.Join(",", new[]
            {
                "Name", "SamAccountName", "DNSHostName", "OperatingSystem", "Description",
                "Enabled", "DistinguishedName", "LastLogon", "WhenCreated", "WhenChanged"
            }.Select(CsvEscape))
        };

        lines.AddRange(computers.Select(c => string.Join(",", new[]
        {
            c.Name, c.SamAccountName, c.DnsHostName, c.OperatingSystem, c.Description,
            FormatBool(c.Enabled), c.DistinguishedName,
            FormatDateTime(c.LastLogonAt), FormatDateTime(c.WhenCreated), FormatDateTime(c.WhenChanged)
        }.Select(CsvEscape))));

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static string FormatComputerCriteria(AdComputerSearchCriteria criteria)
    {
        var desc = criteria.HasDescription switch { true => "あり", false => "なし", _ => "指定なし" };
        return $"keyword={criteria.Keyword}; os={criteria.OperatingSystem}; description={desc}; includeDisabled={criteria.IncludeDisabled}";
    }

    // ─── オペレーション支援 ──────────────────────────────────────────────────

    private void OpSearch_Click(object sender, RoutedEventArgs e)
    {
        var keyword = OpSearchBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(keyword))
        {
            OpSearchResultGrid.ItemsSource = null;
            return;
        }

        try
        {
            var results = _ad.SearchUsers(new AdUserSearchCriteria { Keyword = keyword });
            OpSearchResultGrid.ItemsSource = results.ToList();
            _auditLogger.Log("OpUserSearch", keyword, results.Count, true);
        }
        catch
        {
            MessageBox.Show("ユーザー検索に失敗しました。ネットワーク接続またはAD設定を確認してください。", "検索エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OpSearchResultGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (OpSearchResultGrid.SelectedItem is not AdUser selected) return;

        try
        {
            var user = _ad.GetUser(selected.SamAccountName) ?? selected;
            _opSelectedUser = user;

            OpCurrentSamBox.Text = user.SamAccountName;
            OpCurrentDisplayNameBox.Text = user.DisplayName;
            OpCurrentMailBox.Text = user.Mail;
            OpCurrentSurnameBox.Text = user.Surname;
            OpCurrentGivenNameBox.Text = user.GivenName;

            OpNewDisplayNameBox.Text = user.DisplayName;
            OpNewSurnameBox.Text = user.Surname;
            OpNewGivenNameBox.Text = user.GivenName;
            OpNewMailBox.Text = user.Mail;

            var groups = _ad.GetUserGroups(user.SamAccountName);
            OpCurrentGroupsBox.Text = string.Join(Environment.NewLine, groups.OrderBy(g => g));

            _opGroupAddPlanned.Clear();
            _opGroupRemovePlanned.Clear();
            OpGroupAddList.ItemsSource = null;
            OpGroupRemoveList.ItemsSource = null;
            OpSummaryBox.Text = string.Empty;

            _auditLogger.Log("OpUserDetail", user.SamAccountName, 1, true);
        }
        catch
        {
            MessageBox.Show("ユーザー詳細の取得に失敗しました。ネットワーク接続を確認してください。", "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OpAddToGroupAdds_Click(object sender, RoutedEventArgs e)
    {
        var name = OpGroupAddInputBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;
        if (_opGroupAddPlanned.Any(g => string.Equals(g, name, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show($"「{name}」はすでに追加予定に登録されています。", "重複", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (_opGroupRemovePlanned.Any(g => string.Equals(g, name, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show($"「{name}」は削除予定に登録されています。同じグループを追加予定と削除予定の両方に登録できません。", "矛盾", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        _opGroupAddPlanned.Add(name);
        OpGroupAddList.ItemsSource = null;
        OpGroupAddList.ItemsSource = _opGroupAddPlanned.ToList();
        OpGroupAddInputBox.Clear();
    }

    private void OpAddToGroupRemoves_Click(object sender, RoutedEventArgs e)
    {
        var name = OpGroupRemoveInputBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;
        if (_opGroupRemovePlanned.Any(g => string.Equals(g, name, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show($"「{name}」はすでに削除予定に登録されています。", "重複", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (_opGroupAddPlanned.Any(g => string.Equals(g, name, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show($"「{name}」は追加予定に登録されています。同じグループを追加予定と削除予定の両方に登録できません。", "矛盾", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        _opGroupRemovePlanned.Add(name);
        OpGroupRemoveList.ItemsSource = null;
        OpGroupRemoveList.ItemsSource = _opGroupRemovePlanned.ToList();
        OpGroupRemoveInputBox.Clear();
    }

    private void OpRemoveFromGroupAdds_Click(object sender, RoutedEventArgs e)
    {
        var idx = OpGroupAddList.SelectedIndex;
        if (idx < 0 || idx >= _opGroupAddPlanned.Count) return;
        _opGroupAddPlanned.RemoveAt(idx);
        OpGroupAddList.ItemsSource = null;
        OpGroupAddList.ItemsSource = _opGroupAddPlanned.ToList();
    }

    private void OpRemoveFromGroupRemoves_Click(object sender, RoutedEventArgs e)
    {
        var idx = OpGroupRemoveList.SelectedIndex;
        if (idx < 0 || idx >= _opGroupRemovePlanned.Count) return;
        _opGroupRemovePlanned.RemoveAt(idx);
        OpGroupRemoveList.ItemsSource = null;
        OpGroupRemoveList.ItemsSource = _opGroupRemovePlanned.ToList();
    }

    private void OpGenerateSummary_Click(object sender, RoutedEventArgs e)
    {
        if (_opSelectedUser is null)
        {
            MessageBox.Show("対象ユーザーを選択してください。", "未選択", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var summary = BuildOperationSummary();
        OpSummaryBox.Text = summary;

        var attrChanges = CountOpAttrChanges();
        _auditLogger.LogOperationPlan(Executor, _opSelectedUser.SamAccountName, attrChanges, _opGroupAddPlanned.Count, _opGroupRemovePlanned.Count);
    }

    private void OpCopySummary_Click(object sender, RoutedEventArgs e)
    {
        var text = OpSummaryBox.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            MessageBox.Show("コピーするサマリーがありません。先に「変更予定サマリー生成」を実行してください。", "未生成", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        Clipboard.SetText(text);
    }

    private int CountOpAttrChanges()
    {
        if (_opSelectedUser is null) return 0;
        int count = 0;
        var newDn = OpNewDisplayNameBox.Text.Trim();
        var newSn = OpNewSurnameBox.Text.Trim();
        var newGn = OpNewGivenNameBox.Text.Trim();
        var newMail = OpNewMailBox.Text.Trim();
        if (!string.IsNullOrEmpty(newDn) && newDn != _opSelectedUser.DisplayName) count++;
        if (!string.IsNullOrEmpty(newSn) && newSn != _opSelectedUser.Surname) count++;
        if (!string.IsNullOrEmpty(newGn) && newGn != _opSelectedUser.GivenName) count++;
        if (!string.IsNullOrEmpty(newMail) && newMail != _opSelectedUser.Mail) count++;
        return count;
    }

    // ─── 未ログイン確認 ─────────────────────────────────────────────────────

    private void InactiveSearch_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetInactiveDays(out var days)) return;

        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            _lastInactiveDays = days;
            var isComputer = InactiveTargetComboBox.SelectedIndex == 1;

            if (isComputer)
            {
                var results = _ad.SearchInactiveComputers(days);
                _lastInactiveComputers = results;
                _lastInactiveUsers = Array.Empty<AdUser>();
                InactiveComputerGrid.ItemsSource = results;
                InactiveUserGrid.ItemsSource = null;
                InactiveComputerGrid.Visibility = Visibility.Visible;
                InactiveUserGrid.Visibility = Visibility.Collapsed;
                InactiveStatusText.Text = $"{days}日以上ログインしていないコンピュータ: {results.Count}件";
                OutputBox.Text = InactiveStatusText.Text;
                _auditLogger.Log("InactiveComputerSearch", $"days={days}", results.Count, true);
            }
            else
            {
                var results = _ad.SearchInactiveUsers(days);
                _lastInactiveUsers = results;
                _lastInactiveComputers = Array.Empty<AdComputer>();
                InactiveUserGrid.ItemsSource = results;
                InactiveComputerGrid.ItemsSource = null;
                InactiveUserGrid.Visibility = Visibility.Visible;
                InactiveComputerGrid.Visibility = Visibility.Collapsed;
                InactiveStatusText.Text = $"{days}日以上ログインしていないユーザー: {results.Count}件";
                OutputBox.Text = InactiveStatusText.Text;
                _auditLogger.Log("InactiveUserSearch", $"days={days}", results.Count, true);
            }
        }
        catch (Exception ex)
        {
            InactiveStatusText.Text = "未ログイン確認に失敗しました。ネットワーク接続またはAD設定を確認してください。";
            OutputBox.Text = InactiveStatusText.Text;
            _auditLogger.Log("InactiveSearchError", $"days={days}", 0, false, FormatErrorForLog(ex));
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private void InactiveExportCsv_Click(object sender, RoutedEventArgs e)
    {
        var isComputer = InactiveTargetComboBox.SelectedIndex == 1;
        if (isComputer && _lastInactiveComputers.Count == 0)
        {
            OutputBox.Text = "CSV出力できるコンピュータ結果がありません。先に検索してください。";
            return;
        }
        if (!isComputer && _lastInactiveUsers.Count == 0)
        {
            OutputBox.Text = "CSV出力できるユーザー結果がありません。先に検索してください。";
            return;
        }

        var target = isComputer ? "Computers" : "Users";
        var dialog = new SaveFileDialog
        {
            Title = "未ログイン確認CSV出力",
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            FileName = $"ManageAdTool-Inactive-{target}-{_lastInactiveDays}days-{DateTime.Now:yyyyMMdd-HHmmss}.csv"
        };
        if (dialog.ShowDialog(this) != true) return;

        var csv = isComputer
            ? BuildInactiveComputersCsv(_lastInactiveComputers)
            : BuildInactiveUsersCsv(_lastInactiveUsers);
        File.WriteAllText(dialog.FileName, csv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        OutputBox.Text = $"未ログイン確認CSVを出力しました: {dialog.FileName}";
        _auditLogger.Log(isComputer ? "InactiveComputerCsvExport" : "InactiveUserCsvExport", dialog.FileName, isComputer ? _lastInactiveComputers.Count : _lastInactiveUsers.Count, true);
    }

    private bool TryGetInactiveDays(out int days)
    {
        if (!int.TryParse(InactiveDaysBox.Text.Trim(), out days) || days <= 0)
        {
            OutputBox.Text = "未ログイン日数は 1 以上の整数で入力してください。";
            InactiveStatusText.Text = OutputBox.Text;
            return false;
        }
        return true;
    }

    private static string BuildInactiveUsersCsv(IEnumerable<AdUser> users)
    {
        var lines = new List<string>
        {
            string.Join(",", new[] { "SamAccountName", "DisplayName", "Mail", "LastLogonTimestamp", "Enabled", "DistinguishedName" }.Select(CsvEscape))
        };
        lines.AddRange(users.Select(u => string.Join(",", new[]
        {
            u.SamAccountName, u.DisplayName, u.Mail, FormatDateTime(u.LastLogonAt), FormatBool(u.Enabled), u.DistinguishedName
        }.Select(CsvEscape))));
        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static string BuildInactiveComputersCsv(IEnumerable<AdComputer> computers)
    {
        var lines = new List<string>
        {
            string.Join(",", new[] { "Name", "DNSHostName", "OperatingSystem", "LastLogonTimestamp", "Enabled", "DistinguishedName" }.Select(CsvEscape))
        };
        lines.AddRange(computers.Select(c => string.Join(",", new[]
        {
            c.Name, c.DnsHostName, c.OperatingSystem, FormatDateTime(c.LastLogonAt), FormatBool(c.Enabled), c.DistinguishedName
        }.Select(CsvEscape))));
        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    // ─── GPOシミュレーション ─────────────────────────────────────────────────

    private void GpoKindComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (GpoUserInputPanel is null || GpoComputerInputPanel is null) return;
        var idx = GpoKindComboBox.SelectedIndex;
        GpoUserInputPanel.Visibility = idx == 1 ? Visibility.Collapsed : Visibility.Visible;
        GpoComputerInputPanel.Visibility = idx == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void GpoSimulate_Click(object sender, RoutedEventArgs e)
    {
        var kind = GpoKindComboBox.SelectedIndex;
        var userSam = kind != 1 ? GpoUserSamBox.Text.Trim() : string.Empty;
        var computerName = kind != 0 ? GpoComputerNameBox.Text.Trim() : string.Empty;

        if (kind != 1 && string.IsNullOrWhiteSpace(userSam))
        {
            OutputBox.Text = "sAMAccountName を入力してください";
            return;
        }
        if (kind != 0 && string.IsNullOrWhiteSpace(computerName))
        {
            OutputBox.Text = "コンピュータ名を入力してください";
            return;
        }

        var simType = kind switch { 0 => "ユーザー", 1 => "コンピュータ", _ => "ユーザー+コンピュータ" };
        var targetDesc = kind switch { 0 => userSam, 1 => computerName, _ => $"{userSam} + {computerName}" };

        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            var results = _ad.SimulateGpo(
                kind != 1 ? userSam : null,
                kind != 0 ? computerName : null);
            _lastGpoResults = results;
            GpoResultGrid.ItemsSource = results;
            var exceeded = results.Count >= _policy.MaxSearchResults;
            OutputBox.Text = exceeded
                ? $"GPOシミュレーション完了: {targetDesc} / 適用GPO {results.Count}件（上限 {_policy.MaxSearchResults} 件に達しました）"
                : $"GPOシミュレーション完了: {targetDesc} / 適用GPO {results.Count}件";
            _auditLogger.LogGpoSimulation(Executor, kind != 1 ? userSam : string.Empty, kind != 0 ? computerName : string.Empty, simType, results.Count);
        }
        catch (Exception ex)
        {
            _lastGpoResults = Array.Empty<GpoSimulationResult>();
            GpoResultGrid.ItemsSource = null;
            OutputBox.Text = "GPOシミュレーションに失敗しました。ネットワーク接続またはAD設定を確認してください。";
            _auditLogger.Log("GpoSimulationError", targetDesc, 0, false, FormatErrorForLog(ex));
            _auditLogger.LogGpoSimulation(Executor, kind != 1 ? userSam : string.Empty, kind != 0 ? computerName : string.Empty, simType, 0);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private void GpoCopyResults_Click(object sender, RoutedEventArgs e)
    {
        if (_lastGpoResults.Count == 0)
        {
            OutputBox.Text = "コピーできる結果がありません。先にシミュレーションを実行してください。";
            return;
        }
        Clipboard.SetText(BuildGpoResultText(_lastGpoResults));
        OutputBox.Text = "GPOシミュレーション結果をクリップボードにコピーしました";
    }

    private void GpoCopySimpleResults_Click(object sender, RoutedEventArgs e)
    {
        if (_lastGpoResults.Count == 0)
        {
            OutputBox.Text = "コピーできる結果がありません。先にシミュレーションを実行してください。";
            return;
        }
        Clipboard.SetText(BuildGpoResultsPlainTable(_lastGpoResults));
        OutputBox.Text = "GPOシミュレーション結果（CSV同等）をクリップボードにコピーしました";
    }

    private void GpoExportCsv_Click(object sender, RoutedEventArgs e)
    {
        if (_lastGpoResults.Count == 0)
        {
            OutputBox.Text = "CSV出力できる結果がありません。先にシミュレーションを実行してください。";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "GPOシミュレーション結果CSV出力",
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            FileName = $"ManageAdTool-GPO-{DateTime.Now:yyyyMMdd-HHmmss}.csv"
        };
        if (dialog.ShowDialog(this) != true) return;

        File.WriteAllText(dialog.FileName, BuildGpoResultsCsv(_lastGpoResults), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        OutputBox.Text = $"GPOシミュレーション結果CSVを出力しました: {dialog.FileName}";
        _auditLogger.Log("GpoSimulationCsvExport", dialog.FileName, _lastGpoResults.Count, success: true);
    }

    private static string BuildGpoResultText(IReadOnlyList<GpoSimulationResult> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"GPOシミュレーション結果 ({results.Count}件)");
        foreach (var r in results)
        {
            sb.AppendLine();
            sb.AppendLine($"GPO名     : {r.GpoName}");
            sb.AppendLine($"GPO ID    : {r.GpoId}");
            sb.AppendLine($"適用対象  : {r.AppliesTo}");
            sb.AppendLine($"リンク先  : {r.LinkedOuDn}");
            sb.AppendLine($"有効      : {(r.LinkEnabled ? "はい" : "いいえ")}");
            sb.AppendLine($"強制適用  : {(r.Enforced ? "はい" : "いいえ")}");
            if (!string.IsNullOrWhiteSpace(r.Remarks))
                sb.AppendLine($"備考      : {r.Remarks}");
        }
        return sb.ToString().TrimEnd();
    }

    private static string BuildGpoResultsPlainTable(IEnumerable<GpoSimulationResult> results)
    {
        var lines = new List<string>
        {
            string.Join("\t", new[] { "GPO名", "GPO ID", "適用対象", "リンク先OU", "有効", "強制適用", "備考" })
        };
        lines.AddRange(results.Select(r => string.Join("\t", new[]
        {
            r.GpoName, r.GpoId, r.AppliesTo, r.LinkedOuDn,
            r.LinkEnabled ? "はい" : "いいえ",
            r.Enforced ? "はい" : "いいえ",
            r.Remarks
        })));
        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildGpoResultsCsv(IEnumerable<GpoSimulationResult> results)
    {
        var lines = new List<string>
        {
            string.Join(",", new[] { "GPO名", "GPO ID", "適用対象", "リンク先OU", "有効", "強制適用", "備考" }.Select(CsvEscape))
        };
        lines.AddRange(results.Select(r => string.Join(",", new[]
        {
            r.GpoName, r.GpoId, r.AppliesTo, r.LinkedOuDn,
            r.LinkEnabled ? "はい" : "いいえ",
            r.Enforced ? "はい" : "いいえ",
            r.Remarks
        }.Select(CsvEscape))));
        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private string BuildOperationSummary()
    {
        if (_opSelectedUser is null) return "(対象ユーザーが未選択です)";

        var sb = new StringBuilder();
        sb.AppendLine("対象ユーザー:");
        sb.AppendLine($"  sAMAccountName : {_opSelectedUser.SamAccountName}");
        sb.AppendLine($"  表示名（現在）  : {_opSelectedUser.DisplayName}");
        sb.AppendLine();

        var newDn = OpNewDisplayNameBox.Text.Trim();
        var newSn = OpNewSurnameBox.Text.Trim();
        var newGn = OpNewGivenNameBox.Text.Trim();
        var newMail = OpNewMailBox.Text.Trim();

        var attrLines = new List<string>();
        if (!string.IsNullOrEmpty(newDn) && newDn != _opSelectedUser.DisplayName)
            attrLines.Add($"  表示名         : {_opSelectedUser.DisplayName} → {newDn}");
        if (!string.IsNullOrEmpty(newSn) && newSn != _opSelectedUser.Surname)
            attrLines.Add($"  姓             : {_opSelectedUser.Surname} → {newSn}");
        if (!string.IsNullOrEmpty(newGn) && newGn != _opSelectedUser.GivenName)
            attrLines.Add($"  名             : {_opSelectedUser.GivenName} → {newGn}");
        if (!string.IsNullOrEmpty(newMail) && newMail != _opSelectedUser.Mail)
            attrLines.Add($"  メールアドレス : {_opSelectedUser.Mail} → {newMail}");

        if (attrLines.Count > 0)
        {
            sb.AppendLine("属性変更予定:");
            foreach (var line in attrLines) sb.AppendLine(line);
        }
        else
        {
            sb.AppendLine("属性変更予定: なし");
        }
        sb.AppendLine();

        sb.AppendLine("グループ変更予定:");
        if (_opGroupAddPlanned.Count > 0)
        {
            sb.AppendLine("  追加:");
            foreach (var g in _opGroupAddPlanned) sb.AppendLine($"  + {g}");
        }
        if (_opGroupRemovePlanned.Count > 0)
        {
            sb.AppendLine("  削除:");
            foreach (var g in _opGroupRemovePlanned) sb.AppendLine($"  - {g}");
        }
        if (_opGroupAddPlanned.Count == 0 && _opGroupRemovePlanned.Count == 0)
            sb.AppendLine("  なし");
        sb.AppendLine();

        sb.AppendLine("確認事項:");
        sb.AppendLine($"  [{(OpCheck1.IsChecked == true ? "x" : " ")}] 対象ユーザーに誤りがない");
        sb.AppendLine($"  [{(OpCheck2.IsChecked == true ? "x" : " ")}] 変更後の属性（表示名・姓・名）に誤りがない");
        sb.AppendLine($"  [{(OpCheck3.IsChecked == true ? "x" : " ")}] メールアドレスに誤りがない");
        sb.AppendLine($"  [{(OpCheck4.IsChecked == true ? "x" : " ")}] 追加グループに誤りがない");
        sb.AppendLine($"  [{(OpCheck5.IsChecked == true ? "x" : " ")}] 削除グループに誤りがない");
        sb.AppendLine($"  [{(OpCheck6.IsChecked == true ? "x" : " ")}] 関係部署の確認が済んでいる");

        sb.AppendLine();
        sb.AppendLine("※ 実際の更新は各タブ（ユーザー編集・グループ編集）から実行してください。");

        return sb.ToString().TrimEnd();
    }

    // ─── ログ確認タブ ─────────────────────────────────────────────────────────

    private IReadOnlyList<LogEntry> _loadedLogEntries = Array.Empty<LogEntry>();
    private IReadOnlyList<LogEntry> _filteredLogEntries = Array.Empty<LogEntry>();

    private string GetLogFilePath(int kindIndex)
    {
        var dir = Path.GetDirectoryName(_policy.LogPath) ?? string.Empty;
        return kindIndex switch
        {
            1 => Path.Combine(dir, "auth.jsonl"),
            2 => Path.Combine(dir, "write-audit.jsonl"),
            _ => _policy.LogPath
        };
    }

    private void LogLoad_Click(object sender, RoutedEventArgs e)
    {
        var path = GetLogFilePath(LogKindComboBox.SelectedIndex);
        LogWarningText.Text = string.Empty;

        if (!File.Exists(path))
        {
            LogWarningText.Text = $"ファイルが存在しません: {path}";
            _loadedLogEntries = Array.Empty<LogEntry>();
            _filteredLogEntries = Array.Empty<LogEntry>();
            LogGrid.ItemsSource = null;
            LogStatusText.Text = "0 件";
            return;
        }

        try
        {
            var entries = LogReader.ReadLog(path, _policy.MaxLogDisplayRows);
            _loadedLogEntries = entries;
            _filteredLogEntries = entries;
            LogGrid.ItemsSource = _filteredLogEntries;
            SelectFirstLogEntryOrClear();
            LogStartDatePicker.SelectedDate = null;
            LogEndDatePicker.SelectedDate = null;
            LogSuccessFilterComboBox.SelectedIndex = 0;
            LogActionFilterBox.Text = string.Empty;
            LogTargetFilterBox.Text = string.Empty;

            var parseErrors = entries.Count(x => x.IsParseError);
            LogStatusText.Text = parseErrors > 0
                ? $"{entries.Count} 件（解析エラー {parseErrors} 件）"
                : $"{entries.Count} 件";

            if (entries.Count >= _policy.MaxLogDisplayRows)
                LogWarningText.Text = $"※ 最新 {_policy.MaxLogDisplayRows} 件のみ表示しています。";
        }
        catch
        {
            LogWarningText.Text = "読み込みに失敗しました。ファイルのアクセス権限またはパスを確認してください。";
        }
    }

    private void LogOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var dir = Path.GetDirectoryName(_policy.LogPath);
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
        {
            LogWarningText.Text = $"ログフォルダが見つかりません: {dir}";
            return;
        }
        try
        {
            System.Diagnostics.Process.Start("explorer.exe", dir);
        }
        catch
        {
            LogWarningText.Text = "エクスプローラーを起動できませんでした。";
        }
    }

    private void LogFilter_Click(object sender, RoutedEventArgs e)
    {
        var start = LogStartDatePicker.SelectedDate;
        var end = LogEndDatePicker.SelectedDate.HasValue
            ? LogEndDatePicker.SelectedDate.Value.AddDays(1)
            : (DateTime?)null;
        var successIdx = LogSuccessFilterComboBox.SelectedIndex;
        var actionKw = LogActionFilterBox.Text.Trim();
        var targetKw = LogTargetFilterBox.Text.Trim();

        var filtered = _loadedLogEntries.Where(x =>
        {
            if (start.HasValue && x.Timestamp.HasValue && x.Timestamp.Value.LocalDateTime < start.Value) return false;
            if (end.HasValue && x.Timestamp.HasValue && x.Timestamp.Value.LocalDateTime >= end.Value) return false;
            if (successIdx == 1 && x.Success != true) return false;
            if (successIdx == 2 && x.Success != false) return false;
            if (!string.IsNullOrEmpty(actionKw) &&
                !x.Action.Contains(actionKw, StringComparison.OrdinalIgnoreCase) &&
                !x.Message.Contains(actionKw, StringComparison.OrdinalIgnoreCase) &&
                !x.RawJson.Contains(actionKw, StringComparison.OrdinalIgnoreCase)) return false;
            if (!string.IsNullOrEmpty(targetKw) &&
                !x.Target.Contains(targetKw, StringComparison.OrdinalIgnoreCase) &&
                !x.RawJson.Contains(targetKw, StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }).ToList();

        _filteredLogEntries = filtered;
        LogGrid.ItemsSource = _filteredLogEntries;
        LogStatusText.Text = $"{filtered.Count} 件（フィルター適用中）";
        SelectFirstLogEntryOrClear();
    }

    private void LogFilterClear_Click(object sender, RoutedEventArgs e)
    {
        LogStartDatePicker.SelectedDate = null;
        LogEndDatePicker.SelectedDate = null;
        LogSuccessFilterComboBox.SelectedIndex = 0;
        LogActionFilterBox.Text = string.Empty;
        LogTargetFilterBox.Text = string.Empty;
        _filteredLogEntries = _loadedLogEntries;
        LogGrid.ItemsSource = _filteredLogEntries;
        LogStatusText.Text = $"{_loadedLogEntries.Count} 件";
        SelectFirstLogEntryOrClear();
    }

    private void SelectFirstLogEntryOrClear()
    {
        if (_filteredLogEntries.Count == 0)
        {
            LogGrid.SelectedItem = null;
            LogDetailBox.Text = string.Empty;
            return;
        }

        LogGrid.SelectedIndex = 0;
        if (_filteredLogEntries[0] is LogEntry entry)
            LogDetailBox.Text = LogReader.FormatAndMaskJson(entry.RawJson);
    }

    private void LogGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (LogGrid.SelectedItem is not LogEntry entry)
        {
            LogDetailBox.Text = string.Empty;
            return;
        }
        LogDetailBox.Text = LogReader.FormatAndMaskJson(entry.RawJson);
    }

    private void LogCopyDetail_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(LogDetailBox.Text)) return;
        Clipboard.SetText(LogDetailBox.Text);
    }

    private void LogExportCsv_Click(object sender, RoutedEventArgs e)
    {
        if (_filteredLogEntries.Count == 0)
        {
            OutputBox.Text = "エクスポートするログがありません。先に読み込みを行ってください。";
            return;
        }

        var dlg = new SaveFileDialog
        {
            Filter = "CSVファイル (*.csv)|*.csv",
            FileName = $"log-export-{DateTime.Now:yyyyMMdd-HHmmss}.csv"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("日時,操作,対象,実行者・編集者,成否,備考,種別");
            foreach (var r in _filteredLogEntries)
            {
                sb.AppendLine(string.Join(",",
                    CsvEscape(r.TimestampDisplay),
                    CsvEscape(r.Action),
                    CsvEscape(r.Target),
                    CsvEscape(r.ExecutorDisplay),
                    CsvEscape(r.SuccessDisplay),
                    CsvEscape(r.DetailSummary),
                    CsvEscape(r.TargetType)));
            }
            File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
            OutputBox.Text = $"CSVを出力しました: {dlg.FileName}";
        }
        catch
        {
            OutputBox.Text = "CSV出力に失敗しました。保存先のアクセス権限またはパスを確認してください。";
        }
    }
}
