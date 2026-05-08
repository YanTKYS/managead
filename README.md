# ManageAdTool

閉域ネットワーク向けの Active Directory 参照支援ツール（v0.3.0）です。  
**v0.3.0 時点では実 AD 更新機能は未実装**です。参照・確認・事前調査を主目的としています。

## 方針（v0.3.0 時点）
- v0.3.0 は参照専用・認証基盤の実装までです。実 AD 更新は行いません。
- グループ追加・削除、GPO 編集、ユーザー無効化、退職処理、OU 移動、一括更新は未実装です。
- 将来的な限定的編集機能は、認証・監査・対象 OU・対象属性の制限を前提に v0.4.0 以降で別途検討します。
- 参照専用でも、AD情報確認・所属グループ確認・問い合わせ対応・事前調査で安全に利用できることを重視します。

## 実装済み機能（v0.3.0）
- AD ユーザー検索（SamAccountName / DisplayName / 氏名 / Mail）
- 検索条件の絞り込み（部署 / Mail 有無 / 無効ユーザー表示）
- ユーザー詳細表示（userAccountControl / lastLogonTimestamp / accountExpires を含む）
- 表示項目を `appsettings.json` の `UserDetailDisplayAttributes` で制御
- 所属グループ一覧表示（名前順）・クリップボードコピー
- グループ検索・グループメンバー一覧表示
- 検索結果 CSV 出力
- 参照ログ（JSON Lines 形式、検索・詳細表示・グループ操作を記録）
- 最大検索件数（`MaxSearchResults`）の設定化と上限到達時の利用者向けメッセージ表示
- mail / department / title の属性表示・変更予定確認（更新実行不可）
- **[v0.3.0 新機能]** 別ユーザーログイン・Domain Admins 判定基盤
  - LDAP バインド認証（パスワード非保持・非ログ）
  - Domain Admins グループメンバー判定（直接 / ネスト検索）
  - 編集セッション管理（設定可能なタイムアウト・自動期限チェック）
  - 認証ログ（`auth.jsonl`）への JSON Lines 記録

## ServiceMode
`appsettings.json` の `AppPolicy.ServiceMode` で動作モードを切り替えます。

- `InMemory`
  - デモ・画面確認用モード
  - 実 AD 接続は行いません
  - AD 更新は実行しません
- `DirectoryReadOnly`
  - 実 AD の**読み取り専用**モード
  - ユーザー検索・詳細表示・所属グループ表示・グループ検索・グループメンバー表示のみ
  - **AD 更新は実行しません（更新ボタン無効）**

## appsettings.json 設定例
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
    "LogPath": "C:\\ProgramData\\ManageAdTool\\logs\\audit.jsonl",
    "MaxSearchResults": 200,
    "UserDetailDisplayAttributes": [
      "SamAccountName",
      "DisplayName",
      "Name",
      "DistinguishedName",
      "Enabled",
      "UserAccountControl",
      "LastLogonTimestamp",
      "AccountExpires"
    ],
    "EditorAuthMode": "DomainAdmins",
    "AdminGroupDn": "CN=Domain Admins,CN=Users,DC=example,DC=local",
    "AllowNestedAdminGroupMembership": false,
    "EditSessionMinutes": 15
  }
}
```

### 認証設定（v0.3.0 追加）

| フィールド | 説明 | デフォルト |
|---|---|---|
| `EditorAuthMode` | `"DomainAdmins"` に設定すると編集者ログイン UI が有効化 | `"None"` |
| `AdminGroupDn` | Domain Admins グループの DistinguishedName | `""` |
| `AllowNestedAdminGroupMembership` | `true` にするとネストグループも判定（LDAP_MATCHING_RULE_IN_CHAIN） | `false` |
| `EditSessionMinutes` | 編集セッションのタイムアウト分数 | `15` |

`EditorAuthMode` が `"DomainAdmins"` 以外または `AdminGroupDn` が未設定の場合、ログイン UI は非対応状態になります。

## ステータス
- v0.3.0 開発中。
- InMemory での起動とテストユーザー情報取得は確認済みです。
- DirectoryReadOnly で実 AD のユーザー情報取得、所属グループ情報取得、グループ検索、グループメンバー表示を確認済みです。
- v0.3.0 の編集者ログイン・Domain Admins 判定は実AD環境での動作検証が必要です。詳細は `docs/validation-auth.md` を参照してください。
- v0.3.0 時点では実 AD 更新は未実装です。比較確認（preview）のみ有効です。
- 詳細手順は `docs/validation-readonly.md`、`docs/deploy.md` を参照してください。
- リリース ZIP は self-contained 形式のため、利用端末への .NET Desktop Runtime の別途インストールは不要です。

## 現在の制約（v0.3.0 時点）
- `DirectoryReadOnly` は読み取り専用であり、AD 更新を行いません。
- v0.3.0 では実 AD 更新機能は未実装です。将来の限定的編集は v0.4.0 以降で検討します。
- グループ追加・削除、GPO 編集、ユーザー無効化、退職処理、OU 移動、一括更新は v0.3.0 の対象外です。
- mail / department / title 画面は属性表示・変更予定確認用途に限定し、更新実行機能は無効化しています。

## 今後の検討事項
- 詳細は `docs/backlog.md` を参照してください。
