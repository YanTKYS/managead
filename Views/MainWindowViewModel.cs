using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using ManageAdTool.Models;

namespace ManageAdTool.Views;

public class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly AppPolicy _policy;
    private EditorSession? _session;

    public MainWindowViewModel(AppPolicy policy)
    {
        _policy = policy;
    }

    public bool IsReadOnlyMode
        => string.Equals(_policy.ServiceMode, "DirectoryReadOnly", StringComparison.OrdinalIgnoreCase);

    public bool IsAuthSupported
        => IsReadOnlyMode && !string.IsNullOrWhiteSpace(_policy.AdminGroupDn)
            && string.Equals(_policy.EditorAuthMode, "DomainAdmins", StringComparison.OrdinalIgnoreCase);

    public bool IsEditSessionActive => _session?.IsActive == true;

    public string CurrentEditorUser => _session?.IsActive == true ? _session.EditorUser : string.Empty;

    public string SessionStatusText
    {
        get
        {
            if (!IsReadOnlyMode) return string.Empty;
            if (!IsAuthSupported) return "編集セッション機能は未設定です（EditorAuthMode / AdminGroupDn を設定してください）";
            if (_session is null) return "ログインしていません（編集にはログインが必要）";
            if (!_session.IsActive) return "セッション期限切れ - 再ログインしてください";
            var remaining = Math.Max(1, (int)Math.Ceiling((_session.ExpiresAt - DateTimeOffset.UtcNow).TotalMinutes));
            return $"編集セッション: {_session.EditorUser}（残 {remaining} 分）【v0.3.0: 属性比較確認のみ有効・AD更新は未実装】";
        }
    }

    public Brush SessionStatusBrush
    {
        get
        {
            if (IsEditSessionActive) return Brushes.DarkGreen;
            if (_session is { IsActive: false }) return Brushes.DarkRed;
            return Brushes.DarkOrange;
        }
    }

    public bool CanLoginInput => IsAuthSupported && !IsEditSessionActive;

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

    public bool EditControlsEnabled
        => IsReadOnlyMode ? _canEdit && IsEditSessionActive : _canEdit;

    public string ReadOnlyModeLabel
        => IsReadOnlyMode ? "DirectoryReadOnly モードのため参照のみ（編集にはログインが必要）" : string.Empty;

    public void StartSession(string editorUser)
    {
        _session = new EditorSession
        {
            EditorUser = editorUser,
            StartedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(_policy.EditSessionMinutes)
        };
        RefreshSessionStatus();
    }

    public void EndSession()
    {
        _session = null;
        RefreshSessionStatus();
    }

    public void RefreshSessionStatus()
    {
        OnPropertyChanged(nameof(IsEditSessionActive));
        OnPropertyChanged(nameof(CurrentEditorUser));
        OnPropertyChanged(nameof(SessionStatusText));
        OnPropertyChanged(nameof(SessionStatusBrush));
        OnPropertyChanged(nameof(CanLoginInput));
        OnPropertyChanged(nameof(EditControlsEnabled));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
