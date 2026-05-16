# docs ガイド

ManageAdTool のドキュメント一覧です。

---

## 自動テスト

`ManageAdTool.Tests` は AD 接続を伴わない単体テストです。設定読み込み、編集ポリシー、ログ読み込み、ChangeSet 生成、InMemory サービスの主要ロジックを対象とし、実 AD 参照・実 AD 更新・WPF UI 自動操作は含めません。

実 AD 環境での動作確認は自動テストでは代替せず、引き続き `operation/validation-*.md` の検証手順に従って実施してください。

---

## roadmap / acceptance / backlog — v1.0.0 後方針

| ファイル | 内容 |
|---|---|
| `roadmap.md` | v1.0.0 後ロードマップ |
| `backlog.md` | v1.0.0 後バックログ |
| `acceptance-test-v0.9.7.md` | v0.9.7 受入テスト版の位置づけ・完了記録 |
| `test-record-v0.9.6.md` | v0.9.6 開発側打鍵テスト要約 |
| `test-record-v0.9.7.md` | v0.9.7 受入テスト結果記録 |

---

## operation/ — 操作・運用・検証・テスト記録

| ファイル | 内容 |
|---|---|
| `deploy.md` | 配布・閉域端末への持ち込み・初回起動手順 |
| `user-manual.md` | 利用者向け操作説明書 |
| `admin-manual.md` | 管理者向け設定・運用手順 |
| `troubleshooting.md` | トラブルシューティング |
| `validation-readonly.md` | 参照機能（DirectoryReadOnly）の検証手順 |
| `validation-auth.md` | 認証機能（Domain Admins ログイン）の旧検証手順（保守記録） |
| `validation-user-edit.md` | ユーザー属性編集の旧検証手順（v1.0.0 では参照専用方針） |
| `validation-computer-edit.md` | コンピュータ description 編集の旧検証手順（v1.0.0 では参照専用方針） |
| `validation-group-edit.md` | グループ参照・旧グループメンバー編集の検証手順（v1.0.0 では参照専用方針） |
| `validation-group-member-edit.md` | 旧グループメンバー編集の補足検証手順（保守記録） |
| `validation-operation-support.md` | オペレーション支援機能の検証手順 |
| `validation-gpo-simulation.md` | GPOシミュレーション機能の検証手順 |
| `validation-log-viewer.md` | ログ確認機能の検証手順 |
| `test-record-v0.1.0.md` | v0.1.0 検証結果記録 |
| `test-record-v0.2.0.md` | v0.2.0 検証結果記録 |
| `test-record-v0.4.1.md` | v0.4.1 検証結果記録 |
| `test-record-v0.7.0.md` | v0.7.0 検証結果記録 |
| `test-record-v0.9.0.md` | v0.9.0 検証結果記録 |
| `test-record-v0.9.6.md` | v0.9.6 開発側打鍵テスト結果記録 |

---

## design/ — 設計メモ

| ファイル | 内容 |
|---|---|
| `design-account-expiration.md` | アカウント有効期限（accountExpires）の設計方針 |
| `retrospective-v0.4.1.md` | v0.4.1 振り返り・設計議事録 |

---

## planning/ — ロードマップ・バックログ・完了履歴

| ファイル | 内容 |
|---|---|
| `roadmap.md` | 保守用ロードマップメモ（正式版は `../roadmap.md`） |
| `backlog.md` | 保守用バックログメモ（正式版は `../backlog.md`） |
| `completed-work.md` | 完了済み作業の詳細履歴（v0.1.0〜） |

---

## release/ — リリースノート

| ファイル | 内容 |
|---|---|
| `release-note.md` | 各バージョンの利用者向けリリース要約（release-build.yml が参照） |
