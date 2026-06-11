using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using ManageAdTool.Models;

namespace ManageAdTool.Views;

public class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly AppPolicy _policy;
    private EditorSession? _session;
    private bool _isPendingReady;

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
            if (_session is null) return "ログインしていません（編集・更新にはログインが必要）";
            if (!_session.IsActive) return "セッション期限切れ - 再ログインしてください";
            var remaining = Math.Max(1, (int)Math.Ceiling((_session.ExpiresAt - DateTimeOffset.UtcNow).TotalMinutes));
            var ouNote = _policy.AllowedTargetOuDns.Count == 0 && _policy.EffectiveComputerOuDns.Count == 0
                ? "【AllowedTargetOuDns / AllowedComputerOuDns 未設定のため更新不可】" : string.Empty;
            return $"編集セッション: {_session.EditorUser}（残 {remaining} 分）【ユーザー: mail/displayName/sn/givenName】【コンピュータ: description】【グループ: メンバー追加・削除のみ】{ouNote}";
        }
    }

    public Brush SessionStatusBrush
    {
        get
        {
            if (IsEditSessionActive) return _policy.AllowedTargetOuDns.Count == 0 ? Brushes.DarkOrange : Brushes.DarkGreen;
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
            OnPropertyChanged(nameof(IsWriteButtonEnabled));
            OnPropertyChanged(nameof(WriteButtonDisabledReason));
        }
    }

    public bool EditControlsEnabled
        => IsReadOnlyMode ? _canEdit && IsEditSessionActive : _canEdit;

    public bool IsWriteButtonEnabled
        => IsReadOnlyMode && IsEditSessionActive && _isPendingReady;

    public string WriteButtonDisabledReason
    {
        get
        {
            if (IsWriteButtonEnabled) return string.Empty;
            if (!IsReadOnlyMode) return "DirectoryReadOnly モードが必要です";
            if (!IsAuthSupported) return "認証設定未構成（EditorAuthMode / AdminGroupDn を設定してください）";
            if (_session is null) return "未ログイン（Domain Admins アカウントでログインしてください）";
            if (!IsEditSessionActive) return "セッション期限切れ（再ログインしてください）";
            if (_policy.AllowedTargetOuDns.Count == 0) return "AllowedTargetOuDns 未設定のため更新不可（appsettings.json を確認してください）";
            if (!_canEdit) return $"更新不可: {_editBlockedReason}";
            return "「差分確認」ボタンを押して差分を確認してください";
        }
    }

    private bool _isGroupPendingReady;
    private bool _groupCanEdit;
    private string _groupEditBlockedReason = "グループ未選択";

    public string GroupEditBlockedReason
    {
        get => _groupEditBlockedReason;
        set { _groupEditBlockedReason = value; OnPropertyChanged(); }
    }

    public bool GroupCanEdit
    {
        get => _groupCanEdit;
        set
        {
            _groupCanEdit = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(GroupEditControlsEnabled));
            OnPropertyChanged(nameof(IsGroupWriteButtonEnabled));
            OnPropertyChanged(nameof(GroupWriteButtonDisabledReason));
        }
    }

    public bool GroupEditControlsEnabled
        => IsReadOnlyMode && _groupCanEdit && IsEditSessionActive;

    public bool IsGroupWriteButtonEnabled
        => IsReadOnlyMode && IsEditSessionActive && _isGroupPendingReady;

    public string GroupWriteButtonDisabledReason
    {
        get
        {
            if (IsGroupWriteButtonEnabled) return string.Empty;
            if (!IsReadOnlyMode) return "DirectoryReadOnly モードが必要です";
            if (!IsAuthSupported) return "認証設定未構成（EditorAuthMode / AdminGroupDn を設定してください）";
            if (_session is null) return "未ログイン（Domain Admins アカウントでログインしてください）";
            if (!IsEditSessionActive) return "セッション期限切れ（再ログインしてください）";
            if (_policy.EditableGroupOuDns.Count == 0) return "EditableGroupOuDns 未設定のため更新不可（appsettings.json を確認してください）";
            if (!_groupCanEdit) return $"更新不可: {_groupEditBlockedReason}";
            return "「差分確認」ボタンを押して差分を確認してください";
        }
    }

    public void SetGroupPendingReady(bool value)
    {
        _isGroupPendingReady = value;
        OnPropertyChanged(nameof(IsGroupWriteButtonEnabled));
        OnPropertyChanged(nameof(GroupWriteButtonDisabledReason));
    }

    private bool _isComputerPendingReady;
    private bool _computerCanEdit;
    private string _computerEditBlockedReason = "コンピュータ未選択";

    public string ComputerEditBlockedReason
    {
        get => _computerEditBlockedReason;
        set { _computerEditBlockedReason = value; OnPropertyChanged(); }
    }

    public bool ComputerCanEdit
    {
        get => _computerCanEdit;
        set
        {
            _computerCanEdit = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ComputerEditControlsEnabled));
            OnPropertyChanged(nameof(IsComputerWriteButtonEnabled));
            OnPropertyChanged(nameof(ComputerWriteButtonDisabledReason));
        }
    }

    public bool ComputerEditControlsEnabled
        => IsReadOnlyMode ? _computerCanEdit && IsEditSessionActive : _computerCanEdit;

    public bool IsComputerWriteButtonEnabled
        => IsReadOnlyMode && IsEditSessionActive && _isComputerPendingReady;

    public string ComputerWriteButtonDisabledReason
    {
        get
        {
            if (IsComputerWriteButtonEnabled) return string.Empty;
            if (!IsReadOnlyMode) return "DirectoryReadOnly モードが必要です";
            if (!IsAuthSupported) return "認証設定未構成（EditorAuthMode / AdminGroupDn を設定してください）";
            if (_session is null) return "未ログイン（Domain Admins アカウントでログインしてください）";
            if (!IsEditSessionActive) return "セッション期限切れ（再ログインしてください）";
            if (_policy.EffectiveComputerOuDns.Count == 0) return "AllowedComputerOuDns / AllowedTargetOuDns 未設定のため更新不可（appsettings.json を確認してください）";
            if (!_computerCanEdit) return $"更新不可: {_computerEditBlockedReason}";
            return "「差分確認」ボタンを押して差分を確認してください";
        }
    }

    public void SetComputerPendingReady(bool value)
    {
        _isComputerPendingReady = value;
        OnPropertyChanged(nameof(IsComputerWriteButtonEnabled));
        OnPropertyChanged(nameof(ComputerWriteButtonDisabledReason));
    }

    public string ReadOnlyModeLabel
        => IsReadOnlyMode ? "DirectoryReadOnly モード（編集にはログインが必要）" : string.Empty;

    public string ServiceModeText
        => IsReadOnlyMode ? "DirectoryReadOnly" : "InMemory";

    public string ServiceModeDescText
        => IsReadOnlyMode ? "実AD参照・認証後に限定更新可能" : "テスト用・実AD未接続";

    public void SetPendingReady(bool value)
    {
        _isPendingReady = value;
        OnPropertyChanged(nameof(IsWriteButtonEnabled));
        OnPropertyChanged(nameof(WriteButtonDisabledReason));
    }

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
        _isPendingReady = false;
        _isComputerPendingReady = false;
        _isGroupPendingReady = false;
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
        OnPropertyChanged(nameof(IsWriteButtonEnabled));
        OnPropertyChanged(nameof(WriteButtonDisabledReason));
        OnPropertyChanged(nameof(ComputerEditControlsEnabled));
        OnPropertyChanged(nameof(IsComputerWriteButtonEnabled));
        OnPropertyChanged(nameof(ComputerWriteButtonDisabledReason));
        OnPropertyChanged(nameof(GroupEditControlsEnabled));
        OnPropertyChanged(nameof(IsGroupWriteButtonEnabled));
        OnPropertyChanged(nameof(GroupWriteButtonDisabledReason));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
