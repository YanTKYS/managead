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
    private readonly AppPolicy _policy = AppPolicyProvider.Load();
    private readonly IAdService _ad;
    private readonly IAdUserAttributeWriteService? _writeService;
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

    private static readonly string Executor =
        $"{Environment.UserDomainName}\\{Environment.UserName}";

    public MainWindow()
    {
        var isReadOnly = string.Equals(_policy.ServiceMode, "DirectoryReadOnly", StringComparison.OrdinalIgnoreCase);
        _ad = isReadOnly ? new DirectoryServicesAdReadService(_policy) : new InMemoryAdService();
        _writeService = isReadOnly ? new DirectoryServicesAdUserAttributeWriteService() : null;
        _useCase = new UserAttributeCompareUseCase(_ad);
        _auditLogger = new ReferenceAuditLogger(_policy.LogPath);
        _authAuditLogger = new AuthAuditLogger(_policy.LogPath);
        _writeAuditLogger = new WriteAuditLogger(_policy.LogPath);
        _logPathWritable = TryCheckLogPathWritable(_policy.LogPath);
        _vm = new MainWindowViewModel(_policy);
        _sessionTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _sessionTimer.Tick += SessionTimer_Tick;
        _sessionTimer.Start();
        InitializeComponent();
        DataContext = _vm;
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

        MailBox.Text = _selected.Mail;
        DepartmentBox.Text = _selected.Department;
        TitleBox.Text = _selected.Title;
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
            GroupSearchResultGrid.ItemsSource = groups;
            GroupMemberGrid.ItemsSource = Array.Empty<AdUser>();
            var groupExceeded = groups.Count >= _policy.MaxSearchResults;
            OutputBox.Text = groupExceeded
                ? $"グループ検索結果: {groups.Count}件（上限 {_policy.MaxSearchResults} 件に達しました。検索条件を絞り込んでください）"
                : $"グループ検索結果: {groups.Count}件";
            _auditLogger.Log("GroupSearch", keyword, groups.Count, success: true);
        }
        catch (Exception ex)
        {
            ClearGroupResults();
            OutputBox.Text = $"グループ検索に失敗しました。ネットワーク接続またはAD設定を確認してください。";
            _auditLogger.Log("GroupSearch", keyword, 0, success: false, FormatErrorForLog(ex));
        }
    }

    private void GroupSearchResultGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (GroupSearchResultGrid.SelectedItem is not AdGroup group) return;

        try
        {
            var members = _ad.GetGroupMembers(group.DistinguishedName)
                .OrderBy(x => x.SamAccountName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            GroupMemberGrid.ItemsSource = members;
            var memberExceeded = members.Count >= _policy.MaxSearchResults;
            OutputBox.Text = memberExceeded
                ? $"グループメンバー表示: {group.Name} / {members.Count}件（上限 {_policy.MaxSearchResults} 件に達しました。実際のメンバー数が多い可能性があります）"
                : $"グループメンバー表示: {group.Name} / {members.Count}件";
            _auditLogger.Log("GroupMembers", group.Name, members.Count, success: true);
        }
        catch (Exception ex)
        {
            GroupMemberGrid.ItemsSource = Array.Empty<AdUser>();
            OutputBox.Text = $"グループメンバーの取得に失敗しました。ネットワーク接続を確認してください。";
            _auditLogger.Log("GroupMembers", group.Name, 0, success: false, FormatErrorForLog(ex));
        }
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
        _pending = _useCase.BuildChangeSet(_selected, MailBox.Text.Trim(), DepartmentBox.Text.Trim(), TitleBox.Text.Trim());
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

        var changeSet = _useCase.BuildChangeSet(_selected, MailBox.Text.Trim(), DepartmentBox.Text.Trim(), TitleBox.Text.Trim());

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
                var adValue = change.Field switch
                {
                    "Mail" => currentUser.Mail,
                    "Department" => currentUser.Department,
                    "Title" => currentUser.Title,
                    _ => null
                };
                if (!string.Equals(adValue, change.Before, StringComparison.Ordinal))
                {
                    OutputBox.Text = $"AD上の値が変更されているため更新を中止しました。\n再度「差分確認」から実行してください。\n（{change.Field}: AD現在値「{adValue}」/ 確認時点「{change.Before}」）";
                    LogWriteFailure(changeSet, currentUser.DistinguishedName, authResult.ResolvedUser,
                        $"AD値不一致 field={change.Field}", true, false);
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
                    ["department"] = verifiedUser.Department,
                    ["title"] = verifiedUser.Title
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
            MailBox.Text = _selected.Mail;
            DepartmentBox.Text = _selected.Department;
            TitleBox.Text = _selected.Title;
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
                verifiedVal = change.Field switch
                {
                    "Mail" => verifiedUser.Mail,
                    "Department" => verifiedUser.Department,
                    "Title" => verifiedUser.Title,
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
            "戻し候補メモ (ManageAdTool v0.4.1)",
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
            Department = DepartmentFilterBox.Text.Trim(),
            HasMail = MailFilterBox.SelectedIndex switch { 1 => true, 2 => false, _ => null },
            IncludeDisabled = IncludeDisabledUsersBox.IsChecked == true
        };

    private void ClearSearchResults()
    {
        _lastSearchResults = Array.Empty<AdUser>();
        SearchResultGrid.ItemsSource = Array.Empty<AdUser>();
        UserDetailBox.Text = string.Empty;
        GroupListBox.Text = string.Empty;
    }

    private void ClearGroupResults()
    {
        GroupSearchResultGrid.ItemsSource = Array.Empty<AdGroup>();
        GroupMemberGrid.ItemsSource = Array.Empty<AdUser>();
    }

    private string FormatUserDetails(AdUser user)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["SamAccountName"] = user.SamAccountName,
            ["DisplayName"] = user.DisplayName,
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
                "SamAccountName", "DisplayName", "Name", "Mail", "Department", "Title",
                "Enabled", "UserAccountControl", "LastLogonTimestamp", "AccountExpires", "DistinguishedName"
            }.Select(CsvEscape))
        };

        lines.AddRange(users.Select(user => string.Join(",", new[]
        {
            user.SamAccountName, user.DisplayName, user.Name, user.Mail, user.Department, user.Title,
            FormatBool(user.Enabled), FormatNullable(user.UserAccountControl),
            FormatDateTime(user.LastLogonAt), FormatDateTime(user.AccountExpiresAt), user.DistinguishedName
        }.Select(CsvEscape))));

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static string FormatChangePreview(ChangeSet cs, string operation)
    {
        if (cs.Changes.Count == 0) return "差分なし（更新不要）";
        var lines = new List<string> { $"対象: {cs.TargetSamAccountName}（{cs.TargetDisplayName}）", $"操作: {operation}" };
        lines.AddRange(cs.Changes.Select(c => $"- {c.Field}: 「{c.Before}」→「{c.After}」"));
        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatCriteria(AdUserSearchCriteria criteria)
    {
        var mail = criteria.HasMail switch { true => "あり", false => "なし", _ => "指定なし" };
        return $"keyword={criteria.Keyword}; department={criteria.Department}; mail={mail}; includeDisabled={criteria.IncludeDisabled}";
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
}
