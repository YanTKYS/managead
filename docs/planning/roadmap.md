# ManageAdTool ロードマップ

## 方針

本ツールは閉域ネットワーク向けの **Active Directory 参照・限定編集支援ツール** として開発しています。

- 参照機能を主目的とし、安全性・シンプルさを優先します
- ユーザー書き込みは mail / displayName / sn / givenName のみに限定します
- コンピュータ書き込みは description のみに限定します（v0.5.0 以降）
- グループ書き込みはユーザーのメンバー追加・削除のみに限定します（v0.6.0 以降）
- パスワードは appsettings.json / ログへの保存を行いません
- グループ作成・削除・リネーム・グループをグループに追加・GPO編集・OU移動・ユーザー無効化・一括更新は対象外とします

---

## リリース済み

### v0.1.0
- WPF MVP 画面（ユーザー検索・詳細表示・mail/department/title 比較）
- InMemory / DirectoryReadOnly の ServiceMode 切り替え
- GitHub Actions によるビルド自動化

### v0.2.0
- 参照専用AD確認支援ツールとして安定化
- ユーザー詳細拡充・グループ検索・グループメンバー一覧・CSV出力・参照ログ
- 検索条件・表示項目の設定化（MaxSearchResults / UserDetailDisplayAttributes）
- MainWindowViewModel 導入

### v0.3.0
- 別ユーザーログイン・Domain Admins 判定基盤
- LDAP バインド認証・編集セッション管理・認証ログ（auth.jsonl）

### v0.4.0
- ユーザー属性限定編集 検証版（mail / department / title のみ）
- 更新フロー: 差分確認 → 再認証 → 実行前確認 → AD整合性チェック → 更新 → AD再取得
- 書き込み監査ログ（write-audit.jsonl）
- AllowedTargetOuDns 未設定時は更新不可のセーフガード
- 空文字更新禁止

### v0.4.1
- ユーザー属性限定編集 安定化版（v0.4.x 安定化フェーズ）
- 更新結果表示の改善（属性ごとに変更前・変更後・AD再取得値を縦並び表示）
- 戻し支援の追加（「戻し用メモをコピー」ボタン・戻し候補表示）
- 更新前確認ダイアログの改善（DisplayName・実行端末・起動ユーザー・セッションユーザー追加）
- 差分確認状態の明確化（WriteButtonDisabledReason をボタン下に常時表示）
- write-audit.jsonl に targetDisplayName / revertCandidate を追加
- エラーメッセージの利用者向け整理
- LogPath 書き込み権限チェック（起動時 + 更新前確認ダイアログ）

### v0.4.2
- 編集対象属性の見直し: mail / department / title → mail / displayName / sn / givenName
- UI の日本語ラベル整備（メールアドレス / 表示名 / 姓 / 名）
- ユーザー名（sAMAccountName）は参照専用（参照のみ表示・編集不可）とする設計を確立
- `EditableAttributeDefs` による属性定義の一元管理
- `FieldChange.LdapAttribute` を追加し write-audit.jsonl の ldapAttribute フィールドを強化
- 書き込みサービスの LdapAttribute ベースバリデーションへの切り替え
- `AdUser` に Surname / GivenName を追加
- アカウント有効期限（accountExpires）設計方針を docs/design-account-expiration.md に記録

### v0.5.0
- コンピュータオブジェクト参照・description 限定編集
- コンピュータ検索（Name / DNSHostName / sAMAccountName、OS フィルタ、description 有無、無効端末表示）
- コンピュータ詳細表示（Name / DNSHostName / OS / Enabled / Description / DN / LastLogon / WhenCreated / WhenChanged）・所属グループ表示・CSV出力
- 更新対象は **description のみ**（Domain Admins セッション必須・AllowedComputerOuDns 配下のみ・ExcludedComputerNames は不可・空文字禁止）
- 禁止操作（実装しない）: 無効化・OU移動・削除・グループ変更・GPO編集・パスワードリセット・一括更新
- 更新フロー: 差分確認 → 再認証 → 確認ダイアログ → AD再取得・整合性チェック → 更新 → AD再取得
- write-audit.jsonl に targetType / targetName / operationName フィールドを追加（ユーザー更新ログとの後方互換あり）
- AppPolicy に AllowedComputerOuDns / ExcludedComputerNames / EditableComputerAttributes を追加
- `docs/validation-computer-edit.md` / `appsettings.ComputerDescriptionEdit.sample.json` を追加

### v0.6.0
- グループ詳細表示強化（ユーザーメンバー / コンピュータメンバー / ネストグループ / memberOf）
- グループメンバー限定編集: **ユーザーのみ追加・削除可能**（グループをグループに追加・コンピュータ追加は不可）
- 追加予定・削除予定のステージング UI（差分確認後のみ更新可能）
- `EditableGroupOuDns` 配下のみ編集可能。空なら更新不可（セーフガード）
- `ProtectedGroupNames` / `ProtectedGroupDns` に登録されたグループは編集不可
- 整合性チェック（更新前にAD再取得し既存メンバー状態を確認）
- 更新フロー: 差分確認 → 再認証 → 確認ダイアログ → AD再取得・整合性チェック → 更新 → AD再取得
- write-audit.jsonl に `targetType: "Group"` / `operationName: "UpdateGroupMembers"` として記録
- AppPolicy に EditableGroupOuDns / ProtectedGroupNames / ProtectedGroupDns を追加
- `docs/validation-group-edit.md` / `appsettings.GroupMembershipEdit.sample.json` を追加
- 禁止操作（実装しない）: グループ作成・削除・リネーム / グループをグループに追加 / コンピュータをグループに追加 / GPO編集 / OU移動 / 一括更新

---

## 今後の検討事項

### v0.6.x 完了条件
- 実AD環境での v0.6.0 グループメンバー編集動作検証（`docs/validation-group-edit.md` 参照）
- 検証結果を `docs/test-record-v0.6.0.md` に記録する（別途作成）

### v0.7.0 以降の候補（優先度順・未確定）

1. **参照強化**
   - 検索条件の拡張（OU指定・LastLogon日付範囲など）

2. **キャッシュ**（DirectoryReadOnly 実運用後に評価）
   - ユーザー選択切替ごとの `memberOf` 再取得削減
   - セッション内メモリキャッシュ（TTL: 30〜120秒）

3. **編集機能の拡張**（実運用評価後に検討）
   - ユーザー更新対象属性の追加（要件定義次第）

### 対象外（今後も実装しない）
- グループ作成・削除・リネーム
- グループをグループに追加
- コンピュータをグループに追加
- GPO 編集
- OU 移動
- ユーザー無効化・退職処理
- パスワードリセット
- 新規ユーザー作成
- サービスアカウント方式（パスワードを設定ファイルに保存する方式）
