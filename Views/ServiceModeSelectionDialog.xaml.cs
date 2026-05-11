using System.Windows;

namespace ManageAdTool.Views;

public partial class ServiceModeSelectionDialog : Window
{
    public string SelectedServiceMode { get; private set; } = "InMemory";

    public ServiceModeSelectionDialog(string initialServiceMode)
    {
        InitializeComponent();

        if (string.Equals(initialServiceMode, "DirectoryReadOnly", StringComparison.OrdinalIgnoreCase))
        {
            DirectoryReadOnlyRadio.IsChecked = true;
        }
        else
        {
            InMemoryRadio.IsChecked = true;
        }
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        SelectedServiceMode = DirectoryReadOnlyRadio.IsChecked == true ? "DirectoryReadOnly" : "InMemory";
        DialogResult = true;
    }
}
