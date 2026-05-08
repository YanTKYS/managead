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

## 注意
- 本ツールは実AD更新を行いません。
- 管理者権限ユーザーやサービスアカウントを使った更新実行は対象外です。
- グループ操作、GPO編集、無効化、退職処理、OU移動、一括更新はMVP範囲外です。
