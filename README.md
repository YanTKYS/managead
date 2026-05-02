# ManageAdTool (MVP)

閉域ネットワーク向けの Active Directory 管理デスクトップツールの MVP です。

## 実装済み
- ADユーザー検索と属性編集（Mail / Department / Title）
- 変更差分確認と実行前確認
- 実行結果表示
- 監査ログ出力（JSON Lines）
- ADコンピュータ検索（Get-ADComputer 相当の検索UI）
- GPO検索・編集（Description / UserSettingEnabled / ComputerSettingEnabled）

## 現在の制約
- `InMemoryAdService` はデモ用です。
- 実運用では `DirectoryServices` と `GroupPolicy` モジュール連携実装に置き換えてください。

## ログ保存先
`C:\ProgramData\ManageAdTool\logs\audit.jsonl`
