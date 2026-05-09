using System.Windows;
using ManageAdTool.Models;

namespace ManageAdTool.Views;

public partial class ConfirmComputerUpdateDialog : Window
{
    public ConfirmComputerUpdateDialog(ChangeSet changeSet, string targetDn, string computerName,
        string dnsHostName, string editorUser, string executor, string machineName)
    {
        InitializeComponent();
        ComputerNameText.Text = computerName;
        DnsHostNameText.Text = dnsHostName;
        DnText.Text = targetDn;
        MachineText.Text = machineName;
        ExecutorText.Text = executor;
        EditorText.Text = editorUser;
        ChangesList.ItemsSource = changeSet.Changes;
    }

    private void OK_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
