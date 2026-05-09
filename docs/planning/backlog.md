# Backlog

完了済み作業の詳細は `docs/planning/completed-work.md` を参照してください。

---

## v0.6.x 完了条件（v0.7.0 着手前に確認）

- 実AD環境での v0.6.0 グループメンバー編集動作検証（`docs/operation/validation-group-edit.md` 参照）
- 検証結果を `docs/operation/test-record-v0.6.0.md` に記録する
- `write-audit.jsonl` の `targetType: "Group"` / `operationName: "UpdateGroupMembers"` が正しく記録されることを確認する
- 保護グループ（`ProtectedGroupNames` / `ProtectedGroupDns`）が実AD環境で正しくブロックされることを確認する

---

## v0.7.x 完了条件（v0.8.0 着手前に確認）

- オペレーション支援タブの実AD環境での動作検証
- 変更予定サマリー生成・クリップボードコピーの動作確認
- 参照ログ（audit.jsonl）に `OperationPlanCreated` が正しく記録されることを確認

---

## v0.8.0 候補（優先度順・未確定）

### 参照強化
- 検索条件の拡張（OU指定・LastLogon日付範囲など）

### オペレーション支援の拡張（v0.7.0 実績確認後）
- 複数ユーザーの一括変更予定作成（設計検討）
- チェックリスト項目の設定化（OperationChecklistItems の UI 反映）

### キャッシュ（DirectoryReadOnly 実運用後に評価）
- ユーザー選択切替ごとの `memberOf` 再取得削減
- セッション内メモリキャッシュ（キー: SamAccountName、値: グループ一覧、TTL: 30〜120秒）
- 検索結果一覧表示時に必要属性を先読みし、詳細画面で再問い合わせを抑止
- 明示的リフレッシュボタンで最新取得を強制（通常はキャッシュ利用）
- キャッシュヒット率・LDAP呼出回数をログで可視化

### ログ検索・ログビューア
- `write-audit.jsonl` / `auth.jsonl` の簡易ビューア
- 操作日時・対象ユーザー・属性変更内容の一覧表示

### UI整理
- ボタン配置・ラベルの見直し
- 長いディスエーブル理由の表示改善

### ユーザー有効期限更新
- `accountExpires` の更新設計継続（`docs/design/design-account-expiration.md` 参照）
- 実運用評価後に要件定義

### 編集機能の拡張
- ユーザー更新対象属性の追加（要件定義次第）

---

## 対象外（今後も実装しない）

- グループ作成・削除・リネーム
- グループをグループに追加
- コンピュータをグループに追加
- GPO 編集
- OU 移動
- ユーザー無効化・退職処理
- パスワードリセット
- 新規ユーザー作成
- サービスアカウント方式（パスワードを設定ファイルに保存する方式）
- 一括更新
