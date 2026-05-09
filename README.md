# ManageAdTool

閉域ネットワーク向けの Active Directory 参照・限定編集支援ツール（v0.6.0）です。

> **重要**: 本ツールは「すべての AD 管理操作ができるツール」ではありません。  
> v0.6.0 では **ユーザー属性**（mail / displayName / sn / givenName）、**コンピュータ description**、**グループメンバー追加・削除（ユーザーのみ）** のみ更新可能です。  
> グループ作成・削除・リネーム / グループをグループに追加 / コンピュータをグループに追加 / 無効化・OU移動・削除・GPO編集・パスワードリセット・一括更新は実装していません。

> **利用方針**: 必ず **検証用 OU 限定** で動作確認を完了してから、本番 OU を設定に追加してください。  
> `AllowedTargetOuDns` / `AllowedComputerOuDns` / `EditableGroupOuDns` が空の場合、対応する更新機能が無効化されます（セーフガード）。

## 方針（v0.6.0）
- 参照機能を主目的とし、AD情報確認・所属グループ確認・問い合わせ対応・事前調査で安全に利用できることを重視します。
- ユーザー書き込みは **mail / displayName / sn / givenName のみ** に限定します。
- コンピュータ書き込みは **description のみ** に限定します。
- グループ書き込みは **ユーザーのグループへの追加・削除のみ** に限定します（グループをグループに追加・コンピュータ追加は不可）。
- 書き込みには **Domain Admins 認証済みセッション** が必須です。
- **パスワードは保存しません**（appsettings.json / ログ / メモリへの永続化なし）。更新実行時に再認証ダイアログで入力し、即時使用・破棄します。
- グループ作成・削除・リネーム、GPO編集、OU移動、ユーザー無効化、退職処理、パスワードリセット、一括更新は対象外です。

## 実装済み機能（v0.6.0）

### 参照機能（v0.2.0 以降）
- AD ユーザー検索（SamAccountName / DisplayName / 氏名 / Mail）
- 検索条件の絞り込み（部署 / Mail 有無 / 無効ユーザー表示）
- ユーザー詳細表示（userAccountControl / lastLogonTimestamp / accountExpires）
- 所属グループ一覧表示（名前順）・クリップボードコピー
- グループ検索・グループメンバー一覧表示
- 検索結果 CSV 出力
- 参照ログ（JSON Lines 形式）

### グループ参照・メンバー限定編集（v0.6.0 新機能）

**参照:**
- グループ詳細表示（Name / DN / Description）
- ユーザーメンバー一覧（SamAccountName / DisplayName / Mail / Department / Enabled）
- コンピュータメンバー / ネストグループ / memberOf 表示

**メンバー限定編集:**
- 追加・削除対象は **ユーザーのみ**（グループをグループに追加・コンピュータ追加は不可）
- **Domain Admins 認証済みセッション**が必須
- `EditableGroupOuDns` 配下のグループのみ編集可能（空の場合は更新不可セーフガード）
- `ProtectedGroupNames` / `ProtectedGroupDns` に登録されたグループは編集不可
- 追加予定・削除予定のステージング UI（差分確認後のみ更新可能）
- 整合性チェック（更新前にAD再取得し既存メンバー状態を確認）
- 更新フロー（8段階）: 変更予定作成（ステージング） → 差分確認 → 再認証 → 実行前確認ダイアログ → 更新前AD再取得・整合性チェック → 更新 → 更新後AD再取得 → 監査ログ出力
- 書き込み監査ログ（`write-audit.jsonl`、`targetType: "Group"` / `operationName: "UpdateGroupMembers"` 記録）

> **グループ操作の対象外**: グループ作成・削除・リネーム / グループをグループに追加 / コンピュータをグループに追加 / GPO編集 / OU移動 / 一括更新は実装していません。

### コンピュータ参照・限定編集（v0.5.0 新機能）

**参照:**
- AD コンピュータ検索（Name / DNSHostName / sAMAccountName、2文字以上）
- 検索条件の絞り込み（OS / description 有無 / 無効端末表示）
- コンピュータ詳細表示（Name / DNSHostName / OS / Enabled / Description / DN / LastLogon / WhenCreated / WhenChanged）
- 所属グループ表示・クリップボードコピー
- コンピュータ検索結果 CSV 出力

**description 限定更新:**
- 更新可能な属性は **description のみ**（LDAP属性名: `description`）
- **Domain Admins 認証済みセッション**が必須
- `AllowedComputerOuDns`（空なら `AllowedTargetOuDns` をフォールバック）配下のコンピュータのみ更新可能
- `ExcludedComputerNames` に登録されたコンピュータは更新不可
- 空文字への更新は禁止（属性クリアは対象外）
- 更新フロー: 差分確認 → 再認証 → 実行前確認ダイアログ → AD再取得・整合性チェック → 更新 → AD再取得
- 書き込み監査ログ（`write-audit.jsonl`、`targetType: "Computer"` / `operationName: "UpdateComputerDescription"` 記録）
- 戻し候補表示・クリップボードコピー

> **コンピュータ操作の対象外**: 無効化・OU移動・削除・グループ変更・GPO編集・パスワードリセット・一括更新は実装していません。

### 認証基盤（v0.3.0 以降）
- 別ユーザーログイン・Domain Admins 判定
- LDAP バインド認証（パスワード非保持・非ログ）
- 編集セッション管理（タイムアウト・自動期限チェック）
- 認証ログ（`auth.jsonl`）

### ユーザー限定編集（v0.4.0 以降）
- **mail / displayName / sn / givenName** の AD 更新（Domain Admins セッション必須）
- ユーザー名（sAMAccountName）は参照専用（更新不可）
- 書き込み監査ログ（`write-audit.jsonl`）

## ServiceMode

| 値 | 説明 |
|---|---|
| `InMemory` | デモ・画面確認用。実 AD 接続・更新は行いません |
| `DirectoryReadOnly` | 実 AD 参照 + Domain Admins セッション有効時に限定属性更新が可能 |

## appsettings.json 設定例（v0.6.0 フル構成）
```json
{
  "AppPolicy": {
    "ServiceMode": "DirectoryReadOnly",
    "AllowedTargetOuDns": ["OU=Users,DC=example,DC=local"],
    "ExcludedSamAccountNames": ["administrator", "krbtgt"],
    "EditableAttributes": ["mail", "displayName", "sn", "givenName"],
    "AllowedComputerOuDns": [
      "OU=Computers,DC=example,DC=local",
      "OU=Servers,DC=example,DC=local"
    ],
    "ExcludedComputerNames": ["DC-01", "DC-02"],
    "EditableComputerAttributes": ["description"],
    "EditableGroupOuDns": [
      "OU=Groups,DC=example,DC=local"
    ],
    "ProtectedGroupNames": ["Domain Admins", "Enterprise Admins", "Schema Admins", "Administrators"],
    "ProtectedGroupDns": [
      "CN=Domain Admins,CN=Users,DC=example,DC=local"
    ],
    "LogPath": "C:\\ProgramData\\ManageAdTool\\logs\\audit.jsonl",
    "MaxSearchResults": 200,
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
| `AllowedTargetOuDns` | ユーザー参照・更新対象 OU | `[]` |
| `ExcludedSamAccountNames` | 除外ユーザーアカウント | `[]` |
| `AllowedComputerOuDns` | コンピュータ更新対象 OU（**明示設定を推奨**。空のとき AllowedTargetOuDns にフォールバックするが、コンピュータ用に個別設定することを推奨） | `[]` |
| `ExcludedComputerNames` | 除外コンピュータ名 | `[]` |
| `EditableGroupOuDns` | グループメンバー更新対象 OU（**明示設定必須**。空の場合更新不可） | `[]` |
| `ProtectedGroupNames` | 編集保護グループ名（Domain Admins 等を必ず登録） | `[]` |
| `ProtectedGroupDns` | 編集保護グループの DN | `[]` |
| `MaxSearchResults` | 検索上限件数 | `200` |
| `EditorAuthMode` | `"DomainAdmins"` で認証 UI 有効化 | `"None"` |
| `AdminGroupDn` | Domain Admins グループの DN | `""` |
| `EditSessionMinutes` | 編集セッションタイムアウト（分） | `15` |

## パスワードの扱い
- appsettings.json にパスワードを保存しません
- ログ（audit.jsonl / auth.jsonl / write-audit.jsonl）にパスワードを記録しません
- ログイン・更新実行時のパスワードは認証 + AD書き込みに即時使用し破棄されます

## ステータス
- v0.6.0 開発完了。実AD環境での動作検証が必要です。
- グループメンバー編集の検証手順は `docs/validation-group-edit.md` を参照してください。
- コンピュータ description 編集の検証手順は `docs/validation-computer-edit.md` を参照してください。
- ユーザー属性編集の検証手順は `docs/validation-user-edit.md` を参照してください。
- 詳細手順は `docs/validation-readonly.md`、`docs/validation-auth.md`、`docs/deploy.md` を参照してください。
- リリース ZIP は self-contained 形式のため、利用端末への .NET Desktop Runtime の別途インストールは不要です。

## 現在の対象外機能
グループ作成・削除・リネーム / グループをグループに追加 / コンピュータをグループに追加 / GPO編集 / OU移動 / ユーザー無効化・退職処理 / パスワードリセット / 新規ユーザー作成 / 一括更新は実装していません。

## 今後の検討事項
詳細は `docs/backlog.md`・`docs/roadmap.md` を参照してください。
