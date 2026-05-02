using System.Security.Principal;
using System.Text;
using System.Windows;
using ManageAdTool.Models;
using ManageAdTool.Services;

namespace ManageAdTool.Views;

public partial class MainWindow : Window
{
    private readonly IAdService _ad = new InMemoryAdService();
    private readonly AuditLogService _audit = new(@"C:\ProgramData\ManageAdTool\logs\audit.jsonl");
    private AdUser? _selected;
    private ChangeSet? _pending;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void Search_Click(object sender, RoutedEventArgs e)
    {
        SearchResultGrid.ItemsSource = _ad.SearchUsers(SearchBox.Text.Trim());
        OutputBox.Text = "検索を実行しました。";
    }

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
        if (_selected is null)
        {
            MessageBox.Show("対象ユーザーを選択してください。");
            return;
        }

        _pending = _ad.BuildChangeSet(_selected, MailBox.Text.Trim(), DepartmentBox.Text.Trim(), TitleBox.Text.Trim());
        if (_pending.Changes.Count == 0)
        {
            OutputBox.Text = "差分なし（更新不要）";
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"対象ユーザー: {_pending.TargetSamAccountName}");
        sb.AppendLine("実行予定処理: 属性更新");
        foreach (var c in _pending.Changes)
            sb.AppendLine($"- {c.Field}: '{c.Before}' => '{c.After}'");
        OutputBox.Text = sb.ToString();
    }

    private void Execute_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null || _pending is null || _pending.Changes.Count == 0)
        {
            MessageBox.Show("先に差分確認を実行してください。");
            return;
        }

        var confirm = MessageBox.Show("表示中の差分を更新します。実行しますか？", "確認", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        var executor = WindowsIdentity.GetCurrent().Name;
        try
        {
            _ad.UpdateAttributes(_selected.SamAccountName, MailBox.Text.Trim(), DepartmentBox.Text.Trim(), TitleBox.Text.Trim());
            _audit.Write(executor, _pending, success: true);
            OutputBox.Text += "\n\n更新成功";
        }
        catch (Exception ex)
        {
            _audit.Write(executor, _pending, success: false, error: ex.Message);
            OutputBox.Text += $"\n\n更新失敗: {ex.Message}";
        }
    }
}
