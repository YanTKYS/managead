# ManageAdTool ロードマップ

## 方針

本ツールは閉域ネットワーク向けの **Active Directory 参照支援ツール** として開発しています。  
v0.3.0 時点では実 AD 更新機能は未実装です。  
将来的な限定的編集機能は、認証・監査・対象 OU・対象属性の制限を前提に v0.4.0 以降で別途検討します。

---

## リリース済み

### v0.1.0
- WPF MVP 画面（ユーザー検索・詳細表示・mail/department/title 比較）
- InMemory / DirectoryReadOnly の ServiceMode 切り替え
- GitHub Actions によるビルド自動化

### v0.2.0
- 参照専用AD確認支援ツールとして安定化
- ユーザー詳細拡充（userAccountControl / lastLogonTimestamp / accountExpires）
- グループ検索・グループメンバー一覧・検索結果 CSV 出力
- 参照ログ（JSON Lines）
- 検索条件・表示項目の設定化（MaxSearchResults / UserDetailDisplayAttributes）
- MainWindowViewModel 導入による UI 制御分離

### v0.3.0
- 別ユーザーログイン・Domain Admins 判定基盤（認証のみ、AD 更新は未実装）
- LDAP バインド認証（パスワード非保持・非ログ）
- 編集セッション管理（タイムアウト・自動チェック）
- 認証ログ（auth.jsonl）

---

## 今後の検討事項

### v0.3.0 検証
- DirectoryServicesAdReadService の検索パフォーマンス検証（実AD環境での PageSize・SizeLimit の効果測定）
- 実AD環境での認証・セッション動作の検証（`docs/validation-auth.md` 参照）

### v0.4.0 検討中（未確定）
- 限定的な属性編集機能（mail / department / title）
- 前提条件:
  - v0.3.0 の認証基盤（Domain Admins セッション）が実運用で安定すること
  - 対象 OU・対象属性・監査ログの制限を設計すること
  - 実 AD への書き込みリスク評価と承認フローの整備
- 対象外（v0.4.0 以降も検討しない）: グループ追加・削除、GPO 編集、OU 移動、一括更新、ユーザー無効化・退職処理

### キャッシュ（DirectoryReadOnly 実運用後に検討）
- ユーザー選択切替ごとの `memberOf` 再取得削減（セッション内メモリキャッシュ、TTL: 30〜120秒）
- キャッシュヒット率・LDAP呼出回数のログ可視化
