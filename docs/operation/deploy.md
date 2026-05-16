# デプロイ手順（閉域端末向け / v0.9.5）

> **v1.0.0 本番初版の方針**: v1.0.0 は参照専用 AD 確認支援ツールです。配布・設定時は `DirectoryReadOnly` による実 AD 参照のみを前提とし、AD 更新・削除・無効化・グループ変更・GPO 編集を有効化しないでください。

本手順は ManageAdTool のリリース ZIP を取得し、閉域端末へ持ち込み、初回起動と実 AD 検証へ進むための手順です。

> **重要**: v1.0.0 は本番初版です。参照専用 AD 確認支援ツールとして提供し、AD 更新・削除・無効化・グループ変更・GPO 編集は行いません。

---

## 1. GitHub Releases から ZIP を取得する

1. GitHub Releases を開きます。
2. 対象バージョン（例: `v0.9.5`）の ZIP 成果物を取得します。
3. 必要に応じて、組織の手順に従いハッシュ値や取得元を記録します。
4. ZIP 名とバージョンが作業予定と一致していることを確認します。

リリース ZIP は self-contained の単一 exe 形式を想定しており、利用端末への .NET Desktop Runtime の別途インストールは不要です。パッケージ直下に大量の DLL が並ばない構成にします。

---

## 2. リリース ZIP の想定構成

ZIP 展開後は、次のような構成を想定しています。

```text
ManageAdTool-vX.Y.Z\
  ManageAdTool.exe
  appsettings.json
  README.md
  config-samples\
  docs\
```

確認ポイント:

- `ManageAdTool.exe` が存在する
- `appsettings.json` が存在する
- `README.md` が存在する
- `config-samples/` が存在する
- `docs/` が存在する
- `docs/operation/user-manual.md` / `admin-manual.md` / `troubleshooting.md` が存在する

---

## 3. 閉域端末へ持ち込む

1. 組織で許可された媒体または手順で ZIP を閉域端末へ搬送します。
2. 持ち込み前後でウイルススキャン等の検査を実施します。
3. 取得元、バージョン、搬送日時、担当者を必要に応じて記録します。

---

## 4. 任意の配置先へ展開する

配置先例:

```text
C:\Apps\ManageAdTool\ManageAdTool-v0.9.5\
```

手順:

1. 閉域端末上に配置先フォルダを作成します。
2. ZIP を展開します。
3. 展開後、`ManageAdTool.exe` と `appsettings.json` が同じフォルダにあることを確認します。
4. `README.md`、`config-samples/`、`docs/` が同梱されていることを確認します。

---

## 5. appsettings.json を環境に合わせて編集する

配布用 `appsettings.json` は安全側の初期値です。

- `EditorAuthMode`: `None`
- `AllowedTargetOuDns`: `[]`
- `AllowedComputerOuDns`: `[]`
- `EditableGroupOuDns`: `[]`
- `LogPath`: `C:\ProgramData\ManageAdTool\logs\audit.jsonl`
- `MaxSearchResults`: `200`
- `MaxLogDisplayRows`: `1000`

実 AD 参照や限定編集を行う場合は、`config-samples/` から用途に合うサンプルをコピーして `appsettings.json` として編集します。`ServiceMode` は `appsettings.json` ではなく起動時ダイアログで選択します。

注意:

- 配布用 `appsettings.json` に本番 OU の DN を直接入れた状態で配布しないでください。
- 組織固有の Domain Admins DN や管理者グループ DN は、配布後に環境別に設定してください。
- 設定変更後はアプリを再起動してください。

詳細は `admin-manual.md` を参照してください。

---

## 6. まず InMemory で起動確認する

1. 起動時ダイアログで `InMemory` を選択します。
2. `ManageAdTool.exe` を起動します。
3. 画面が起動することを確認します。
4. ダミーデータでユーザー、コンピュータ、グループ検索を確認します。
5. 実 AD に接続していないことを前提に、画面操作と CSV 出力等を確認します。

`InMemory` は実 AD 接続・更新を行いません。表示されるデータは実 AD の内容ではありません。

---

## 7. 次に DirectoryReadOnly で参照確認する

1. `config-samples/appsettings.DirectoryReadOnly.sample.json` を参考に `appsettings.json` を作成します。
2. 起動時ダイアログで `DirectoryReadOnly` を選択します。
3. `AllowedTargetOuDns` は検証用 OU の DN から開始します。
4. `LogPath` が書き込み可能な場所であることを確認します。
5. アプリを再起動します。
6. `validation-readonly.md` に従って、実 AD 参照を確認します。

---

## 8. 編集機能は検証用 OU から開始する

編集機能を使う場合は、必ず検証用 OU のみを対象に開始してください。

| 編集機能 | 主な設定 | 検証手順 |
|---|---|---|
| ユーザー属性編集 | `AllowedTargetOuDns`, `EditorAuthMode`, `AdminGroupDn` | `validation-user-edit.md` |
| コンピュータ description 編集 | `AllowedComputerOuDns`, `EditorAuthMode`, `AdminGroupDn` | `validation-computer-edit.md` |
| グループメンバー編集 | `EditableGroupOuDns`, `ProtectedGroupNames`, `ProtectedGroupDns`, `EditorAuthMode`, `AdminGroupDn` | `validation-group-edit.md` |

運用方針:

- `EditorAuthMode: "DomainAdmins"` を原則とします。
- 本番 OU は検証完了後に段階的に追加します。
- `AllowedTargetOuDns` / `AllowedComputerOuDns` / `EditableGroupOuDns` が空の場合、対応する更新機能は無効化されます。
- 保護グループと除外対象を先に設定してください。

---

## 9. ログ出力先の権限を確認する

既定例:

```text
C:\ProgramData\ManageAdTool\logs\audit.jsonl
```

確認するログ:

| ログ | 内容 |
|---|---|
| `audit.jsonl` | 参照操作、検索、GPOシミュレーション、オペレーション支援 |
| `auth.jsonl` | Domain Admins 認証の成否 |
| `write-audit.jsonl` | AD 更新操作の監査 |

確認ポイント:

- アプリ実行ユーザーがログフォルダを作成・追記できる
- ログ保存先が一般利用者から不用意に閲覧されない
- CSV 出力先も内部情報として管理される

---

## 10. 参照する資料

| 目的 | 資料 |
|---|---|
| 利用者向け操作手順 | `user-manual.md` |
| 管理者向け設定・運用 | `admin-manual.md` |
| トラブル対応 | `troubleshooting.md` |
| 実 AD 参照検証 | `validation-readonly.md` |
| Domain Admins 認証検証 | `validation-auth.md` |
| ユーザー属性編集検証 | `validation-user-edit.md` |
| コンピュータ description 編集検証 | `validation-computer-edit.md` |
| グループメンバー編集検証 | `validation-group-edit.md` |
| GPOシミュレーション検証 | `validation-gpo-simulation.md` |
| オペレーション支援検証 | `validation-operation-support.md` |
| ログ確認検証 | `validation-log-viewer.md` |

---

## 11. CI / release-build 運用メモ

- 通常ビルドは `main` への push と手動実行を前提とします。
- `build.yml` に `pull_request` トリガーは追加しません。
- リリース ZIP は `release-build.yml` の `workflow_dispatch` からバージョンを指定して作成します。
