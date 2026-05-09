# ManageAdTool ロードマップ

## 方針

本ツールは閉域ネットワーク向けの **Active Directory 参照・限定編集支援ツール** として開発しています。

- 参照機能を主目的とし、安全性・シンプルさを優先します
- 書き込みは mail / department / title のみに限定します（v0.4.0 時点）
- パスワードは appsettings.json / ログへの保存を行いません
- グループ操作・GPO編集・OU移動・ユーザー無効化・一括更新は対象外とします

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

---

## 今後の検討事項

### v0.4.x 完了条件
- 実AD環境での v0.4.1 限定編集動作検証（`docs/validation-user-edit.md` 参照）
- 検証結果を `docs/test-record-v0.4.1.md` に記録する

### v0.5.0 以降の候補（優先度順・未確定）

1. **参照強化**（優先度 高）
   - コンピューターオブジェクトの参照
   - グループの詳細参照（説明・管理者・ネスト状態）
   - 検索条件の拡張（OU指定・LastLogon日付範囲など）

2. **キャッシュ**（DirectoryReadOnly 実運用後に評価）
   - ユーザー選択切替ごとの `memberOf` 再取得削減
   - セッション内メモリキャッシュ（TTL: 30〜120秒）

3. **編集機能の拡張**（v0.4.0 の実運用評価後に検討）
   - 更新対象属性の追加（要件定義次第）
   - 複数ユーザーへの一括属性更新（安全性設計が前提）

### 対象外（今後も実装しない）
- グループ追加・削除
- GPO 編集
- OU 移動
- ユーザー無効化・退職処理
- パスワードリセット
- 新規ユーザー作成
- サービスアカウント方式（パスワードを設定ファイルに保存する方式）
