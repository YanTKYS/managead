# Backlog

## v0.2.0 方針（参照専用強化版）
- 実AD更新機能は実装しない。
- DirectoryServicesAdWriteService は実装しない方針。
- 管理者権限ユーザーやサービスアカウントを使った更新実行は対象外。
- グループ追加・削除、GPO編集、ユーザー無効化、退職処理、OU移動、一括更新は対象外。

## v0.2.0 完了
- ユーザー詳細表示項目の拡充（lastLogonTimestamp、accountExpires、userAccountControl の読み取り表示）。
- 所属グループ表示の一覧化・名前順並び替え・コピー性向上。
- グループ検索。
- グループメンバー一覧表示。
- 参照ログ（検索・詳細表示・グループ表示の記録）。
- 検索条件の追加（部署、メール有無、無効ユーザー表示など）。
- 表示項目を `appsettings.json` で制御。
- 最大検索件数（`MaxSearchResults`）の設定化と上限超過時の利用者向けメッセージ表示（ユーザー検索・グループ検索・グループメンバー）。
- ServiceModeごとのUI表示制御を MainWindowViewModel に移動（IsReadOnlyMode / CanEdit / EditControlsEnabled）。
- mail / department / title の比較 GroupBox を「属性表示・変更予定確認」として用途を明確化。
- PropertiesToLoad 最適化：SearchUsers・GetGroupMembers は memberOf を取得しない検索用プロパティセットを使用し、GetUser のみ memberOf を含む詳細用プロパティセットを使用。
- 例外メッセージの利用者向け/管理者向け分離：UI には利用者向けラッパーメッセージ、参照ログには InnerException を含む技術詳細を記録。
- コードベース整理：DirectoryServicesAdLimitedWriteService / AppSettingsBootstrapper / AuditLogService / InMemoryAdService.Future 等の不要ファイルを削除。csproj の除外エントリも削除。
- バグ修正：InMemoryAdService.GetGroupMembers が DistinguishedName 渡しで空を返す問題を修正（DN→グループ名への解決を追加）。
- フィールド命名規約修正：DirectoryServicesAdReadService.Policy → _policy。
- ドキュメント整備：test-record-v0.2.0.md を追加、validation-readonly.md の設定例に MaxSearchResults を追記。
- IAdFutureOperations インターフェースと GPO・コンピューター管理モデル（GpoPolicy / GroupGpoStatus / TargetGpoStatus / AdComputer）を削除。InMemoryAdService を IAdService のみの実装に整理（285行 → 100行）。
- README.md を v0.2.0 向けに更新（DirectoryLimitedWrite 記述削除・v0.2.0 機能一覧・設定例を刷新）。

## v0.3.0 完了
- 別ユーザーログイン・Domain Admins 判定基盤の追加（EditorAuthService / AuthAuditLogger / EditorSession / AuthResult）。
- 編集者ログイン UI（domain\user / PasswordBox / ログイン・ログアウトボタン / セッション状態表示）。
- LDAP バインド認証（パスワード非保持・非ログ）と Domain Admins グループメンバー判定（直接 / ネスト対応）。
- 編集セッション管理（AppPolicy: EditorAuthMode / AdminGroupDn / AllowNestedAdminGroupMembership / EditSessionMinutes）。
- DispatcherTimer による 30 秒周期のセッション期限自動チェック。
- 認証ログ（auth.jsonl）への JSON Lines 記録。
- UserEditPolicyService.Evaluate に isSessionActive パラメーターを追加。
- MainWindowViewModel にセッション状態プロパティ・メソッドを追加。
- docs/roadmap.md を追加。

## v0.4.0 完了
- ユーザー属性限定編集機能の追加（mail / department / title のみ・検証版）。
- 更新前: 差分確認 → 再認証ダイアログ（パスワード再入力） → 実行前確認ダイアログ → AD再取得・整合性チェック。
- 更新後: AD再取得して処理結果欄に表示。
- 書き込み監査ログ（write-audit.jsonl）の追加（パスワード非記録）。
- DirectoryServicesAdUserAttributeWriteService / IAdUserAttributeWriteService を追加。
- WriteAuditLogger / WriteAuditEntry / UpdateResult を追加。
- UserEditPolicyService に EvaluateWrite を追加。
- ReAuthDialog / ConfirmUpdateDialog を追加。
- AllowedTargetOuDns 未設定時は更新不可とする制限を実装。
- 空文字への更新禁止を実装。
- EditInput_TextChanged で差分確認状態をクリア（入力変更時に再確認を促す）。
- MainWindowViewModel に IsWriteButtonEnabled / SetPendingReady を追加。
- ServiceMode は追加せず DirectoryReadOnly + セッション有効 + OU設定で更新可能とした。
- パスワードは appsettings.json / ログへの保存なし。更新実行時に再認証ダイアログで入力・即時使用・破棄。
- docs/validation-user-edit.md / appsettings.UserAttributeEdit.sample.json を追加。
- README / backlog / release-note / roadmap を v0.4.0 向けに更新。

## Next
- v0.4.0 の実AD環境での限定編集動作検証（docs/validation-user-edit.md）。
- DirectoryServicesAdReadService の検索パフォーマンス検証（実AD環境での PageSize・SizeLimit の効果測定）。

## AD実運用検討（キャッシュ）
- 目的: ユーザー選択切替ごとの `memberOf` 再取得を減らし、DC負荷を抑制する。
- 候補案:
  - セッション内メモリキャッシュ（キー: SamAccountName、値: グループ一覧、TTL: 30〜120秒）
  - 検索結果一覧表示時に必要属性を先読みし、詳細画面で再問い合わせを抑止
  - 明示的リフレッシュボタンで最新取得を強制（通常はキャッシュ利用）
- 運用観点:
  - TTLを短くして鮮度を担保
  - キャッシュヒット率・LDAP呼出回数をログで可視化
- 実装時期:
  - DirectoryReadOnly 実AD運用の初期検証後に効果測定して導入可否を判断
