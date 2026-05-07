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

## v0.2.0
### Title
ManageAdTool DirectoryLimitedWrite 検証OU限定書き込み対応

### Note
- DirectoryLimitedWrite ServiceMode を追加し、検証OU配下のユーザーに対する mail / department / title の3属性限定更新に対応。
- DirectoryReadOnly は引き続き完全参照モードとして維持。
- 更新前の再取得、許可OU/除外アカウント確認、差分確認必須、更新後再取得結果表示、serviceMode付き監査ログを追加。
- グループ操作、GPO操作、無効化、退職処理、OU移動、一括処理は未実装のまま維持。
- DirectoryLimitedWrite 用 sample appsettings と検証手順書を追加。
