# ManageAdTool (MVP)

閉域ネットワーク向けの Active Directory 管理支援ツール（MVP）です。  
**現在は安全側の小さな範囲に限定**して実装しています。

## 実装済み（現在のMVP範囲）
- ADユーザー検索（SamAccountName / DisplayName / 氏名 / Mail）
- ユーザー詳細表示
- 所属グループ表示（参照）
- 属性編集対象の限定（mail / department / title）
- 変更差分確認
- 実行前確認ダイアログ
- 監査ログ出力（JSON Lines）
- 処理結果欄（テキスト選択・コピー可能）

## ServiceMode
`appsettings.json` の `AppPolicy.ServiceMode` で動作モードを切り替えます。

- `InMemory`
  - デモ・画面確認用モード
  - 実AD接続は行いません
- `DirectoryReadOnly`
  - 実ADの**読み取り専用**モード
  - ユーザー検索 / 詳細表示 / 所属グループ表示のみ
  - **AD更新は実行しません（更新ボタン無効）**
- `DirectoryLimitedWrite`
  - v0.2.0 向けの検証OU限定・属性限定の最小書き込みモード
  - `AllowedTargetOuDns` 配下のユーザーのみ対象
  - 更新対象は `mail` / `department` / `title` の3属性のみ
  - グループ操作、GPO操作、無効化、退職処理、OU移動、一括処理は実装しません

## 実AD検証時の appsettings.json 設定例
```json
{
  "AppPolicy": {
    "ServiceMode": "DirectoryReadOnly",
    "AllowedTargetOuDns": [
      "OU=Users,DC=example,DC=local"
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


## v0.1.1 ステータス
- v0.1.1 はリリース済みです。
- InMemory での起動とテストユーザー情報取得は確認済みです。
- DirectoryReadOnly で実ADのユーザー情報取得、所属グループ情報取得、検索結果件数表示を確認済みです。
- v0.2.0 では `DirectoryLimitedWrite` による検証OU限定・3属性限定の最小書き込み機能を追加します。
- 詳細手順は `docs/validation-readonly.md`、`docs/validation-limited-write.md`、`docs/deploy.md` を参照してください。
- リリースZIPでは `ManageAdTool.exe` と実行に必要なファイルを ZIP 展開先直下に配置します。
- リリースZIPは self-contained 形式で .NET Desktop Runtime を同梱するため、利用端末への別途インストールは不要です。

## 現在の制約（重要）
- `DirectoryReadOnly` は読み取り専用であり、AD更新しません。
- `DirectoryLimitedWrite` の更新対象は検証OU配下の `mail` / `department` / `title` に限定します。
- グループ追加・削除、GPO操作、ユーザー無効化、退職処理、端末無効化、OU移動、一括処理は**現在のMVP対象外**です。

## 今後の検討事項
- 詳細は `docs/backlog.md` を参照してください。
