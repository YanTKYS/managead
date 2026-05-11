using System.Windows;
using ManageAdTool.Services;
using ManageAdTool.Views;

namespace ManageAdTool;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var policy = AppPolicyProvider.Load();
        var modeDialog = new ServiceModeSelectionDialog(policy.ServiceMode);
        if (modeDialog.ShowDialog() != true)
        {
            Shutdown();
            return;
        }

        policy.ServiceMode = modeDialog.SelectedServiceMode;

        var mainWindow = new MainWindow(policy);
        MainWindow = mainWindow;
        mainWindow.Show();
    }
}
