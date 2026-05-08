using System.Windows;

namespace ManageAdTool.Views;

public partial class ReAuthDialog : Window
{
    public string? DomainUser { get; private set; }
    public string? Password { get; private set; }

    public ReAuthDialog(string prefillUser)
    {
        InitializeComponent();
        UserBox.Text = prefillUser;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        PasswordBox.Focus();
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        var user = UserBox.Text.Trim();
        var pass = PasswordBox.Password;

        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
        {
            ErrorText.Text = "ユーザー名とパスワードを入力してください";
            ErrorText.Visibility = Visibility.Visible;
            PasswordBox.Clear();
            PasswordBox.Focus();
            return;
        }

        DomainUser = user;
        Password = pass;
        PasswordBox.Clear();
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        PasswordBox.Clear();
        DialogResult = false;
    }
}
