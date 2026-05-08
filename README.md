# ManageAdTool (MVP)

閉域ネットワーク向けの Active Directory 確認支援ツール（MVP）です。
**本ツールは参照専用**として、安全側の小さな範囲に限定して育てます。

## 方針（重要）
- 実AD更新機能は実装しません。
- 管理者権限ユーザーやサービスアカウントを使ったAD更新は対象外です。
- グループ追加・削除、GPO編集、ユーザー無効化、退職処理、OU移動、一括更新は実装しません。
- 参照専用でも、AD情報確認、所属グループ確認、問い合わせ対応、事前調査で安全に利用できることを重視します。

## 実装済み（現在のMVP範囲）
- ADユーザー検索（SamAccountName / DisplayName / 氏名 / Mail）
- ユーザー詳細表示
- 所属グループ表示（参照）
- mail / department / title の比較表示（AD更新は実行不可）
- 変更差分確認（比較用途）
- 処理結果欄（テキスト選択・コピー可能）

## ServiceMode
`appsettings.json` の `AppPolicy.ServiceMode` で動作モードを切り替えます。

- `InMemory`
  - デモ・画面確認用モード
  - 実AD接続は行いません
  - AD更新は実行しません
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
- 実AD更新テストでは実行ユーザーのAD書き込み権限不足により「アクセスが拒否されました」となりました。
- 検討の結果、v0.2.0 は書き込み機能ではなく参照専用強化版として進めます。
- 詳細手順は `docs/validation-readonly.md`、`docs/deploy.md` を参照してください。
- リリースZIPでは `ManageAdTool.exe` と実行に必要なファイルを ZIP 展開先直下に配置します。
- リリースZIPは self-contained 形式で .NET Desktop Runtime を同梱するため、利用端末への別途インストールは不要です。

## 現在の制約（重要）
- `DirectoryReadOnly` は読み取り専用であり、AD更新しません。
- 実AD更新機能は今後も実装しない方針です。
- グループ追加・削除、GPO編集、ユーザー無効化、退職処理、OU移動、一括更新は**対象外**です。
- 既存の mail / department / title 画面は比較表示・変更予定確認用途に限定し、更新実行機能は無効化しています。

## 今後の検討事項
- 詳細は `docs/backlog.md` を参照してください。
