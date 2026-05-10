# ManageAdTool

閉域ネットワーク向けの Active Directory 参照・限定編集支援ツール（v0.9.5）です。

> **重要**: 本ツールは「すべての AD 管理操作ができるツール」ではありません。  
> ユーザー属性（mail / displayName / sn / givenName）・コンピュータ description・グループメンバー追加削除（ユーザーのみ）のみ更新可能です。
> v0.9.5 は v1.0.0 前のリリースパッケージ・配布物整理版です。新しい AD 更新操作は追加していません。

---

## できること（v0.9.5）

### 参照
- AD ユーザー検索・詳細表示・所属グループ確認
- グループ検索・ユーザーメンバー一覧・コンピュータメンバー・ネストグループ・memberOf 表示
- コンピュータ検索・詳細表示・所属グループ確認
- 検索結果 CSV 出力・参照ログ（JSON Lines）

### 限定編集（Domain Admins セッション必須）
- **ユーザー属性**: mail / displayName / sn / givenName のみ（`AllowedTargetOuDns` 配下）
- **コンピュータ**: description のみ（`AllowedComputerOuDns` 配下）
- **グループメンバー**: ユーザーの追加・削除のみ（`EditableGroupOuDns` 配下・保護グループ除く）

### オペレーション支援
- 対象ユーザーを検索・選択し、属性変更予定とグループ追加・削除予定を一画面で整理できる
- 変更予定をテキスト形式でサマリー生成し、クリップボードへコピーできる（決裁書・作業メモへの貼り付けを想定）
- 確認チェックリストを画面上で管理できる（永続保存なし）
- **AD更新は行わない**: このタブはあくまで変更予定の確認用。実際の更新はユーザー編集・グループ編集タブから実行する

### GPOシミュレーション（簡易・OUリンク参照）
- ユーザー / コンピュータ / 両方を対象に、OUリンクから推定されるGPO一覧を参照できる
- GPO名・GPO ID・適用対象・リンク先OU・有効/無効・強制適用の有無を表示する
- 結果のクリップボードコピーと CSV 出力ができる
- **GPO編集は行わない**: GPOリンク変更・セキュリティフィルタ変更・WMIフィルタ変更は実装していない
- **簡易実装の制約**: セキュリティフィルタ・WMIフィルタ・継承ブロック・ループバック処理・サイトリンクは考慮しない。GPMC の RSOP（ポリシーの結果セット）や `gpresult /R` の代替にはならない

### ログ確認
- 参照ログ（audit.jsonl）・認証ログ（auth.jsonl）・書き込みログ（write-audit.jsonl）をアプリ内で閲覧できる
- ログ種別をドロップダウンで選択し、一覧表示・フィルタリングができる
- 成否・日付範囲・操作種別キーワード・対象名キーワードでフィルタリングできる
- 行選択で整形済み JSON を表示できる（パスワードフィールドは `***` にマスク）
- 表示中のログを CSV 出力できる
- ログ格納フォルダをエクスプローラーで開けるボタンを提供する
- **ログ編集・削除は行わない**: ログ確認タブは閲覧専用。ログファイルの書き換えは行わない
- **対象外機能**: ログ改ざん検知・ログローテーション・外部 DB への保存は実装していない
- **取り扱い注意**: ログには AD 属性・DN・操作履歴など内部情報が含まれます。閲覧・エクスポートした内容の管理に注意してください

> **v0.9.0 について**: v0.9.0 では新しい AD 更新操作は追加していません。ログ確認タブは既存の監査ログを確認するための閲覧支援機能です。

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

## リリースZIPの構成

リリース ZIP は self-contained 形式を想定しており、利用端末への .NET Desktop Runtime の別途インストールは不要です。展開後は次の構成を想定しています。

```text
ManageAdTool-vX.Y.Z/
  ManageAdTool.exe
  appsettings.json
  README.md
  config-samples/
  docs/
```

配布用 `appsettings.json` は `ServiceMode: "InMemory"`、OU 許可リスト空、`EditorAuthMode: "None"` の安全側初期値です。実 AD 参照・限定編集を行う場合は、`config-samples/` から用途に合うサンプルをコピーし、検証用 OU から設定してください。

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
| `appsettings.DirectoryReadOnly.sample.json` | 実AD参照確認用（DN は example.local のサンプル） |
| `appsettings.UserAttributeEdit.sample.json` | ユーザー属性編集を検証用 OU から有効化する例 |
| `appsettings.ComputerDescriptionEdit.sample.json` | コンピュータ description 編集を検証用 OU から有効化する例 |
| `appsettings.GroupMembershipEdit.sample.json` | ユーザー限定のグループメンバー編集を検証用 OU から有効化する例 |

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
| `MaxLogDisplayRows` | ログ確認タブの最大表示行数（末尾から取得） | `1000` |
| `EditorAuthMode` | `"DomainAdmins"` で認証 UI 有効化 | `"None"` |
| `AdminGroupDn` | Domain Admins グループの DN | `""` |
| `EditSessionMinutes` | 編集セッションタイムアウト（分） | `15` |
| `LogPath` | 監査ログ出力先（audit.jsonl / auth.jsonl / write-audit.jsonl） | `C:\ProgramData\ManageAdTool\logs\audit.jsonl` |
| `EnableOperationSupport` | `false` にするとオペレーション支援タブを非表示にします | `true` |
| `OperationChecklistItems` | 設定から読み込まれますが、チェックリスト UI とサマリーには反映されません（v1.0.0 検討） | `[]` |

---

## パスワードの扱い
- appsettings.json にパスワードを保存しません
- ログ（audit.jsonl / auth.jsonl / write-audit.jsonl）にパスワードを記録しません
- ログイン・更新実行時のパスワードは認証 + AD書き込みに即時使用し破棄されます

---

## 操作・運用資料

- 詳細な操作手順は `docs/operation/user-manual.md` を参照してください。
- 管理者向け設定手順は `docs/operation/admin-manual.md` を参照してください。
- トラブル時は `docs/operation/troubleshooting.md` を参照してください。

---

## 自動テスト

- `ManageAdTool.Tests` は v1.0.0 前の回帰防止を目的とした、AD 接続を伴わない単体テストです。
- テスト対象は設定読み込み、編集可否判定、ログ読み込み、ChangeSet 生成、`InMemoryAdService` のダミーデータ操作に限定しています。実 AD 参照・実 AD 更新・WPF UI 自動操作は行いません。
- 実 AD 環境での検証は、引き続き `docs/operation/validation-*.md` の手順に従って、検証用 OU 限定で実施してください。

```bash
dotnet test ManageAdTool.Tests/ManageAdTool.Tests.csproj
```

---

## ドキュメント

| ファイル | 内容 |
|---|---|
| `docs/operation/deploy.md` | 配布・閉域端末への持ち込み手順 |
| `docs/operation/user-manual.md` | 利用者向け操作説明書 |
| `docs/operation/admin-manual.md` | 管理者向け設定・運用手順 |
| `docs/operation/troubleshooting.md` | トラブルシューティング |
| `docs/operation/validation-readonly.md` | 参照機能の検証手順 |
| `docs/operation/validation-auth.md` | 認証機能の検証手順 |
| `docs/operation/validation-user-edit.md` | ユーザー属性編集の検証手順 |
| `docs/operation/validation-computer-edit.md` | コンピュータ description 編集の検証手順 |
| `docs/operation/validation-group-edit.md` | グループメンバー編集の検証手順 |
| `docs/operation/validation-group-member-edit.md` | グループメンバー編集の補足検証手順 |
| `docs/operation/validation-operation-support.md` | オペレーション支援機能の検証手順 |
| `docs/operation/validation-gpo-simulation.md` | GPOシミュレーション機能の検証手順 |
| `docs/operation/validation-log-viewer.md` | ログ確認機能の検証手順 |
| `docs/design/design-account-expiration.md` | アカウント有効期限の設計メモ |
| `docs/planning/roadmap.md` | 今後の方向性 |
| `docs/planning/backlog.md` | 今後やること・検討事項 |
| `docs/planning/completed-work.md` | 完了済み作業の詳細履歴 |
| `docs/release/release-note.md` | リリースノート |

> リリース ZIP は self-contained 形式のため、利用端末への .NET Desktop Runtime の別途インストールは不要です。
