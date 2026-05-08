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
    private IReadOnlyList<AdUser> _lastSearchResults = Array.Empty<AdUser>();
    private AdUser? _selected;
    private ChangeSet? _pending;
    private bool _canEdit;

    public MainWindow()
    {
        _ad = string.Equals(_policy.ServiceMode, "DirectoryReadOnly", StringComparison.OrdinalIgnoreCase)
            ? new DirectoryServicesAdReadService(_policy)
            : new InMemoryAdService();
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
            ClearSearchResults();
            return;
        }

        try
        {
            var results = _ad.SearchUsers(keyword);
            _selected = null;
            _pending = null;
            _lastSearchResults = results;
            SearchResultGrid.ItemsSource = results;
            UserDetailBox.Text = string.Empty;
            GroupListBox.Text = string.Empty;
            OutputBox.Text = $"検索結果: {results.Count}件";
        }
        catch (Exception ex)
        {
            ClearSearchResults();
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
        UserDetailBox.Text = FormatUserDetails(_selected);
        try
        {
            var groups = _ad.GetUserGroups(_selected.SamAccountName)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
            GroupListBox.Text = string.Join(Environment.NewLine, groups);
            OutputBox.Text = $"詳細表示: {_selected.SamAccountName} / 所属グループ {groups.Count}件";
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
        ExecuteButton.IsEnabled = false;
        EditBlockedReasonText.Text = reason;
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        if (!_canEdit || _selected is null) return;
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
    }

    private void CopyGroups_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(GroupListBox.Text)) Clipboard.SetText(GroupListBox.Text);
    }

    private void CopyOutput_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(OutputBox.Text)) Clipboard.SetText(OutputBox.Text);
    }

    private void ClearSearchResults()
    {
        _lastSearchResults = Array.Empty<AdUser>();
        SearchResultGrid.ItemsSource = Array.Empty<AdUser>();
        UserDetailBox.Text = string.Empty;
        GroupListBox.Text = string.Empty;
    }

    private static string FormatUserDetails(AdUser user)
        => string.Join(Environment.NewLine, new[]
        {
            $"SamAccountName: {user.SamAccountName}",
            $"DisplayName: {user.DisplayName}",
            $"Name: {user.Name}",
            $"DistinguishedName: {user.DistinguishedName}",
            $"Enabled: {FormatBool(user.Enabled)}",
            $"userAccountControl: {FormatNullable(user.UserAccountControl)}",
            $"lastLogonTimestamp: {FormatDateTime(user.LastLogonAt)}",
            $"accountExpires: {FormatDateTime(user.AccountExpiresAt)}"
        });

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

    private static string FormatDateTime(DateTimeOffset? value)
        => value.HasValue ? value.Value.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss") : "(未設定)";

    private static string FormatNullable(int? value)
        => value.HasValue ? value.Value.ToString() : "(未取得)";

    private static string FormatBool(bool value)
        => value ? "True" : "False";

    private static string CsvEscape(string value)
        => $"\"{value.Replace("\"", "\"\"")}\"";
}
