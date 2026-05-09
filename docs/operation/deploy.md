# デプロイ手順（閉域端末向け）

本手順は ManageAdTool を閉域端末へ配置し、初回確認を行うための手順です。
本ツールは参照専用のAD確認支援ツールであり、実AD更新は行いません。

## 1. リリース成果物の取得
1. GitHub Releases から対象バージョンの ZIP 成果物を取得する
2. ハッシュ値（必要に応じて）を確認する

## CIビルド運用
- 通常ビルドは PR 作成時には実行せず、`main` への merge 後の push を契機に実行します。
- 必要に応じて GitHub Actions の `workflow_dispatch` から手動実行します。

## 2. 閉域端末への持ち込み
1. 許可された媒体で ZIP 成果物を閉域端末へ搬送する
2. ウイルススキャン/持込手順に従って検査する

## 3. 配置先
1. 閉域端末上の配置先フォルダを作成する（例: `C:\Apps\ManageAdTool`）
2. ZIP を展開する
3. `ManageAdTool.exe` と実行に必要なファイルは ZIP 展開先直下に配置されます（例: `C:\Apps\ManageAdTool\ManageAdTool.exe`）
4. リリース成果物は self-contained 形式のため、対象端末に .NET Desktop Runtime を別途インストールする必要はありません

## 4. appsettings.json の編集
1. 展開先直下で利用モードに応じた sample appsettings をコピーして `appsettings.json` を作成する
   - `appsettings.InMemory.sample.json`
   - `appsettings.DirectoryReadOnly.sample.json`
2. 環境に合わせて OU DN / 除外ユーザー / ログ出力先を調整する
3. DirectoryReadOnly 利用時は `AllowedTargetOuDns` を検証用OUのみに限定する

## 5. InMemory での初回起動確認
1. `ServiceMode` を `InMemory` に設定して起動する
2. 展開先直下の `ManageAdTool.exe` を起動する
3. テストユーザー検索とユーザー詳細表示ができることを確認する
4. 更新実行機能が無効化されていることを確認する

## 6. DirectoryReadOnly 検証への移行
- 実AD接続の読み取り専用検証は `docs/validation-readonly.md` を参照して実施する
- DirectoryReadOnly では更新操作は実行できない（更新ボタン無効）

## 注意（v0.3.0 以前）
- 本ツールは実AD更新を行いません（v0.3.0 以前）。
- 管理者権限ユーザーやサービスアカウントを使った更新実行は対象外です。
- グループ操作、GPO編集、無効化、退職処理、OU移動、一括更新は対象外です。

---

## v0.4.x 利用時の追加注意事項

v0.4.0 以降では Domain Admins セッション + AllowedTargetOuDns 設定により  
mail / department / title の限定編集が可能になります。以下の点に注意してください。

### LogPath の書き込み権限
- アプリ起動ユーザーに `LogPath` のディレクトリへの書き込み権限が必要です
- 不可の場合、起動時に警告が表示されます（参照機能は使用可能）
- 監査ログが記録されない状態では更新実行前に確認ダイアログが表示されます
- ログディレクトリ例: `C:\ProgramData\ManageAdTool\logs\`（初回起動時に自動作成を試みます）

### write-audit.jsonl の保管
- 更新操作のたびに `write-audit.jsonl` に追記されます
- このファイルには更新前後の属性値が記録されます（パスワードは記録されません）
- 定期的にバックアップするか、ログ管理手順に組み込んでください
- 監査ログが保存できなかった場合は `auth.jsonl` に `WriteAuditSaveFailed` が記録されます

### AllowedTargetOuDns の設定
- 必ず **検証用OUのみ** を設定した状態で動作確認を完了してから、本番OUを追加してください
- 空の場合は更新機能全体が無効化されます（セーフガード）
- OU DN の例: `OU=TestUsers,OU=Validation,DC=example,DC=local`

### 更新後の戻し手順
- 更新成功後、画面の「戻し候補」に変更前の値が表示されます
- 「戻し用メモをコピー」ボタンでクリップボードにコピーできます
- 本ツールで同じユーザーを再選択し、変更前の値を入力し直して再度更新することで戻せます
- 自動ロールバック機能はありません（v0.4.x）

### 成果物の閉域への持ち込み時の注意
- ZIP 展開後、`appsettings.UserAttributeEdit.sample.json` をコピーして `appsettings.json` を作成してください
- `AllowedTargetOuDns`・`AdminGroupDn`・`LogPath` は環境に合わせて必ず書き換えてください
- パスワードは appsettings.json に記載しません（起動時のログイン UI で入力する設計です）
