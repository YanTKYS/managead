# ManageAdTool (MVP)

閉域ネットワーク向けの Active Directory 管理デスクトップツールの MVP です。

## 実装済み
- ユーザー検索（SamAccountName / DisplayName / 氏名 / Mail）
- ユーザー属性の表示と編集（Mail / Department / Title）
- 差分確認
- 実行前確認ダイアログ
- 実行結果表示
- JSON Lines 監査ログ出力

## 現在の制約
- このリポジトリの `InMemoryAdService` はデモ用です。
- 実運用では `DirectoryServicesAdService` を作成し、OU制限・除外アカウント・権限分離を組み込んでください。

## ログ保存先
`C:\ProgramData\ManageAdTool\logs\audit.jsonl`
