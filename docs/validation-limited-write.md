# DirectoryLimitedWrite 検証OU限定 書き込み検証手順

本手順は v0.2.0 の `DirectoryLimitedWrite` モードで、検証OU配下の検証用ユーザーに対して **mail / department / title の3属性のみ** を更新検証するためのものです。

## 1. 前提
- 検証用OUが用意されていること
- 検証用OU配下に、更新してよい検証用ユーザーが用意されていること
- 本番ユーザーでは実施しないこと
- グループ操作、GPO操作、無効化、退職処理、OU移動、一括処理は実施しないこと

## 2. appsettings.json の設定
`ServiceMode` を `DirectoryLimitedWrite` に設定します。  
`AllowedTargetOuDns` は **検証用OUのみ** に限定してください。

```json
{
  "AppPolicy": {
    "ServiceMode": "DirectoryLimitedWrite",
    "AllowedTargetOuDns": [
      "OU=ValidationUsers,OU=ManageAdTool,DC=example,DC=local"
    ],
    "ExcludedSamAccountNames": [
      "administrator",
      "krbtgt"
    ],
    "EditableAttributes": ["mail", "department", "title"],
    "LogPath": "C:\\ProgramData\\ManageAdTool\\logs\\audit.jsonl"
  }
}
```

## 3. 検証手順
1. アプリを起動する
2. 検証用ユーザーを検索する
3. 検索結果件数が処理結果欄に表示されることを確認する
4. 検証用ユーザーを選択し、更新前の `mail` / `department` / `title` を記録する
5. `mail` / `department` / `title` のみを変更する
6. 差分確認を実行し、変更差分を確認する
7. 更新実行前の確認ダイアログで対象と差分を再確認する
8. 更新を実行する
9. 更新後に再取得された `mail` / `department` / `title` が処理結果欄に表示されることを確認する
10. AD側でも更新後の値を確認し、更新前後の値を記録する

## 4. 記録欄
| 項目 | 更新前 | 更新後 | 備考 |
| --- | --- | --- | --- |
| mail |  |  |  |
| department |  |  |  |
| title |  |  |  |

## 5. 禁止事項
- 本番ユーザーでは実施しない
- 検証OU以外を `AllowedTargetOuDns` に指定しない
- `mail` / `department` / `title` 以外の属性更新を行わない
- グループ操作、GPO操作、無効化、退職処理、OU移動、一括処理を行わない
