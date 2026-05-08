# ManageAdTool ロードマップ

## 方針

本ツールは閉域ネットワーク向けの **Active Directory 参照専用支援ツール** として開発しています。  
実 AD 更新機能は実装しない方針を維持しています。

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
- 別ユーザーログイン・Domain Admins 判定基盤
- LDAP バインド認証（パスワード非保持・非ログ）
- 編集セッション管理（タイムアウト・自動チェック）
- 認証ログ（auth.jsonl）

---

## 今後の検討事項

### パフォーマンス
- DirectoryServicesAdReadService の検索パフォーマンス検証（実AD環境での PageSize・SizeLimit の効果測定）
- v0.3.0 実AD認証・セッション動作の検証

### キャッシュ（DirectoryReadOnly 実運用後に検討）
- ユーザー選択切替ごとの `memberOf` 再取得削減（セッション内メモリキャッシュ、TTL: 30〜120秒）
- キャッシュヒット率・LDAP呼出回数のログ可視化

### 将来的な拡張（未確定）
- 実 AD 書き込み機能（グループ追加・削除など）は **現時点では対象外**
- 対象外の理由: 閉域参照専用ツールとしての安全性・シンプルさを優先
- 将来的に必要性が明確になった場合に別途検討
