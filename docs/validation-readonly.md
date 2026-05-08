# DirectoryReadOnly 実AD検証手順

本手順は **DirectoryReadOnly（読み取り専用）** で、実ADへの接続と参照機能のみを検証するためのものです。  
本手順では AD 更新機能の検証は行いません（MVP範囲外）。

## 1. 事前条件
- 閉域端末に ManageAdTool v0.1.0 の成果物が配置済みであること
- 実ADへ参照可能なネットワーク疎通があること
- 検証用ユーザー/OUが事前に用意されていること

## 2. appsettings.json の設定
`AppPolicy.ServiceMode` を `DirectoryReadOnly` に設定します。  
また、`AllowedTargetOuDns` は **検証用OUのみ** に限定してください。

例:
```json
{
  "AppPolicy": {
    "ServiceMode": "DirectoryReadOnly",
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

## 3. 起動と検証観点
1. アプリを起動する
2. 検証対象ユーザーを検索する（SamAccountName / DisplayName / 氏名 / Mail）
3. ユーザー詳細（DistinguishedName / Enabled / userAccountControl / lastLogonTimestamp / accountExpires）が表示されることを確認する
4. 所属グループが名前順で表示され、グループコピーができることを確認する
5. 検索結果CSV出力ができることを確認する
6. **更新ボタンが無効化されていること** を確認する
7. エラーなく処理結果欄に結果が表示されることを確認する

## 4. 判定基準
- 実ADの参照（検索・詳細表示・所属グループ表示）が成功する
- 拡張詳細項目（userAccountControl / lastLogonTimestamp / accountExpires）が表示される
- 検索結果CSV出力と所属グループコピーが成功する
- 更新操作が UI 上で実行不可（更新ボタン無効）である
- AD更新が実行されない


## 5. v0.1.1 実AD読み取り検証結果
- ユーザー情報取得: 成功
- 所属グループ情報取得: 成功
- 検索結果件数表示: 成功
- 実AD更新: 未実施
- 更新可否: `DirectoryReadOnly` モードのため更新不可（更新ボタン無効）

## 6. 注意事項
- 本モードは読み取り専用です。実AD更新の検証は行いません。
- `DirectoryReadOnly` では実AD更新（書き込み）検証を実施しません。グループ操作、GPO操作、無効化、退職処理も本手順の対象外です。
