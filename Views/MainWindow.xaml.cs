using System.Security.Principal;
using System.Text;
using System.Windows;
using ManageAdTool.Models;
using ManageAdTool.Services;

namespace ManageAdTool.Views;

public partial class MainWindow : Window
{
    private readonly IAdService _ad = new InMemoryAdService();
    private AppPolicy _policy = AppPolicyProvider.Load();
    private readonly AuditLogService _audit;
    private AdUser? _selected;
    private GpoPolicy? _selectedGpo;
    private ChangeSet? _pending;
    private ChangeSet? _pendingGroup;
    private ChangeSet? _pendingGpo;
    private readonly HashSet<string> _groupsToAdd = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _groupsToRemove = new(StringComparer.OrdinalIgnoreCase);

    public MainWindow()
    {
        _audit = new AuditLogService(_policy.LogPath);
        InitializeComponent();
    }


    private void BootstrapConfig_Click(object sender, RoutedEventArgs e)
    {
        _policy = AppSettingsBootstrapper.MergeFromCurrentEnvironment();
        OutputBox.Text = "appsettings.json に現在のユーザー/PC/ドメイン情報を取り込みました。";
    }

    private void Search_Click(object sender, RoutedEventArgs e) => SearchResultGrid.ItemsSource = _ad.SearchUsers(SearchBox.Text.Trim());

    private void SearchResultGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        _selected = SearchResultGrid.SelectedItem as AdUser;
        if (_selected is null) return;
        MailBox.Text = _selected.Mail;
        DepartmentBox.Text = _selected.Department;
        TitleBox.Text = _selected.Title;
        var lastPc = string.IsNullOrWhiteSpace(_selected.LastLogonComputer) ? null : _ad.GetComputer(_selected.LastLogonComputer);
        var pcInfo = lastPc is null ? _selected.LastLogonComputer : $"{lastPc.Name} / {lastPc.DnsHostName} / {lastPc.OperatingSystem}";
        OutputBox.Text = $"対象: {_selected.SamAccountName}\n氏名: {_selected.Name}\nDN: {_selected.DistinguishedName}\n最終ログオン日時(UTC): {_selected.LastLogonAt:yyyy-MM-dd HH:mm:ss}\n最終ログオンPC: {pcInfo}";
        GroupListBox.Text = string.Join(Environment.NewLine, _ad.GetUserGroups(_selected.SamAccountName));
        _groupsToAdd.Clear(); _groupsToRemove.Clear();
    }



    private void LoadExpiredUsers_Click(object sender, RoutedEventArgs e)
    {
        ExpiredUsersGrid.ItemsSource = _ad.GetExpiredUsers(DateTimeOffset.UtcNow);
    }

    private void ExtendSelectedUsers_Click(object sender, RoutedEventArgs e)
    {
        var selected = ExpiredUsersGrid.SelectedItems.Cast<AdUser>().ToList();
        if (selected.Count == 0) return;
        if (!DateTimeOffset.TryParseExact(ExtendDateBox.Text.Trim(), "yyyy/MM/dd", null, System.Globalization.DateTimeStyles.None, out var date))
        {
            MessageBox.Show("日付は yyyy/MM/dd で入力してください。");
            return;
        }
        _ad.ExtendAccountExpiration(selected.Select(x => x.SamAccountName), date);
        ExpiredUsersGrid.ItemsSource = _ad.GetExpiredUsers(DateTimeOffset.UtcNow);
        OutputBox.Text = $"{selected.Count} 件の有効期限を {date:yyyy/MM/dd} へ延長しました。";
    }

    private void DisableSelectedUsers_Click(object sender, RoutedEventArgs e)
    {
        var selected = ExpiredUsersGrid.SelectedItems.Cast<AdUser>().ToList();
        if (selected.Count == 0) return;
        var ok = MessageBox.Show($"{selected.Count} 件を無効化します。よろしいですか？", "注意", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (ok != MessageBoxResult.Yes) return;

        _ad.DisableUsers(selected.Select(x => x.SamAccountName));
        ExpiredUsersGrid.ItemsSource = _ad.GetExpiredUsers(DateTimeOffset.UtcNow);
        OutputBox.Text = $"{selected.Count} 件を無効化しました。";
    }


    private void LoadInactiveUsers_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(UserInactiveDaysBox.Text.Trim(), out var days)) return;
        InactiveUsersGrid.ItemsSource = _ad.GetUsersNotLoggedInForDays(days, DateTimeOffset.UtcNow);
    }

    private void DisableInactiveUsers_Click(object sender, RoutedEventArgs e)
    {
        var selected = InactiveUsersGrid.SelectedItems.Cast<AdUser>().ToList();
        if (selected.Count == 0) return;
        var ok = MessageBox.Show($"{selected.Count} 件のユーザを無効化します。よろしいですか？", "注意", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (ok != MessageBoxResult.Yes) return;
        _ad.DisableUsers(selected.Select(x => x.SamAccountName));
        OutputBox.Text = $"{selected.Count} 件のユーザを無効化しました。";
    }

    private void LoadInactiveComputers_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(ComputerInactiveDaysBox.Text.Trim(), out var days)) return;
        InactiveComputersGrid.ItemsSource = _ad.GetComputersNotBootedForDays(days, DateTimeOffset.UtcNow);
    }

    private void DisableInactiveComputers_Click(object sender, RoutedEventArgs e)
    {
        var selected = InactiveComputersGrid.SelectedItems.Cast<AdComputer>().ToList();
        if (selected.Count == 0) return;
        var ok = MessageBox.Show($"{selected.Count} 台の端末を無効化します。よろしいですか？", "注意", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (ok != MessageBoxResult.Yes) return;
        _ad.DisableComputers(selected.Select(x => x.Name));
        OutputBox.Text = $"{selected.Count} 台の端末を無効化しました。";
    }

    private void StageAddGroup_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(AddGroupBox.Text)) return;
        _groupsToAdd.Add(AddGroupBox.Text.Trim());
        AddGroupBox.Clear();
        OutputBox.Text += $"\n追加候補: {string.Join(", ", _groupsToAdd)}";
    }

    private void StageRemoveGroup_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(RemoveGroupBox.Text)) return;
        _groupsToRemove.Add(RemoveGroupBox.Text.Trim());
        RemoveGroupBox.Clear();
        OutputBox.Text += $"\n削除候補: {string.Join(", ", _groupsToRemove)}";
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        _pending = _ad.BuildChangeSet(_selected, MailBox.Text.Trim(), DepartmentBox.Text.Trim(), TitleBox.Text.Trim());
        _pendingGroup = _ad.BuildGroupMembershipChangeSet(_selected.SamAccountName, _groupsToAdd, _groupsToRemove);
        var text = FormatChangePreview(_pending, "属性更新");
        if (_pendingGroup.Changes.Count > 0) text += "\n" + FormatChangePreview(_pendingGroup, "グループ追加/削除");
        OutputBox.Text = text;
    }

    private void Execute_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null || _pending is null) return;
        var hasAttr = _pending.Changes.Count > 0;
        var hasGroup = _pendingGroup is not null && _pendingGroup.Changes.Count > 0;
        if (!hasAttr && !hasGroup) return;
        if (!IsAllowedDn(_selected.DistinguishedName))
        {
            OutputBox.Text += "\n\n許可されていないOUのため更新を拒否しました。";
            return;
        }
        if (MessageBox.Show("表示中の差分を更新します。実行しますか？", "確認", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

        var executor = WindowsIdentity.GetCurrent().Name;
        try
        {
            if (hasAttr) _ad.UpdateAttributes(_selected.SamAccountName, MailBox.Text.Trim(), DepartmentBox.Text.Trim(), TitleBox.Text.Trim());
            if (hasGroup) _ad.UpdateUserGroups(_selected.SamAccountName, _groupsToAdd, _groupsToRemove);
            _audit.Write(executor, _pending, true);
            if (hasGroup && _pendingGroup is not null) _audit.Write(executor, _pendingGroup, true);
            GroupListBox.Text = string.Join(Environment.NewLine, _ad.GetUserGroups(_selected.SamAccountName));
            _groupsToAdd.Clear(); _groupsToRemove.Clear();
            OutputBox.Text += "\n\n更新成功";
        }
        catch (Exception ex)
        {
            _audit.Write(executor, _pending, false, ex.Message);
            OutputBox.Text += $"\n\n更新失敗: {ex.Message}";
        }
    }

    private void SearchComputer_Click(object sender, RoutedEventArgs e) => ComputerResultGrid.ItemsSource = _ad.SearchComputers(ComputerSearchBox.Text.Trim());

    private void SearchGpo_Click(object sender, RoutedEventArgs e) => GpoResultGrid.ItemsSource = _ad.SearchGpos(GpoSearchBox.Text.Trim());

    private void ComputerResultGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        var selected = ComputerResultGrid.SelectedItem as AdComputer;
        if (selected is null) return;
        ComputerDetailBox.Text = $"対象PC: {selected.Name}\n最終起動日時(UTC): {selected.LastBootAt:yyyy-MM-dd HH:mm:ss}\n最終ログインユーザ: {selected.LastLoggedOnUser}";
    }


    private void LoadGroupGpoStatus_Click(object sender, RoutedEventArgs e)
    {
        var group = GroupForGpoBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(group)) return;
        GroupGpoStatusGrid.ItemsSource = _ad.GetAppliedGposForGroup(group);
    }

    private void ExportGroupGpoCsv_Click(object sender, RoutedEventArgs e)
    {
        var group = GroupForGpoBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(group)) return;
        var data = _ad.GetAppliedGposForGroup(group);
        var path = Path.Combine(Environment.CurrentDirectory, $"group-gpo-{group}.csv");
        using var sw = new StreamWriter(path, false, Encoding.UTF8);
        sw.WriteLine("GroupName,GpoDisplayName,Enforced,LinkTarget");
        foreach (var r in data)
        {
            sw.WriteLine($""{r.GroupName}","{r.GpoDisplayName}","{r.Enforced}","{r.LinkTarget}"");
        }
        GpoOutputBox.Text = $"CSV出力完了: {path}";
    }


    private void LoadTargetGpoStatus_Click(object sender, RoutedEventArgs e)
    {
        var user = TargetUserBox.Text.Trim();
        var computer = TargetComputerBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(computer)) return;
        TargetGpoStatusGrid.ItemsSource = _ad.GetAppliedGposForUserAndComputer(user, computer);
    }

    private void ExportTargetGpoCsv_Click(object sender, RoutedEventArgs e)
    {
        var user = TargetUserBox.Text.Trim();
        var computer = TargetComputerBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(computer)) return;
        var data = _ad.GetAppliedGposForUserAndComputer(user, computer);
        var path = Path.Combine(Environment.CurrentDirectory, $"target-gpo-{user}-{computer}.csv");
        using var sw = new StreamWriter(path, false, Encoding.UTF8);
        sw.WriteLine("UserSamAccountName,ComputerName,GpoDisplayName,Enforced,Scope");
        foreach (var r in data)
        {
            sw.WriteLine($""{r.UserSamAccountName}","{r.ComputerName}","{r.GpoDisplayName}","{r.Enforced}","{r.Scope}"");
        }
        GpoOutputBox.Text = $"CSV出力完了: {path}";
    }

    private void GpoResultGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        _selectedGpo = GpoResultGrid.SelectedItem as GpoPolicy;
        if (_selectedGpo is null) return;
        GpoDescriptionBox.Text = _selectedGpo.Description;
        UserSettingEnabledBox.IsChecked = _selectedGpo.UserSettingsEnabled;
        ComputerSettingEnabledBox.IsChecked = _selectedGpo.ComputerSettingsEnabled;
    }

    private void PreviewGpo_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedGpo is null) return;
        _pendingGpo = _ad.BuildGpoChangeSet(_selectedGpo, GpoDescriptionBox.Text.Trim(), UserSettingEnabledBox.IsChecked ?? false, ComputerSettingEnabledBox.IsChecked ?? false);
        GpoOutputBox.Text = FormatChangePreview(_pendingGpo, "GPO更新");
    }

    private void ExecuteGpo_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedGpo is null || _pendingGpo is null || _pendingGpo.Changes.Count == 0) return;
        if (MessageBox.Show("表示中のGPO差分を更新します。実行しますか？", "確認", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

        var executor = WindowsIdentity.GetCurrent().Name;
        try
        {
            _ad.UpdateGpo(_selectedGpo.Id, GpoDescriptionBox.Text.Trim(), UserSettingEnabledBox.IsChecked ?? false, ComputerSettingEnabledBox.IsChecked ?? false);
            _audit.Write(executor, _pendingGpo, true);
            GpoOutputBox.Text += "\n\n更新成功";
        }
        catch (Exception ex)
        {
            _audit.Write(executor, _pendingGpo, false, ex.Message);
            GpoOutputBox.Text += $"\n\n更新失敗: {ex.Message}";
        }
    }

    private bool IsAllowedDn(string dn)
    {
        if (_policy.AllowedTargetOuDns.Count == 0) return true;
        return _policy.AllowedTargetOuDns.Any(ou => dn.Contains(ou, StringComparison.OrdinalIgnoreCase));
    }

    private static string FormatChangePreview(ChangeSet cs, string operation)
    {
        if (cs.Changes.Count == 0) return "差分なし（更新不要）";
        var sb = new StringBuilder();
        sb.AppendLine($"対象: {cs.TargetSamAccountName}");
        sb.AppendLine($"実行予定処理: {operation}");
        foreach (var c in cs.Changes) sb.AppendLine($"- {c.Field}: '{c.Before}' => '{c.After}'");
        return sb.ToString();
    }
}
