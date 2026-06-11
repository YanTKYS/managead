# ManageAdTool

閉域ネットワーク向けの **Active Directory 参照・限定更新支援ツール（v1.0.1）** です。

> **重要**: 本ツールは `DirectoryReadOnly` による実 AD 参照と、Domain Admins 認証後の限定的な属性更新（mail / displayName / sn / givenName / description）およびグループメンバー管理を行います。ユーザー無効化・退職処理・OU 移動・グループ作成削除・GPO 編集は行いません。

---

## v1.0.1 UI改善版

v1.0.1 では機能追加は行わず、WPF 画面の UI 改善を実施しました。

- 画面上部にヘッダーバンドを追加し、現在のモード（DirectoryReadOnly: 「実AD参照・認証後に限定更新可能」 / InMemory: 「テスト用・実AD未接続」）を常時表示
- 共通スタイル（`Styles/Theme.xaml`）を追加し、ボタン・DataGrid・TextBox の見た目を統一
- 検索バーをカード風レイアウトに変更し、各タブの操作導線を整理
- ユーザー詳細・グループ詳細・コンピュータ詳細の表示を見やすくした
- グループ参照にメンバー件数表示を追加
- ユーザー詳細タブの差分確認・限定更新実行ボタンを常時表示領域に配置（ウィンドウ縮小時も操作可能）

---

## v1.0.0 本番初版の位置づけ

- v0.9.6 で開発側打鍵テストを実施済みです。
- v0.9.7 で実運用担当者による受入テストを実施済みです。
- DirectoryReadOnly で実 AD のユーザー検索、属性表示、所属グループ表示、コンピュータ参照、グループ参照が大きな問題なく確認されました。
- v1.0.0 は、上記の確認結果を踏まえた本番初版です。
- 今後も参照専用方針を維持し、AD 更新系の機能は原則として追加しません。

---

## v1.0.0 時点の主な機能

### 参照
- ユーザー検索
- ユーザー属性表示
- 所属グループ表示
- コンピュータ参照
- グループ参照
- 検索結果件数表示
- 検索結果 CSV 出力
- 未ログイン確認（ユーザー / コンピュータ）
- GPO シミュレーション（OU リンク参照の簡易表示）
- 参照ログ・認証ログ・書き込みログの閲覧

### ヘルプ表示
- アプリ画面の「ヘルプを開く」ボタンから `help/index.html` を既定ブラウザで開けます。
- `help/index.html` はローカル HTML です。外部 CDN やインターネット接続は使用しません。

---

## できないこと / 行わないこと

v1.0.0 では、次の操作は実装・提供しません。

- AD 更新
- AD オブジェクト削除
- ユーザー無効化
- 退職処理
- OU 移動
- グループ追加・削除
- グループメンバー変更
- GPO 編集
- パスワードリセット
- 一括更新処理

理由:

- 参照専用ツールとして安全に展開する方針のため
- AD 更新権限や監査・承認・復旧設計が必要になるため
- 本ツールの利用者層と運用リスクが合わない可能性があるため

---

## 起動時の ServiceMode 選択

`ServiceMode` は `appsettings.json` へ記入せず、起動時の選択ダイアログで今回の起動モードを選びます。

| 値 | 用途 | 説明 |
|---|---|---|
| `InMemory` | デモ・画面確認用 | 実 AD に接続しません。サンプルデータで画面確認・操作練習を行います。 |
| `DirectoryReadOnly` | 実 AD 参照用 | 実 AD の情報を参照します。AD 更新は行いません。 |

本番運用では `DirectoryReadOnly` を使用し、事前確認や説明時は `InMemory` で画面動作を確認してください。

---

## リリース ZIP の構成

リリース ZIP は self-contained の単一 exe 形式を想定しており、利用端末への .NET Desktop Runtime の別途インストールは不要です。ルート直下に大量の DLL が並ばないよう、展開後は次の構成を想定しています。

```text
ManageAdTool-vX.Y.Z/
  ManageAdTool.exe
  appsettings.json
  README.md
  help/
    index.html
    style.css
  config-samples/
  docs/
```

配布用 `appsettings.json` は安全側初期値です。実 AD 参照を行う場合は、管理者が環境に合わせて OU DN、除外ユーザー名、ログ出力先などを設定してください。起動モードは起動時ダイアログで選択します。

---

## 基本的な使い方

1. 管理者が環境に合わせて `appsettings.json` を準備する
2. `ManageAdTool.exe` を起動する
3. 起動時ダイアログで `InMemory` または `DirectoryReadOnly` を選択する
4. 実 AD 確認時は `DirectoryReadOnly` を選び、ユーザー詳細、コンピュータ参照、グループ参照などのタブで検索する
5. 操作に迷う場合は、アプリ画面の「ヘルプを開く」または `help/index.html` を参照する

> **利用方針**: 必ず検証用 OU や確認済み設定で動作確認を完了してから、本番参照範囲を設定してください。  
> v1.0.0 は参照専用です。AD 更新・削除・無効化・グループ変更・GPO 編集は行いません。

---

## 主な設定項目

| フィールド | 説明 | デフォルト |
|---|---|---|
| `AllowedTargetOuDns` | ユーザー参照対象 OU | `[]` |
| `ExcludedSamAccountNames` | 除外ユーザーアカウント | `[]` |
| `AllowedComputerOuDns` | コンピュータ参照対象 OU | `[]` |
| `ExcludedComputerNames` | 除外コンピュータ名 | `[]` |
| `MaxSearchResults` | 検索上限件数 | `200` |
| `MaxLogDisplayRows` | ログ確認タブの最大表示行数（末尾から取得） | `1000` |
| `LogPath` | 監査ログ出力先（audit.jsonl / auth.jsonl / write-audit.jsonl） | `C:\ProgramData\ManageAdTool\logs\audit.jsonl` |
| `EnableOperationSupport` | `false` にするとオペレーション支援タブを非表示にします | `true` |

---

## パスワードの扱い

- appsettings.json にパスワードを保存しません。
- ログ（audit.jsonl / auth.jsonl / write-audit.jsonl）にパスワードを記録しません。
- v1.0.0 は参照専用運用のため、AD 更新のためのパスワード保存方式は採用しません。

---

## ヘルプと操作・運用資料

- **利用者向けヘルプ**: `help/index.html` を既定ブラウザで開いて参照してください。アプリ画面の「ヘルプを開く」ボタンからも開けます。
- **開発・運用者向け資料**: `docs/` 配下の Markdown を参照してください。検証記録、受入テスト結果、ロードマップ、バックログ、運用手順を管理します。
- 管理者向け設定手順は `docs/operation/admin-manual.md` を参照してください。
- トラブル時は `docs/operation/troubleshooting.md` を参照してください。

---

## 自動テストとビルド運用

- `ManageAdTool.Tests` は AD 接続を伴わない単体テストです。
- 実 AD 環境での確認は `DirectoryReadOnly` で行います。
- PR 時のビルドは行わず、main merge 後の GitHub Actions build を確認する運用です。
- `release-build.yml` は手動実行で `v1.0.0` などのリリース ZIP を作成します。

```bash
dotnet test ManageAdTool.Tests/ManageAdTool.Tests.csproj
dotnet build ManageAdTool.csproj -c Release
```

---

## ドキュメント

| ファイル | 内容 |
|---|---|
| `help/index.html` | 一般利用者向けの単独ヘルプ（既定ブラウザで表示） |
| `docs/roadmap.md` | v1.0.0 後のロードマップ |
| `docs/backlog.md` | v1.0.0 後のバックログ |
| `docs/acceptance-test-v0.9.7.md` | v0.9.7 受入テスト方針・完了記録 |
| `docs/test-record-v0.9.7.md` | v0.9.7 受入テスト結果記録 |
| `docs/operation/deploy.md` | 配布・閉域端末への持ち込み手順 |
| `docs/operation/user-manual.md` | 利用者向け操作説明書（詳細版） |
| `docs/operation/admin-manual.md` | 管理者向け設定・運用手順 |
| `docs/operation/troubleshooting.md` | トラブルシューティング |
| `docs/release/release-note.md` | リリースノート |

> リリース ZIP は self-contained 形式のため、利用端末への .NET Desktop Runtime の別途インストールは不要です。
