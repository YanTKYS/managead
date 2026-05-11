using System.IO;
using System.Windows;
using ManageAdTool.Models;
using ManageAdTool.Services;
using ManageAdTool.Views;

namespace ManageAdTool;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ServiceMode 選択ダイアログを閉じた時点で「最後の Window が閉じた」と判定され、
        // MainWindow 表示前にアプリが終了しないよう、メイン画面表示までは明示終了にする。
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        AppPolicy? policy = null;
        try
        {
            policy = AppPolicyProvider.Load();
            AppendStartupLog(policy.LogPath, "Startup begin: showing ServiceMode selection dialog.");

            var modeDialog = new ServiceModeSelectionDialog(policy.ServiceMode);
            if (modeDialog.ShowDialog() != true)
            {
                AppendStartupLog(policy.LogPath, "Startup canceled at ServiceMode selection dialog.");
                Shutdown();
                return;
            }

            policy.ServiceMode = modeDialog.SelectedServiceMode;
            AppendStartupLog(policy.LogPath, $"ServiceMode selected: {policy.ServiceMode}. Creating MainWindow.");

            var mainWindow = new MainWindow(policy);
            MainWindow = mainWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.Show();
            AppendStartupLog(policy.LogPath, "MainWindow shown.");
        }
        catch (Exception ex)
        {
            var startupLogPath = AppendStartupLog(policy?.LogPath, $"Startup failed: {ex}");
            MessageBox.Show(
                $"ManageAdTool の起動に失敗しました。\n\n" +
                $"詳細は起動ログを確認してください。\n{startupLogPath}\n\n" +
                $"エラー: {ex.Message}",
                "ManageAdTool 起動エラー",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    private static string AppendStartupLog(string? configuredLogPath, string message)
    {
        var candidatePaths = new List<string>();
        var configuredLogDirectory = string.IsNullOrWhiteSpace(configuredLogPath)
            ? null
            : Path.GetDirectoryName(configuredLogPath);

        if (!string.IsNullOrWhiteSpace(configuredLogDirectory))
        {
            candidatePaths.Add(Path.Combine(configuredLogDirectory, "startup.log"));
        }

        candidatePaths.Add(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "ManageAdTool",
            "logs",
            "startup.log"));
        candidatePaths.Add(Path.Combine(Path.GetTempPath(), "ManageAdTool-startup.log"));

        foreach (var path in candidatePaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
                File.AppendAllText(path, $"{DateTimeOffset.Now:O}\t{message}{Environment.NewLine}");
                return path;
            }
            catch
            {
                // 次の候補へフォールバックする。起動ログ書き込み失敗で起動自体を止めない。
            }
        }

        return "(起動ログを書き込めませんでした)";
    }
}
