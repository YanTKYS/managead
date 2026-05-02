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
    private ChangeSet? _pendingGpo;

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
        OutputBox.Text = $"対象: {_selected.SamAccountName}\nDN: {_selected.DistinguishedName}";
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        _pending = _ad.BuildChangeSet(_selected, MailBox.Text.Trim(), DepartmentBox.Text.Trim(), TitleBox.Text.Trim());
        OutputBox.Text = FormatChangePreview(_pending, "属性更新");
    }

    private void Execute_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null || _pending is null || _pending.Changes.Count == 0) return;
        if (!IsAllowedDn(_selected.DistinguishedName))
        {
            OutputBox.Text += "\n\n許可されていないOUのため更新を拒否しました。";
            return;
        }
        if (MessageBox.Show("表示中の差分を更新します。実行しますか？", "確認", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

        var executor = WindowsIdentity.GetCurrent().Name;
        try
        {
            _ad.UpdateAttributes(_selected.SamAccountName, MailBox.Text.Trim(), DepartmentBox.Text.Trim(), TitleBox.Text.Trim());
            _audit.Write(executor, _pending, true);
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
