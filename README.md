# ManageAdTool (MVP)

閉域ネットワーク向けの Active Directory 管理デスクトップツールの MVP です。

## 実装済み
- ADユーザー検索（氏名からログインID検索を含む）、属性編集（Mail / Department / Title）、所属グループの追加・削除、グループ直接所属メンバーの表示/追加/削除（直接参加のみ）
- 変更差分確認と実行前確認
- 期限切れアカウント一覧表示、Ctrl複数選択で有効期限延長/無効化（無効化は警告ダイアログ）
- XX日以上未ログインのユーザ一覧表示と複数無効化（警告ダイアログ）
- XX日以上未起動の端末一覧表示と複数無効化（警告ダイアログ）
- 無効ユーザ一覧表示と複数選択退職処理（appsettings.json の RetiredUsersOuDn へ移動）
- 実行結果表示（処理結果欄・テキスト選択可・コピーボタン付き）
- ユーザー詳細で最終ログオン日時と最終ログオンPC情報を表示
- 監査ログ出力（JSON Lines）
- ADコンピュータ検索（部分一致）と、選択時の最終起動日時・最終ログインユーザ表示
- GPO検索・編集（Description / UserSettingEnabled / ComputerSettingEnabled）
- グループごとの適用中GPO状況表示とCSV出力
- ADユーザー名 + ADコンピュータ指定での適用中GPO状況表示とCSV出力
- `appsettings.json` でOU DN、除外アカウント、ログパスを外だし設定
- ログイン中ユーザー/端末/ドメイン情報から `appsettings.json` を自動補完

## 設定ファイル
`appsettings.json` の `AppPolicy.AllowedTargetOuDns` に許可OU DNを設定してください。

## 現在の制約
- `InMemoryAdService` はデモ用です。
- 実運用では `DirectoryServices` と `GroupPolicy` モジュール連携実装に置き換えてください。

## ログ保存先
`appsettings.json` の `AppPolicy.LogPath`


## 自動補完
UIの「現在ユーザー/PCで設定補完」ボタンで、`AllowedTargetOuDns` と `DetectedContext` を自動反映できます。
