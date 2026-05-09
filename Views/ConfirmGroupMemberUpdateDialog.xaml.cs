using System.Windows;

namespace ManageAdTool.Views;

public partial class ConfirmGroupMemberUpdateDialog : Window
{
    public ConfirmGroupMemberUpdateDialog(
        string groupName, string groupDn,
        string editorUser, string executor, string machineName,
        IReadOnlyList<string> addDisplayTexts, IReadOnlyList<string> removeDisplayTexts)
    {
        InitializeComponent();
        GroupNameText.Text = groupName;
        DnText.Text = groupDn;
        MachineExecutorText.Text = $"{machineName} / {executor}";
        EditorText.Text = editorUser;

        AddHeaderText.Text = $"追加するユーザー: {addDisplayTexts.Count}名";
        AddList.ItemsSource = addDisplayTexts;
        RemoveHeaderText.Text = $"削除するユーザー: {removeDisplayTexts.Count}名";
        RemoveList.ItemsSource = removeDisplayTexts;
    }

    private void OK_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
