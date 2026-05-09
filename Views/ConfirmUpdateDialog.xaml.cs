using System.Windows;
using ManageAdTool.Models;

namespace ManageAdTool.Views;

public partial class ConfirmUpdateDialog : Window
{
    public ConfirmUpdateDialog(ChangeSet changeSet, string targetDn, string targetDisplayName,
        string editorUser, string executor, string machineName)
    {
        InitializeComponent();
        SamText.Text = changeSet.TargetSamAccountName;
        DisplayNameText.Text = targetDisplayName;
        DnText.Text = targetDn;
        MachineText.Text = machineName;
        ExecutorText.Text = executor;
        EditorText.Text = editorUser;
        ChangesList.ItemsSource = changeSet.Changes;
    }

    private void OK_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
