using System.IO;
using System.Text;
using System.Windows;
using ManageAdTool.Models;
using ManageAdTool.Services;
using Microsoft.Win32;

namespace ManageAdTool.Views;

public partial class MainWindow : Window
{
    private readonly AppPolicy _policy = AppPolicyProvider.Load();
    private readonly IAdService _ad;
    private readonly UserEditPolicyService _policyService = new();
    private readonly UserEditUseCase _useCase;
    private readonly ReferenceAuditLogger _auditLogger;
    private readonly MainWindowViewModel _vm;
    private IReadOnlyList<AdUser> _lastSearchResults = Array.Empty<AdUser>();
    private AdUser? _selected;
    private ChangeSet? _pending;

    public MainWindow()
    {
        _ad = string.Equals(_policy.ServiceMode, "DirectoryReadOnly", StringComparison.OrdinalIgnoreCase)
            ? new DirectoryServicesAdReadService(_policy)
            : new InMemoryAdService();
        _useCase = new UserEditUseCase(_ad);
        _auditLogger = new ReferenceAuditLogger(_policy.LogPath);
        _vm = new MainWindowViewModel(_policy);
        InitializeComponent();
        DataContext = _vm;
        ApplyEditability(false, _vm.IsReadOnlyMode ? _vm.ReadOnlyModeLabel : "ユーザー未選択");
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
            OutputBox.Text = $"検索失敗: {ex.Message}";
            _auditLogger.Log("UserSearch", FormatCriteria(criteria), 0, success: false, ex.Message);
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
            OutputBox.Text = $"詳細取得失敗: {ex.Message}";
            GroupListBox.Text = string.Empty;
            _auditLogger.Log("UserDetail", _selected.SamAccountName, 0, success: false, ex.Message);
        }

        EvaluateEditability();
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
            OutputBox.Text = $"グループ検索結果: {groups.Count}件";
            _auditLogger.Log("GroupSearch", keyword, groups.Count, success: true);
        }
        catch (Exception ex)
        {
            ClearGroupResults();
            OutputBox.Text = $"グループ検索失敗: {ex.Message}";
            _auditLogger.Log("GroupSearch", keyword, 0, success: false, ex.Message);
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
            OutputBox.Text = $"グループメンバー表示: {group.Name} / {members.Count}件";
            _auditLogger.Log("GroupMembers", group.Name, members.Count, success: true);
        }
        catch (Exception ex)
        {
            GroupMemberGrid.ItemsSource = Array.Empty<AdUser>();
            OutputBox.Text = $"グループメンバー取得失敗: {ex.Message}";
            _auditLogger.Log("GroupMembers", group.Name, 0, success: false, ex.Message);
        }
    }

    private void EvaluateEditability()
    {
        var result = _policyService.Evaluate(_selected, _policy);
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
        OutputBox.Text = FormatChangePreview(_pending, "属性比較");
    }

    private void Execute_Click(object sender, RoutedEventArgs e)
    {
        OutputBox.Text = "参照専用ツールのため、AD更新は実行できません";
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
            HasMail = MailFilterBox.SelectedIndex switch
            {
                1 => true,
                2 => false,
                _ => null
            },
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
                "SamAccountName",
                "DisplayName",
                "Name",
                "Mail",
                "Department",
                "Title",
                "Enabled",
                "UserAccountControl",
                "LastLogonTimestamp",
                "AccountExpires",
                "DistinguishedName"
            }.Select(CsvEscape))
        };

        lines.AddRange(users.Select(user => string.Join(",", new[]
        {
            user.SamAccountName,
            user.DisplayName,
            user.Name,
            user.Mail,
            user.Department,
            user.Title,
            FormatBool(user.Enabled),
            FormatNullable(user.UserAccountControl),
            FormatDateTime(user.LastLogonAt),
            FormatDateTime(user.AccountExpiresAt),
            user.DistinguishedName
        }.Select(CsvEscape))));

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static string FormatChangePreview(ChangeSet cs, string operation)
    {
        if (cs.Changes.Count == 0) return "差分なし（更新不要）";
        var lines = new List<string> { $"対象: {cs.TargetSamAccountName}", $"実行予定処理: {operation}" };
        lines.AddRange(cs.Changes.Select(c => $"- {c.Field}: '{c.Before}' => '{c.After}'"));
        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatCriteria(AdUserSearchCriteria criteria)
    {
        var mail = criteria.HasMail switch
        {
            true => "あり",
            false => "なし",
            _ => "指定なし"
        };
        return $"keyword={criteria.Keyword}; department={criteria.Department}; mail={mail}; includeDisabled={criteria.IncludeDisabled}";
    }

    private static string FormatDateTime(DateTimeOffset? value)
        => value.HasValue ? value.Value.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss") : "(未設定)";

    private static string FormatNullable(int? value)
        => value.HasValue ? value.Value.ToString() : "(未取得)";

    private static string FormatBool(bool value)
        => value ? "True" : "False";

    private static string CsvEscape(string value)
        => $"\"{value.Replace("\"", "\"\"")}\"";
}
