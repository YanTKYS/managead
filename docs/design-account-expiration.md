# アカウント有効期限（accountExpires）設計方針

## 概要

本文書は Active Directory の `accountExpires` 属性に関する設計方針と、  
ManageAdTool での扱いを記録するものです。

---

## 現在の扱い（v0.4.2 時点）

- **参照のみ**: `accountExpires` は `AdUser.AccountExpiresAt`（`DateTimeOffset?`）として読み取り表示する
- **編集不可**: `appsettings.json` の `EditableAttributes` に含まれない限り、更新対象にしない
- **UI**: `UserDetailDisplayAttributes` に `"AccountExpires"` を追加することで詳細表示欄に表示できる
- **v0.4.2 では実装しない**: 編集 UI への追加・更新機能は v0.4.2 の対象外

---

## accountExpires の仕様

### 値の形式
- LDAP 属性型: `Large Integer`（64ビット整数）
- 単位: 100ナノ秒刻み（Windows ファイルタイム）
- 基準日: 1601年1月1日 00:00:00 UTC

### 特殊値
| 値 | 意味 |
|---|---|
| `0` | 有効期限なし（Never expires）|
| `9223372036854775807`（`Int64.MaxValue`）| 有効期限なし（旧来の表現）|
| その他 | 有効期限日時（Windows ファイルタイムとして変換）|

### ManageAdTool での変換ロジック（DirectoryServicesUserMapper）
```csharp
// accountExpires の読み取り変換
var raw = GetLong(r, "accountExpires");
AccountExpiresAt = (raw is null or 0 or long.MaxValue)
    ? null
    : DateTimeOffset.FromFileTime(raw.Value);
```

---

## v0.4.x での実装しない理由

1. **高リスク属性**: 誤った値を設定するとアカウントが即座にロックされる可能性がある
2. **変換の複雑さ**: 0 / MaxValue の特殊値・タイムゾーン変換・UI での日付入力バリデーションが必要
3. **v0.4.x の方針**: 安定化フェーズ。新規高リスク機能は追加しない
4. **代替手段の存在**: ADUC・PowerShell で安全に設定可能

---

## 将来の実装検討（v0.5.0 以降候補）

### 実装する場合の要件
- 日付ピッカー UI（DatePicker）の追加
- 「有効期限なし」チェックボックス（0 または MaxValue に対応）
- 入力値バリデーション（過去日付の警告・不正値の拒否）
- accountExpires を `EditableAttributes` に明示的に追加した場合のみ有効化（セーフガード）
- 更新前確認ダイアログへの有効期限変更の明示
- 更新後の `verifiedAfterUpdate` への記録

### 実装しない場合のリスク
- 該当なし（現状 ADUC / PowerShell での運用で代替可能）

---

## 注意事項

- `accountExpires` の誤設定はアカウントロックを引き起こすため、手動更新時も十分注意すること
- `write-audit.jsonl` の `verifiedAfterUpdate` に有効期限が含まれない点を運用側で把握すること（v0.4.2）
- 将来的に編集機能を追加する場合は、`AllowedTargetOuDns` / `ExcludedSamAccountNames` の制限が適用されることを確認すること
