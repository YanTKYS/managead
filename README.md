# ManageAdTool

閉域ネットワーク向けの Active Directory 参照・限定編集支援ツール（v0.4.2）です。

> **重要**: 本ツールは「すべての AD 管理操作ができるツール」ではありません。  
> v0.4.x では **メールアドレス（mail）/ 表示名（displayName）/ 姓（sn）/ 名（givenName）** の4属性のみ更新可能です。  
> ユーザー名（sAMAccountName）は参照専用です（v0.4.2 では更新対象外）。  
> グループ操作・GPO編集・OU移動・ユーザー無効化・パスワードリセット・一括更新は実装していません。

> **v0.4.x の利用方針**: v0.4.x は「ユーザー属性限定編集の安定化フェーズ」です。  
> 必ず **検証用 OU 限定** で動作確認を完了してから、本番 OU を `AllowedTargetOuDns` に追加してください。  
> `AllowedTargetOuDns` が空の場合は更新機能全体が無効化されます（セーフガード）。

## 方針（v0.4.x 時点）
- 参照機能を主目的とし、AD情報確認・所属グループ確認・問い合わせ対応・事前調査で安全に利用できることを重視します。
- 書き込みは **mail / displayName / sn / givenName のみ** に限定します（メールアドレス / 表示名 / 姓 / 名）。
- 書き込みには **Domain Admins 認証済みセッション** が必須です。
- 書き込みには **AllowedTargetOuDns の設定** が必須です（未設定なら書き込み不可）。
- **パスワードは保存しません**（appsettings.json / ログ / メモリへの永続化なし）。更新実行時に再認証ダイアログで入力し、即時使用・破棄します。
- グループ追加・削除、GPO編集、OU移動、ユーザー無効化、退職処理、パスワードリセット、一括更新は対象外です。

## 実装済み機能（v0.4.2）

### 参照機能（v0.2.0 以降）
- AD ユーザー検索（SamAccountName / DisplayName / 氏名 / Mail）
- 検索条件の絞り込み（部署 / Mail 有無 / 無効ユーザー表示）
- ユーザー詳細表示（userAccountControl / lastLogonTimestamp / accountExpires）
- 所属グループ一覧表示（名前順）・クリップボードコピー
- グループ検索・グループメンバー一覧表示
- 検索結果 CSV 出力（SamAccountName / DisplayName / Surname / GivenName / Name / Mail / Department / Title 他）
- 参照ログ（JSON Lines 形式）

### 認証基盤（v0.3.0 以降）
- 別ユーザーログイン・Domain Admins 判定
- LDAP バインド認証（パスワード非保持・非ログ）
- 編集セッション管理（タイムアウト・自動期限チェック）
- 認証ログ（`auth.jsonl`）

### 限定編集（v0.4.0 新機能、v0.4.2 属性見直し）
- **メールアドレス（mail）/ 表示名（displayName）/ 姓（sn）/ 名（givenName）** の AD 更新（Domain Admins セッション必須）
- ユーザー名（sAMAccountName）は参照専用（更新不可）
- AllowedTargetOuDns 配下のユーザーのみ更新可能
- ExcludedSamAccountNames のユーザーは更新不可
- 空文字への更新は禁止
- 更新前: 差分確認 → 再認証 → 実行前確認ダイアログ → AD再取得・整合性チェック
- 更新後: AD再取得して結果表示・戻し候補表示
- 書き込み監査ログ（`write-audit.jsonl`、ldapAttribute フィールド含む、パスワード非記録）

## ServiceMode
`appsettings.json` の `AppPolicy.ServiceMode` で動作モードを切り替えます。

- `InMemory`: デモ・画面確認用。実 AD 接続・更新は行いません
- `DirectoryReadOnly`: 実 AD 参照 + Domain Admins セッション有効時に限定属性更新が可能

> v0.4.x では新しい ServiceMode を追加していません。  
> DirectoryReadOnly + Domain Admins 認証済みセッション + AllowedTargetOuDns 設定 = 限定編集が有効になります。

## appsettings.json 設定例（v0.4.2 限定編集有効構成）
```json
{
  "AppPolicy": {
    "ServiceMode": "DirectoryReadOnly",
    "AllowedTargetOuDns": [
      "OU=TestUsers,OU=Validation,DC=example,DC=local"
    ],
    "ExcludedSamAccountNames": [
      "administrator",
      "krbtgt"
    ],
    "EditableAttributes": ["mail", "displayName", "sn", "givenName"],
    "LogPath": "C:\\ProgramData\\ManageAdTool\\logs\\audit.jsonl",
    "MaxSearchResults": 200,
    "UserDetailDisplayAttributes": [
      "SamAccountName",
      "DisplayName",
      "Name",
      "DistinguishedName",
      "Enabled",
      "UserAccountControl",
      "LastLogonTimestamp",
      "AccountExpires"
    ],
    "EditorAuthMode": "DomainAdmins",
    "AdminGroupDn": "CN=Domain Admins,CN=Users,DC=example,DC=local",
    "AllowNestedAdminGroupMembership": false,
    "EditSessionMinutes": 15
  }
}
```

### 主要設定項目

| フィールド | 説明 | デフォルト |
|---|---|---|
| `ServiceMode` | `"InMemory"` / `"DirectoryReadOnly"` | `"InMemory"` |
| `AllowedTargetOuDns` | 参照・更新対象 OU（**更新には必須・空なら更新不可**） | `[]` |
| `ExcludedSamAccountNames` | 除外アカウント | `[]` |
| `MaxSearchResults` | 検索上限件数 | `200` |
| `EditorAuthMode` | `"DomainAdmins"` で認証 UI 有効化 | `"None"` |
| `AdminGroupDn` | Domain Admins グループの DN | `""` |
| `AllowNestedAdminGroupMembership` | ネストグループ判定（LDAP_MATCHING_RULE_IN_CHAIN） | `false` |
| `EditSessionMinutes` | 編集セッションタイムアウト（分） | `15` |

## パスワードの扱い
- appsettings.json にパスワードを保存しません
- ログ（audit.jsonl / auth.jsonl / write-audit.jsonl）にパスワードを記録しません
- ログイン時のパスワードは LDAP バインド後に破棄されます
- 「限定更新実行」押下時の再認証パスワードは、認証 + AD書き込みに即時使用し破棄されます

## ステータス
- v0.4.2 開発完了。実AD環境での動作検証が必要です。
- v0.3.0 の参照・認証機能は確認済みです。
- v0.4.2 の限定編集機能は実AD環境での動作検証が必要です。詳細は `docs/validation-user-edit.md` および `docs/test-record-v0.4.1.md` を参照してください。
- 詳細手順は `docs/validation-readonly.md`、`docs/validation-auth.md`、`docs/deploy.md` を参照してください。
- リリース ZIP は self-contained 形式のため、利用端末への .NET Desktop Runtime の別途インストールは不要です。

## 現在の対象外機能
グループ追加・削除 / GPO編集 / OU移動 / ユーザー無効化・退職処理 / パスワードリセット / 新規ユーザー作成 / 一括更新は実装していません。

## 今後の検討事項
詳細は `docs/backlog.md`・`docs/roadmap.md` を参照してください。
