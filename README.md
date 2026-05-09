# ManageAdTool

閉域ネットワーク向けの Active Directory 参照・限定編集支援ツール（v0.6.0）です。

> **重要**: 本ツールは「すべての AD 管理操作ができるツール」ではありません。  
> ユーザー属性（mail / displayName / sn / givenName）・コンピュータ description・グループメンバー追加削除（ユーザーのみ）のみ更新可能です。

---

## できること（v0.6.0）

### 参照
- AD ユーザー検索・詳細表示・所属グループ確認
- グループ検索・ユーザーメンバー一覧・コンピュータメンバー・ネストグループ・memberOf 表示
- コンピュータ検索・詳細表示・所属グループ確認
- 検索結果 CSV 出力・参照ログ（JSON Lines）

### 限定編集（Domain Admins セッション必須）
- **ユーザー属性**: mail / displayName / sn / givenName のみ（`AllowedTargetOuDns` 配下）
- **コンピュータ**: description のみ（`AllowedComputerOuDns` 配下）
- **グループメンバー**: ユーザーの追加・削除のみ（`EditableGroupOuDns` 配下・保護グループ除く）

### 主な制限
- ユーザー名（sAMAccountName）は参照専用（更新不可）
- グループ作成・削除・リネーム / グループをグループに追加 / コンピュータをグループに追加 / GPO編集 / OU移動 / 無効化・退職処理 / パスワードリセット / 一括更新は実装していません
- パスワードは保存しません（appsettings.json・ログへの記録なし）

---

## ServiceMode

| 値 | 説明 |
|---|---|
| `InMemory` | デモ・画面確認用。実 AD 接続・更新は行いません |
| `DirectoryReadOnly` | 実 AD 参照 + Domain Admins セッション有効時に限定属性更新が可能 |

---

## 基本的な使い方

1. `config-samples/` から利用モードに応じたサンプルをコピーして `appsettings.json` を作成する
2. OU DN・除外ユーザー名・ログ出力先を環境に合わせて編集する
3. `ManageAdTool.exe` を起動する
4. まず `InMemory` モードで画面動作を確認し、次に `DirectoryReadOnly` で実 AD 接続を検証する

> **利用方針**: 必ず **検証用 OU 限定** で動作確認を完了してから、本番 OU を設定に追加してください。  
> `AllowedTargetOuDns` / `AllowedComputerOuDns` / `EditableGroupOuDns` が空の場合、対応する更新機能が無効化されます（セーフガード）。

---

## 設定サンプル（config-samples/）

| ファイル | 用途 |
|---|---|
| `appsettings.InMemory.sample.json` | デモ・動作確認用（実AD接続なし） |
| `appsettings.DirectoryReadOnly.sample.json` | 実AD参照のみ（更新なし） |
| `appsettings.UserAttributeEdit.sample.json` | ユーザー属性編集を有効化 |
| `appsettings.ComputerDescriptionEdit.sample.json` | コンピュータ description 編集を有効化 |
| `appsettings.GroupMembershipEdit.sample.json` | グループメンバー編集を有効化 |

### 主な設定項目

| フィールド | 説明 | デフォルト |
|---|---|---|
| `ServiceMode` | `"InMemory"` / `"DirectoryReadOnly"` | `"InMemory"` |
| `AllowedTargetOuDns` | ユーザー参照・更新対象 OU | `[]` |
| `ExcludedSamAccountNames` | 除外ユーザーアカウント | `[]` |
| `AllowedComputerOuDns` | コンピュータ更新対象 OU | `[]` |
| `ExcludedComputerNames` | 除外コンピュータ名 | `[]` |
| `EditableGroupOuDns` | グループメンバー更新対象 OU（**明示設定必須**。空の場合更新不可） | `[]` |
| `ProtectedGroupNames` | 編集保護グループ名（Domain Admins 等を必ず登録） | `[]` |
| `ProtectedGroupDns` | 編集保護グループの DN | `[]` |
| `MaxSearchResults` | 検索上限件数 | `200` |
| `EditorAuthMode` | `"DomainAdmins"` で認証 UI 有効化 | `"None"` |
| `AdminGroupDn` | Domain Admins グループの DN | `""` |
| `EditSessionMinutes` | 編集セッションタイムアウト（分） | `15` |
| `LogPath` | 監査ログ出力先（audit.jsonl / auth.jsonl / write-audit.jsonl） | `""` |

---

## パスワードの扱い
- appsettings.json にパスワードを保存しません
- ログ（audit.jsonl / auth.jsonl / write-audit.jsonl）にパスワードを記録しません
- ログイン・更新実行時のパスワードは認証 + AD書き込みに即時使用し破棄されます

---

## ドキュメント

| ファイル | 内容 |
|---|---|
| `docs/operation/deploy.md` | 配布・閉域端末への持ち込み手順 |
| `docs/operation/validation-readonly.md` | 参照機能の検証手順 |
| `docs/operation/validation-auth.md` | 認証機能の検証手順 |
| `docs/operation/validation-user-edit.md` | ユーザー属性編集の検証手順 |
| `docs/operation/validation-computer-edit.md` | コンピュータ description 編集の検証手順 |
| `docs/operation/validation-group-edit.md` | グループメンバー編集の検証手順 |
| `docs/design/design-account-expiration.md` | アカウント有効期限の設計メモ |
| `docs/planning/roadmap.md` | 今後の方向性 |
| `docs/planning/backlog.md` | 今後やること・検討事項 |
| `docs/planning/completed-work.md` | 完了済み作業の詳細履歴 |
| `docs/release/release-note.md` | リリースノート |

> リリース ZIP は self-contained 形式のため、利用端末への .NET Desktop Runtime の別途インストールは不要です。
