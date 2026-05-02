# デプロイ手順（閉域端末向け）

本手順は ManageAdTool v0.1.0 を閉域端末へ配置し、初回確認を行うための手順です。

## 1. リリース成果物の取得
1. GitHub Releases から対象バージョンの ZIP 成果物を取得する
2. ハッシュ値（必要に応じて）を確認する

## 2. 閉域端末への持ち込み
1. 許可された媒体で ZIP 成果物を閉域端末へ搬送する
2. ウイルススキャン/持込手順に従って検査する

## 3. 配置先
1. 閉域端末上の配置先フォルダを作成する（例: `C:\Apps\ManageAdTool`）
2. ZIP を展開する

## 4. appsettings.json の編集
1. `appsettings.InMemory.sample.json` もしくは `appsettings.DirectoryReadOnly.sample.json` をコピーして `appsettings.json` を作成する
2. 環境に合わせて OU DN / 除外ユーザー / ログ出力先を調整する
3. DirectoryReadOnly 利用時は `AllowedTargetOuDns` を検証用OUのみに限定する

## 5. InMemory での初回起動確認
1. `ServiceMode` を `InMemory` に設定して起動する
2. テストユーザー検索とユーザー詳細表示ができることを確認する

## 6. DirectoryReadOnly 検証への移行
- 実AD接続の読み取り専用検証は `docs/validation-readonly.md` を参照して実施する
- DirectoryReadOnly では更新操作は実行できない（更新ボタン無効）

## 注意
- 本バージョンでは実AD更新機能（書き込み）は未実装。
- グループ操作、GPO操作、無効化、退職処理はMVP範囲外。
