using System.Windows;
using ManageAdTool.Models;

namespace ManageAdTool.Views;

public partial class ConfirmUpdateDialog : Window
{
    public ConfirmUpdateDialog(ChangeSet changeSet, string targetDn, string editorUser, string executor)
    {
        InitializeComponent();
        SamText.Text = changeSet.TargetSamAccountName;
        DnText.Text = targetDn;
        EditorText.Text = editorUser;
        ExecutorText.Text = executor;
        ChangesList.ItemsSource = changeSet.Changes;
    }

    private void OK_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
