# Release Notes

## v0.6.0
### Title
ManageAdTool グループ詳細表示強化・ユーザー限定グループメンバー追加・削除

### Note
- **グループ詳細表示強化**: 「グループ参照・メンバー編集」タブを刷新。ユーザーメンバー DataGrid（SamAccountName / DisplayName / Mail / Department / Enabled）、コンピュータメンバー数・ネストグループ数・memberOf 表示に対応。
- **グループメンバー限定編集**: ユーザーのみグループへの追加・削除を可能にした。グループをグループに追加・コンピュータをグループに追加・グループ作成・削除・リネームは対象外。
- **ステージング UI**: 追加予定・削除予定リストにユーザーを積み上げ、「差分確認」後のみ「限定更新実行」が可能。
- **セーフガード**: `EditableGroupOuDns` 未設定なら更新不可。`ProtectedGroupNames` / `ProtectedGroupDns` に登録されたグループは編集不可。既存メンバーの重複追加禁止・非メンバーの削除禁止。矛盾する操作（同一ユーザーの追加と削除の同時ステージング）を禁止。
- **SAM検索**: ユーザー追加時は SAM を入力して AD 検索し、見つかった場合のみ追加予定リストに登録（DN 不明ユーザーの追加を防止）。
- **整合性チェック**: 更新前に AD からグループを再取得し、追加予定ユーザーが既にメンバーでないか・削除予定ユーザーが依然メンバーかを確認して不一致なら中止。
- **更新フロー（8段階）**: ① 変更予定作成（追加・削除対象ユーザーをステージング） → ② 差分確認（プレビュー表示） → ③ 再認証ダイアログ（パスワード再入力） → ④ ConfirmGroupMemberUpdateDialog（グループ名・DN・実行端末・編集者・追加ユーザー（緑）・削除ユーザー（赤）表示） → ⑤ 更新前AD再取得・整合性チェック（重複追加・非メンバー削除の検出） → ⑥ 更新（LDAP `member` 属性更新） → ⑦ 更新後AD再取得・結果表示 → ⑧ 監査ログ出力（write-audit.jsonl）。
- **write-audit.jsonl**: `targetType: "Group"` / `operationName: "UpdateGroupMembers"` で記録。各 member 変更は `ldapAttribute: "member"`, `before = ""` (追加) または `after = ""` (削除) として個別に記録。
- **AppPolicy 拡張**: `EditableGroupOuDns` / `ProtectedGroupNames` / `ProtectedGroupDns` を追加。
- **新規モデル・サービス**: `AdGroupDetail` / `IAdGroupMemberWriteService` / `DirectoryServicesAdGroupMemberWriteService` を追加。`IAdService` に `GetGroupDetail` / `FindUserForGroupAdd` を追加。
- **禁止操作**: グループ作成・削除・リネーム / グループをグループに追加 / コンピュータをグループに追加 / GPO編集 / OU移動 / 一括更新は実装していません。
- `docs/validation-group-edit.md` / `appsettings.GroupMembershipEdit.sample.json` を追加。

## v0.5.0
### Title
ManageAdTool コンピュータオブジェクト参照・description 限定編集

### Note
- **コンピュータオブジェクト参照機能を追加**: 「コンピュータ参照」タブを新設。Name / DNSHostName / sAMAccountName による検索（2文字以上）、OS フィルタ、description 有無フィルタ、無効端末表示に対応。
- **コンピュータ詳細表示**: Name / SamAccountName / DNSHostName / OperatingSystem / Description / Enabled / DistinguishedName / LastLogon / WhenCreated / WhenChanged を表示。
- **コンピュータ所属グループ表示**: コンピュータの memberOf を取得してグループ一覧を表示・クリップボードコピーに対応。
- **コンピュータ検索結果 CSV 出力**: 検索結果を UTF-8 BOM 付き CSV でエクスポート。
- **description 限定更新**: コンピュータオブジェクトの description 属性のみ更新可能。無効化・OU移動・削除・グループ変更・GPO編集・パスワードリセット・一括更新は実行しない。
- **更新フロー**: 差分確認 → 再認証ダイアログ → ConfirmComputerUpdateDialog（コンピュータ名・DNSHostName・DN・実行端末・起動ユーザー・セッションユーザー・差分表示） → AD再取得・整合性チェック → 更新 → AD再取得して結果表示。
- **セーフガード**: `AllowedComputerOuDns`（空なら `AllowedTargetOuDns` をフォールバック）未設定なら更新不可。`ExcludedComputerNames` に登録した端末は更新不可。空文字更新禁止。Domain Admins セッション必須。
- **AppPolicy 拡張**: `AllowedComputerOuDns` / `ExcludedComputerNames` / `EditableComputerAttributes` / `EffectiveComputerOuDns` を追加。
- **write-audit.jsonl の拡張**: `targetType`（"User" / "Computer"）/ `targetName` / `operationName`（"UpdateUserAttributes" / "UpdateComputerDescription"）フィールドを追加。既存ユーザー更新ログとの後方互換あり。
- **新規モデル・サービス**: `AdComputer` / `AdComputerSearchCriteria` / `IAdComputerAttributeWriteService` / `DirectoryServicesAdComputerAttributeWriteService` / `DirectoryServicesComputerMapper` を追加。
- **InMemory デモデータ**: PC-001（Windows 11）/ PC-002（Windows 10、description なし）/ SRV-001（Windows Server 2022）を追加。
- `docs/validation-computer-edit.md` / `appsettings.ComputerDescriptionEdit.sample.json` を追加。

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

## v0.4.2
### Title
ManageAdTool ユーザー属性限定編集 属性見直し版（メールアドレス / 表示名 / 姓 / 名）

### Note
- **編集対象属性の見直し**: mail / department / title → **mail / displayName / sn / givenName**（メールアドレス / 表示名 / 姓 / 名）に変更。department / title は編集UIから除外（参照表示は継続）。
- **ユーザー名（sAMAccountName）の参照専用化**: UI に読み取り専用テキストボックス（SamAccountNameReadBox）を追加。書き込み対象から永久除外の設計方針を確立。
- **EditableAttributeDefs の追加**: Mail / DisplayName / Surname / GivenName の属性定義（日本語表示名・LDAP属性名）を一元管理するクラスを追加。
- **FieldChange.LdapAttribute の追加**: `write-audit.jsonl` の `changes` に `ldapAttribute` フィールドを追加。監査ログの機械可読性を向上。
- **書き込みサービスのバリデーション強化**: LdapAttribute ベースの許可リスト（"mail"/"displayName"/"sn"/"givenName"）チェックに切り替え。
- **AdUser モデルの拡張**: `Surname`（sn）/ `GivenName`（givenName）プロパティを追加。
- **差分確認プレビューに LDAP属性名を表示**: 「メールアドレス（mail）: ...」形式に変更。
- **整合性チェック・AD再取得検証の更新**: LdapAttribute ベースの switch 式に切り替え。
- `docs/retrospective-v0.4.1.md` を追加（v0.4.1 振り返り・設計議事録）。
- `docs/design-account-expiration.md` を追加（アカウント有効期限設計方針、v0.4.2 では実装しない）。
- **検索結果 CSV に Surname / GivenName を追加**: ヘッダーと値に姓（sn）・名（givenName）の列を追加。
- **write-audit.jsonl の AppVersion を "0.4.2" に更新**: 将来的にはアセンブリバージョンから自動取得する方針。
- `appsettings.UserAttributeEdit.sample.json` の `EditableAttributes` を更新。

## v0.4.1
### Title
ManageAdTool ユーザー属性限定編集 安定化版（UI改善・戻し支援・監査強化）

### Note
- **更新結果表示の改善**: 更新成功時の OutputBox を属性ごとに「変更前・変更後・AD再取得値」を縦並びで表示するよう改善。
- **戻し支援の追加**: 更新成功後に「戻し候補（変更前の値）」を OutputBox に表示し、「戻し用メモをコピー」ボタンでクリップボードにコピーできるようにした。自動ロールバックは実装しない。
- **更新前確認ダイアログの改善**: ConfirmUpdateDialog に対象 DisplayName・実行端末・アプリ起動ユーザー・編集セッションユーザーを追加。変更内容のヘッダー行と前後の色付きテキストボックスを整備。ボタン文言を「この内容でADを更新する」に変更。
- **差分確認状態の明確化**: 「限定更新実行」ボタン無効理由を ViewModel の `WriteButtonDisabledReason` プロパティで管理し、ボタン下に常時表示（未ログイン・期限切れ・OU外・差分未確認など8種類）。
- **write-audit.jsonl の強化**: `targetDisplayName`（対象ユーザーの DisplayName）と `revertCandidate`（変更前の値 Map）を新規追加。
- **エラーメッセージの改善**: 再認証失敗・OU外・除外アカウント・AD値不一致・更新失敗・例外時のユーザー向けメッセージを利用者が行動しやすい文言に改善。例外メッセージの直接表示を廃止。
- **LogPath 書き込み権限チェック**: 起動時に LogPath のディレクトリへの書き込み可能性を検証。書き込み不可の場合は起動時に警告表示。更新実行前には確認ダイアログを表示。参照機能は継続使用可能。
- **ChangeSet に TargetDisplayName 追加**: BuildChangeSet で DisplayName を設定し差分確認表示にも反映。
- `docs/validation-user-edit.md` を v0.4.1 向けに更新（新機能の検証項目を追加）。
- `docs/deploy.md` に v0.4.x 利用時の注意事項を追加。
- `docs/test-record-v0.4.1.md` を追加。

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
