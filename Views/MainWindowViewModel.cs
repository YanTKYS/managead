using System.ComponentModel;
using System.Runtime.CompilerServices;
using ManageAdTool.Models;

namespace ManageAdTool.Views;

public class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly AppPolicy _policy;

    public MainWindowViewModel(AppPolicy policy)
    {
        _policy = policy;
    }

    public bool IsReadOnlyMode
        => string.Equals(_policy.ServiceMode, "DirectoryReadOnly", StringComparison.OrdinalIgnoreCase);

    public bool IsEditMode => !IsReadOnlyMode;

    public string ReadOnlyModeLabel
        => IsReadOnlyMode ? "DirectoryReadOnly モードのため参照のみ" : string.Empty;

    private string _editBlockedReason = "ユーザー未選択";
    public string EditBlockedReason
    {
        get => _editBlockedReason;
        set { _editBlockedReason = value; OnPropertyChanged(); }
    }

    private bool _canEdit;
    public bool CanEdit
    {
        get => _canEdit;
        set
        {
            _canEdit = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(EditControlsEnabled));
        }
    }

    public bool EditControlsEnabled => _canEdit && !IsReadOnlyMode;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
