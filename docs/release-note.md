# Release Notes

## v0.1.0
### Title
ManageAdTool MVP 初版（InMemory / DirectoryReadOnly 対応）

### Note
- WPFベースのMVP画面を追加（ユーザー検索、詳細表示、mail/department/title比較、差分確認、処理結果欄）。
- InMemoryAdService を追加し、閉域検証向けにデモ運用を可能化。
- DirectoryServicesAdReadService（読み取り専用）を追加し、実AD検索/詳細/所属グループ表示に対応。
- AppPolicy / appsettings.json による AllowedTargetOuDns・ExcludedSamAccountNames・EditableAttributes・ServiceMode などの制御を実装。
- GitHub Actions に通常ビルド・リリースビルド（手動、version入力）を追加。

## v0.2.0
### Title
ManageAdTool 参照専用AD確認支援ツール 強化版

### Note
- 実AD更新ツールではなく、参照専用のAD確認支援ツールとして育てる方針に変更。
- 書き込み用ServiceMode / DirectoryServicesAdWriteService は実装しない方針。
- DirectoryReadOnly による実ADユーザー情報取得・所属グループ取得・検索結果件数表示を継続強化。
- ユーザー詳細表示項目として userAccountControl / lastLogonTimestamp / accountExpires の読み取り表示に対応。
- グループ検索、グループメンバー一覧、検索結果CSV出力、参照ログ（JSON Lines）を追加。
- 検索条件（部署、Mail有無、無効ユーザー表示）と appsettings.json による詳細表示項目制御を追加。
- `MaxSearchResults`（デフォルト 200）を appsettings.json で設定可能にし、ユーザー検索・グループ検索・グループメンバー一覧のいずれも上限到達時に利用者向けメッセージを表示。DirectoryReadOnly モードでは LDAP の SizeLimit にも適用。
- ServiceMode ごとの UI 表示制御を MainWindowViewModel に移動し、code-behind から分離（IsReadOnlyMode / CanEdit / EditControlsEnabled / EditBlockedReason）。
- 属性表示 GroupBox のヘッダーを「属性表示・変更予定確認」に変更し参照専用用途を明確化。
- UserEditUseCase を UserAttributeCompareUseCase に改名し、更新処理を持たないことを名称で明示。
- グループ追加・削除、GPO編集、ユーザー無効化、退職処理、OU移動、一括更新は未実装のまま維持。

## v0.3.0
### Title
ManageAdTool 別ユーザーログイン・Domain Admins 判定基盤

### Note
- 実AD更新機能は v0.3.0 でも実装しない方針を維持。
- 編集者ログイン UI を追加（domain\user 形式・PasswordBox・ログイン/ログアウトボタン・セッション状態表示）。
- `EditorAuthService` を追加：LDAP バインド認証（パスワードは保持・ログ記録しない）。
- Domain Admins グループメンバー判定（直接メンバーシップ / `LDAP_MATCHING_RULE_IN_CHAIN` によるネスト検索を設定で切り替え）。
- 編集セッション管理（`EditorSession`・`IsEditSessionActive`・`CurrentEditorUser`）：セッション有効時のみ編集コントロールを有効化。
- `DispatcherTimer`（30秒間隔）でセッション期限を自動チェック。
- 認証ログ（`auth.jsonl`）への記録：ログイン成功・失敗・拒否・ログアウトを JSON Lines で追記（パスワード非記録）。
- `AppPolicy` に `EditorAuthMode` / `AdminGroupDn` / `AllowNestedAdminGroupMembership` / `EditSessionMinutes` を追加。
- `UserEditPolicyService.Evaluate` に `isSessionActive` パラメーターを追加し、セッション有効時は DirectoryReadOnly でも編集可能と判定。
- `MainWindowViewModel` に `IsAuthSupported` / `IsEditSessionActive` / `CurrentEditorUser` / `SessionStatusText` / `SessionStatusBrush` / `CanLoginInput` / `StartSession` / `EndSession` / `RefreshSessionStatus` を追加。
- InMemory モードでは認証非対応メッセージを表示（ログイン入力無効）。
- `docs/roadmap.md` を追加。

## v0.4.0
### Title
ManageAdTool ユーザー属性限定編集 検証版

### Note
- **mail / department / title の3属性のみ** AD更新が可能になった（検証版）。
- グループ操作・GPO編集・OU移動・無効化・退職処理・パスワードリセット・一括更新は対象外。
- **更新要件**: Domain Admins 認証済みセッション + AllowedTargetOuDns 設定 + 差分確認済み + 差分1件以上。
- **更新フロー**: 差分確認 → 再認証ダイアログ（パスワード再入力） → 実行前確認ダイアログ → AD再取得・整合性チェック → 更新 → AD再取得して結果表示。
- **パスワード管理**: appsettings.json / ログへの保存なし。再認証パスワードは認証 + 書き込みに即時使用・破棄。
- **空文字更新禁止**: 属性クリアは対象外（将来機能）。
- **AllowedTargetOuDns 必須**: 未設定の場合は更新不可（セーフガード）。
- **AD整合性チェック**: 更新前にADから再取得し、ChangeSet.Before と現在値の一致を確認。不一致なら中止。
- 書き込み監査ログ（`write-audit.jsonl`）を追加：operationId / executor / editorUser / before / after / verifiedAfterUpdate を記録（パスワード非記録）。
- `IAdUserAttributeWriteService` / `DirectoryServicesAdUserAttributeWriteService` を追加。
- `WriteAuditLogger` / `WriteAuditEntry` / `UpdateResult` を追加。
- `UserEditPolicyService.EvaluateWrite` を追加。
- `ReAuthDialog` / `ConfirmUpdateDialog`（WPF ダイアログ）を追加。
- `MainWindowViewModel` に `IsWriteButtonEnabled` / `SetPendingReady` を追加。
- `EditInput_TextChanged` で差分確認状態をクリア（入力変更後は再確認が必要）。
- ServiceMode は追加せず。DirectoryReadOnly + セッション + OU設定で限定編集が有効になる構成を採用。
- `appsettings.UserAttributeEdit.sample.json` / `docs/validation-user-edit.md` を追加。
