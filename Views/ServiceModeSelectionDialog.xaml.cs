using System.Windows;

namespace ManageAdTool.Views;

public partial class ServiceModeSelectionDialog : Window
{
    public string SelectedServiceMode { get; private set; } = "InMemory";

    public ServiceModeSelectionDialog(string initialServiceMode, string editorAuthMode, string adminGroupDn)
    {
        InitializeComponent();

        EditorAuthModeText.Text = $"EditorAuthMode: {FormatUnset(editorAuthMode)}";
        AdminGroupText.Text = string.IsNullOrWhiteSpace(adminGroupDn)
            ? "AdminGroupDn: (未設定)"
            : $"AdminGroupDn: {adminGroupDn}";

        if (string.Equals(initialServiceMode, "DirectoryReadOnly", StringComparison.OrdinalIgnoreCase))
        {
            DirectoryReadOnlyRadio.IsChecked = true;
        }
        else
        {
            InMemoryRadio.IsChecked = true;
        }
    }

    private static string FormatUnset(string value)
        => string.IsNullOrWhiteSpace(value) ? "(未設定)" : value;

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        SelectedServiceMode = DirectoryReadOnlyRadio.IsChecked == true ? "DirectoryReadOnly" : "InMemory";
        DialogResult = true;
    }
}
