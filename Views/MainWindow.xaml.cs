using System.Security.Principal;
using System.Windows;
using ManageAdTool.Models;
using ManageAdTool.Services;

namespace ManageAdTool.Views;

public partial class MainWindow : Window
{
    private readonly AppPolicy _policy = AppPolicyProvider.Load();
    private readonly IAdService _ad;
    private readonly AuditLogService _audit;
    private readonly UserEditPolicyService _policyService = new();
    private readonly UserEditUseCase _useCase;
    private AdUser? _selected;
    private ChangeSet? _pending;
    private bool _canEdit;

    public MainWindow()
    {
        _ad = _policy.ServiceMode switch
        {
            var mode when string.Equals(mode, "DirectoryReadOnly", StringComparison.OrdinalIgnoreCase) => new DirectoryServicesAdReadService(_policy),
            var mode when string.Equals(mode, "DirectoryLimitedWrite", StringComparison.OrdinalIgnoreCase) => new DirectoryServicesAdLimitedWriteService(_policy),
            _ => new InMemoryAdService()
        };
        _audit = new AuditLogService(_policy.LogPath);
        _useCase = new UserEditUseCase(_ad);
        InitializeComponent();
        ApplyEditability(false, "ユーザー未選択");
        if (string.Equals(_policy.ServiceMode, "DirectoryReadOnly", StringComparison.OrdinalIgnoreCase))
        {
            MailBox.IsEnabled = false;
            DepartmentBox.IsEnabled = false;
            TitleBox.IsEnabled = false;
            PreviewButton.IsEnabled = false;
            ExecuteButton.IsEnabled = false;
            EditBlockedReasonText.Text = "DirectoryReadOnly モードのため参照のみ";
        }
    }

    private void Search_Click(object sender, RoutedEventArgs e)
    {
        var keyword = SearchBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(keyword) || keyword.Length <= 1)
        {
            OutputBox.Text = "検索語は2文字以上入力してください";
            SearchResultGrid.ItemsSource = Array.Empty<AdUser>();
            return;
        }

        try
        {
            var results = _ad.SearchUsers(keyword);
            _selected = null;
            _pending = null;
            SearchResultGrid.ItemsSource = results;
            OutputBox.Text = $"検索結果: {results.Count}件";
        }
        catch (Exception ex)
        {
            SearchResultGrid.ItemsSource = Array.Empty<AdUser>();
            OutputBox.Text = $"検索失敗: {ex.Message}";
        }
    }

    private void SearchResultGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        _selected = SearchResultGrid.SelectedItem as AdUser;
        if (_selected is null) return;

        _pending = null;
        MailBox.Text = _selected.Mail;
        DepartmentBox.Text = _selected.Department;
        TitleBox.Text = _selected.Title;
        try
        {
            GroupListBox.Text = string.Join(Environment.NewLine, _ad.GetUserGroups(_selected.SamAccountName));
        }
        catch (Exception ex)
        {
            OutputBox.Text = $"詳細取得失敗: {ex.Message}";
            GroupListBox.Text = string.Empty;
        }

        EvaluateEditability();
    }

    private void EvaluateEditability()
    {
        var result = _policyService.Evaluate(_selected, _policy);
        ApplyEditability(result.canEdit, result.reason);
    }

    private void ApplyEditability(bool canEdit, string reason)
    {
        _canEdit = canEdit;
        var readOnlyMode = string.Equals(_policy.ServiceMode, "DirectoryReadOnly", StringComparison.OrdinalIgnoreCase);
        MailBox.IsEnabled = canEdit && !readOnlyMode;
        DepartmentBox.IsEnabled = canEdit && !readOnlyMode;
        TitleBox.IsEnabled = canEdit && !readOnlyMode;
        PreviewButton.IsEnabled = canEdit && !readOnlyMode;
        ExecuteButton.IsEnabled = canEdit && !readOnlyMode;
        EditBlockedReasonText.Text = reason;
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        if (!_canEdit || _selected is null) return;
        _pending = _useCase.BuildChangeSet(_selected, MailBox.Text.Trim(), DepartmentBox.Text.Trim(), TitleBox.Text.Trim());
        OutputBox.Text = FormatChangePreview(_pending, "属性更新");
    }

    private void Execute_Click(object sender, RoutedEventArgs e)
    {
        if (string.Equals(_policy.ServiceMode, "DirectoryReadOnly", StringComparison.OrdinalIgnoreCase))
        {
            OutputBox.Text = "DirectoryReadOnly モードのため更新は実行できません";
            return;
        }

        EvaluateEditability();
        if (_selected is null)
        {
            OutputBox.Text = "更新不可: ユーザー未選択";
            return;
        }

        if (_pending is null || _pending.Changes.Count == 0)
        {
            OutputBox.Text = "更新不可: 差分確認を実施してください";
            return;
        }

        if (!_canEdit)
        {
            OutputBox.Text = $"更新不可: {EditBlockedReasonText.Text}";
            return;
        }

        var requestedMail = MailBox.Text.Trim();
        var requestedDepartment = DepartmentBox.Text.Trim();
        var requestedTitle = TitleBox.Text.Trim();
        if (!PendingMatchesRequestedValues(_pending, _selected, requestedMail, requestedDepartment, requestedTitle))
        {
            OutputBox.Text = "更新不可: 差分確認後に入力値が変更されています。再度、差分確認を実施してください";
            return;
        }

        AdUser? current;
        ChangeSet currentChangeSet;
        try
        {
            current = _ad.GetUser(_selected.SamAccountName);
            if (current is null)
            {
                OutputBox.Text = "更新不可: 対象ユーザーが存在しません";
                return;
            }

            var policyResult = _policyService.Evaluate(current, _policy);
            if (!policyResult.canEdit)
            {
                OutputBox.Text = $"更新不可: {policyResult.reason}";
                return;
            }

            currentChangeSet = _useCase.BuildChangeSet(current, requestedMail, requestedDepartment, requestedTitle);
            if (currentChangeSet.Changes.Count == 0)
            {
                OutputBox.Text = "差分なし（更新不要）";
                return;
            }
        }
        catch (Exception ex)
        {
            OutputBox.Text = $"更新前確認失敗: {ex.Message}";
            return;
        }

        var confirm = MessageBox.Show("表示中の差分を更新します。実行しますか？", "確認", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        var executor = WindowsIdentity.GetCurrent().Name;
        var opId = Guid.NewGuid().ToString("N");
        try
        {
            _useCase.UpdateAttributes(current.SamAccountName, requestedMail, requestedDepartment, requestedTitle);
            var updated = _ad.GetUser(current.SamAccountName);
            _audit.WriteExtended(opId, executor, Environment.MachineName, current.DistinguishedName, current.SamAccountName, Environment.UserDomainName, "1.0.0", "UpdateUserAttributes", currentChangeSet.Changes, success: true, serviceMode: _policy.ServiceMode);
            _selected = updated ?? current;
            _pending = null;
            OutputBox.Text = FormatUpdateSuccess(updated ?? current);
        }
        catch (Exception ex)
        {
            _audit.WriteExtended(opId, executor, Environment.MachineName, current.DistinguishedName, current.SamAccountName, Environment.UserDomainName, "1.0.0", "UpdateUserAttributes", currentChangeSet.Changes, success: false, error: ex.Message, serviceMode: _policy.ServiceMode);
            OutputBox.Text = $"更新失敗: {ex.Message}";
        }
    }

    private static bool PendingMatchesRequestedValues(ChangeSet pending, AdUser selected, string mail, string department, string title)
    {
        var expectedMail = GetExpectedValue(pending, "Mail", selected.Mail);
        var expectedDepartment = GetExpectedValue(pending, "Department", selected.Department);
        var expectedTitle = GetExpectedValue(pending, "Title", selected.Title);

        return string.Equals(expectedMail, mail, StringComparison.Ordinal)
            && string.Equals(expectedDepartment, department, StringComparison.Ordinal)
            && string.Equals(expectedTitle, title, StringComparison.Ordinal);
    }

    private static string GetExpectedValue(ChangeSet pending, string field, string currentValue)
        => pending.Changes.FirstOrDefault(c => string.Equals(c.Field, field, StringComparison.Ordinal))?.After ?? currentValue;

    private static string FormatUpdateSuccess(AdUser user)
        => string.Join(Environment.NewLine, new[]
        {
            "更新成功",
            $"対象: {user.SamAccountName}",
            $"mail: {user.Mail}",
            $"department: {user.Department}",
            $"title: {user.Title}"
        });

    private static string FormatChangePreview(ChangeSet cs, string operation)
    {
        if (cs.Changes.Count == 0) return "差分なし（更新不要）";
        var lines = new List<string> { $"対象: {cs.TargetSamAccountName}", $"実行予定処理: {operation}" };
        lines.AddRange(cs.Changes.Select(c => $"- {c.Field}: '{c.Before}' => '{c.After}'"));
        return string.Join(Environment.NewLine, lines);
    }

    private void CopyOutput_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(OutputBox.Text)) Clipboard.SetText(OutputBox.Text);
    }
}
