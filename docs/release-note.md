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
- グループ追加・削除、GPO編集、ユーザー無効化、退職処理、OU移動、一括更新は未実装のまま維持。
