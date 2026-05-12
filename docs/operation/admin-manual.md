# ManageAdTool 管理者向け設定・運用手順（v0.9.5）

対象読者: ManageAdTool の配布・設定・保守を行う管理者

> **重要**: 本書は設定・運用手順を整理するものです。v0.9.5 ではリリースパッケージ・配布物を整理しています。新しい AD 更新機能や AD 操作ロジックの変更はありません。

---

## 1. 配置先例

閉域端末または管理用端末に、配布 ZIP を展開して利用します。

配布 ZIP の推奨構成:

```text
ManageAdTool-vX.Y.Z\
  ManageAdTool.exe
  appsettings.json
  README.md
  config-samples\
  docs\
```

配置例:

```text
C:\Tools\ManageAdTool\ManageAdTool-v0.9.5\
  ManageAdTool.exe
  appsettings.json
  README.md
  config-samples\
  docs\
```

運用上の注意:

- 配布用 `appsettings.json` は安全側初期値です。配布後に環境別に編集してください。
- ログ出力先は書き込み権限があり、情報管理できるパスを指定してください。
- 利用者がサンプル設定を誤って本番設定として使わないよう、配布前に確認してください。
- 実 AD 接続用設定は、`config-samples/` から用途に合うサンプルをコピーして `appsettings.json` として編集する運用にしてください。

---

## 2. appsettings.json の基本構成

`appsettings.json` の `AppPolicy` セクションで対象 OU、除外対象、ログ出力先、認証設定などを指定します。ServiceMode は起動時ダイアログで選択します。配布直後の `appsettings.json` は OU 許可リスト空、`EditorAuthMode: "None"` の安全側初期値です。

DirectoryReadOnly / 限定編集を有効にする場合の代表的な構成例:

```json
{
  "AppPolicy": {
    "AllowedTargetOuDns": ["OU=Users,DC=example,DC=local"],
    "AllowedComputerOuDns": ["OU=Computers,DC=example,DC=local"],
    "EditableGroupOuDns": ["OU=Groups,DC=example,DC=local"],
    "ProtectedGroupNames": ["Domain Admins"],
    "ProtectedGroupDns": ["CN=Domain Admins,CN=Users,DC=example,DC=local"],
    "ExcludedSamAccountNames": ["svc.example"],
    "ExcludedComputerNames": ["PC-DO-NOT-EDIT"],
    "EditorAuthMode": "DomainAdmins",
    "AdminGroupDn": "CN=Domain Admins,CN=Users,DC=example,DC=local",
    "EditSessionMinutes": 15,
    "MaxSearchResults": 200,
    "MaxLogDisplayRows": 1000,
    "LogPath": "C:\\ProgramData\\ManageAdTool\\logs\\audit.jsonl",
    "EnableOperationSupport": true
  }
}
```

設定変更後は、アプリの再起動が必要です。

---

## 3. config-samples の使い分け

| ファイル | 用途 |
|---|---|
| `appsettings.InMemory.sample.json` | 画面確認・教育・デモ用。実 AD 接続なし。 |
| `appsettings.DirectoryReadOnly.sample.json` | 実 AD 参照を確認する初期検証用。 |
| `appsettings.UserAttributeEdit.sample.json` | ユーザー属性編集を検証用 OU に限定して有効化する例。 |
| `appsettings.ComputerDescriptionEdit.sample.json` | コンピュータ description 編集を検証用 OU に限定して有効化する例。 |
| `appsettings.GroupMembershipEdit.sample.json` | グループメンバー追加・削除を検証用 OU に限定して有効化する例。 |

運用開始時は、サンプルをそのまま使わず、必ず自組織の DN、ログパス、除外対象、保護グループを確認してください。用途に合うサンプルをコピーして `appsettings.json` にリネームし、検証用 OU から段階的に設定してください。

---

## 4. 主な設定項目

### 起動時 ServiceMode 選択

| 値 | 説明 |
|---|---|
| `InMemory` | デモ・画面確認用。実 AD 接続・更新なし。 |
| `DirectoryReadOnly` | 実 AD 参照 + Domain Admins セッション有効時の限定更新。 |

ServiceMode は `appsettings.json` へ記入せず、起動時ダイアログで選択します。

### AllowedTargetOuDns

ユーザー属性編集を許可する OU DN の一覧です。

- 空の場合、ユーザー更新はできません。
- まず検証用 OU のみを指定してください。
- 本番 OU は検証完了後に追加してください。

### AllowedComputerOuDns

コンピュータ description 編集を許可する OU DN の一覧です。

- 空の場合、コンピュータ更新はできません。
- 未設定時の実効 OU は、実装上 `AllowedTargetOuDns` を参照する場合があります。運用上は誤解を避けるため、コンピュータ編集を使う場合は明示設定してください。

### EditableGroupOuDns

グループメンバー編集を許可するグループ OU DN の一覧です。

- 空の場合、グループメンバー更新はできません。
- 更新できるのはユーザーの追加・削除のみです。

### ProtectedGroupNames / ProtectedGroupDns

編集禁止にするグループ名または DN です。

- Domain Admins など高権限グループは必ず保護対象にしてください。
- 名前だけでなく DN でも保護すると、同名グループや表示揺れに強くなります。

### ExcludedSamAccountNames

ユーザー編集対象から除外する sAMAccountName の一覧です。

- サービスアカウント、共有アカウント、特権アカウントを登録してください。
- 大文字・小文字の違いに依存しない運用を推奨します。

### ExcludedComputerNames

コンピュータ編集対象から除外するコンピュータ名の一覧です。

- サーバー、重要端末、運用対象外端末を登録してください。

### EditorAuthMode

編集時の認証モードです。

- 編集機能を使う場合は、`DomainAdmins` を原則とします。Domain Admins 認証 UI を有効化し、セッション期限付きで限定更新を実行します。
- `None` はデモ・参照中心の運用または検証用途向けです。実 AD 更新を行う運用では、安全性を考慮して `DomainAdmins` を使用してください。

### AdminGroupDn

Domain Admins グループの DN です。

- `EditorAuthMode: "DomainAdmins"` の場合に使用します。
- 実 AD の DN と完全一致するよう確認してください。

### EditSessionMinutes

編集セッションの有効時間（分）です。

- 短すぎると作業中に再認証が頻発します。
- 長すぎると離席時のリスクが高まります。

### MaxSearchResults

検索結果の最大件数です。

- 大きすぎる値は操作性と負荷に影響します。
- 0 以下や不正型は既定値にフォールバックします。

### MaxLogDisplayRows

ログ確認タブで表示する最大行数です。

- 大きなログでも末尾から指定件数を読み込みます。
- 0 以下や不正型は既定値にフォールバックします。

### LogPath

参照ログ `audit.jsonl` の出力先です。

- `auth.jsonl` と `write-audit.jsonl` は `LogPath` と同じフォルダに出力されます。
- フォルダに書き込み権限が必要です。

### EnableOperationSupport

オペレーション支援タブの表示を制御します。

- `true`: 表示
- `false`: 非表示

`OperationChecklistItems` は設定から読み込まれますが、現バージョンではチェックリスト UI とサマリーには反映されません。

---

## 5. ログファイルの種類

| ログ | 出力先 | 内容 |
|---|---|---|
| `audit.jsonl` | `LogPath` | 参照操作、検索、GPOシミュレーション、オペレーション支援など |
| `auth.jsonl` | `LogPath` と同じフォルダ | Domain Admins 認証の成否 |
| `write-audit.jsonl` | `LogPath` と同じフォルダ | ユーザー属性、コンピュータ description、グループメンバー更新の監査 |

ログは JSON Lines 形式です。1 行が 1 イベントを表します。

---

## 6. ログファイルの取り扱い注意

ログには次の情報が含まれる場合があります。

- AD 属性値
- DN
- 操作者
- 端末名
- 対象ユーザー / コンピュータ / グループ
- 変更前後の値
- エラー情報

取り扱い方針:

- 一般利用者が閲覧できる共有フォルダに置かないでください。
- CSV 出力したログも同じ機密レベルで扱ってください。
- 本ツールはログ改ざん検知、ログローテーション、外部 DB 保存を行いません。必要に応じて運用側で補完してください。

---

## 7. Domain Admins 認証の扱い

- 編集操作には Domain Admins セッションが必要です。
- パスワードは認証・AD 書き込みに即時使用し、appsettings.json やログに保存しません。
- セッションは `EditSessionMinutes` で期限切れになります。
- 認証失敗は `auth.jsonl` に記録されますが、パスワードは記録されません。

---

## 8. 検証用 OU から始める運用手順

1. `InMemory` で画面操作を確認します。
2. `DirectoryReadOnly` で実 AD 参照のみを確認します。
3. 検証用 OU のみを `AllowedTargetOuDns` / `AllowedComputerOuDns` / `EditableGroupOuDns` に指定します。
4. `validation-*.md` に従い、参照・認証・各限定編集・ログ確認を検証します。
5. 監査ログと戻し手順が運用要件を満たすことを確認します。
6. 必要に応じて本番 OU を段階的に追加します。
7. 設定変更後はアプリを再起動し、再度対象 OU を確認します。

---

## 9. 管理者向けチェックリスト

- [ ] `appsettings.json` の DN が実 AD と一致している
- [ ] 検証用 OU から開始している
- [ ] 保護グループを `ProtectedGroupNames` / `ProtectedGroupDns` に設定している
- [ ] 除外ユーザー・除外コンピュータを設定している
- [ ] ログ出力先に書き込み権限がある
- [ ] ログ保存先のアクセス制御を確認している
- [ ] Domain Admins 認証の有効時間が妥当である
- [ ] 設定変更後にアプリを再起動している
