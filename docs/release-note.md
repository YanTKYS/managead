# Release Notes

## v0.1.0
### Title
ManageAdTool MVP 初版（InMemory / DirectoryReadOnly 対応）

### Note
- WPFベースのMVP画面を追加（ユーザー検索、詳細表示、mail/department/title編集、差分確認、実行前確認、監査ログ出力）。
- InMemoryAdService を追加し、閉域検証向けにデモ運用を可能化。
- DirectoryServicesAdReadService（読み取り専用）を追加し、実AD検索/詳細/所属グループ表示に対応。
- AppPolicy / appsettings.json による AllowedTargetOuDns・ExcludedSamAccountNames・EditableAttributes・ServiceMode などの制御を実装。
- GitHub Actions に通常ビルド・リリースビルド（手動、version入力）を追加。
