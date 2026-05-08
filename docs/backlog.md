# Backlog

## v0.2.0 方針（参照専用強化版）
- 実AD更新機能は実装しない。
- DirectoryServicesAdWriteService は実装しない方針。
- 管理者権限ユーザーやサービスアカウントを使った更新実行は対象外。
- グループ追加・削除、GPO編集、ユーザー無効化、退職処理、OU移動、一括更新は対象外。

## v0.2.0 完了
- ユーザー詳細表示項目の拡充（lastLogonTimestamp、accountExpires、userAccountControl の読み取り表示）。
- 所属グループ表示の一覧化・名前順並び替え・コピー性向上。
- グループ検索。
- グループメンバー一覧表示。
- 参照ログ（検索・詳細表示・グループ表示の記録）。
- 検索条件の追加（部署、メール有無、無効ユーザー表示など）。
- 表示項目を `appsettings.json` で制御。
- 最大検索件数（`MaxSearchResults`）の設定化と上限超過時の利用者向けメッセージ表示（ユーザー検索・グループ検索・グループメンバー）。
- ServiceModeごとのUI表示制御を MainWindowViewModel に移動（IsReadOnlyMode / CanEdit / EditControlsEnabled）。
- mail / department / title の比較 GroupBox を「属性表示・変更予定確認」として用途を明確化。
- PropertiesToLoad 最適化：SearchUsers・GetGroupMembers は memberOf を取得しない検索用プロパティセットを使用し、GetUser のみ memberOf を含む詳細用プロパティセットを使用。
- 例外メッセージの利用者向け/管理者向け分離：UI には利用者向けラッパーメッセージ、参照ログには InnerException を含む技術詳細を記録。

## Next
- DirectoryServicesAdReadService の検索パフォーマンス検証（実AD環境での PageSize・SizeLimit の効果測定）。

## AD実運用検討（キャッシュ）
- 目的: ユーザー選択切替ごとの `memberOf` 再取得を減らし、DC負荷を抑制する。
- 候補案:
  - セッション内メモリキャッシュ（キー: SamAccountName、値: グループ一覧、TTL: 30〜120秒）
  - 検索結果一覧表示時に必要属性を先読みし、詳細画面で再問い合わせを抑止
  - 明示的リフレッシュボタンで最新取得を強制（通常はキャッシュ利用）
- 運用観点:
  - TTLを短くして鮮度を担保
  - キャッシュヒット率・LDAP呼出回数をログで可視化
- 実装時期:
  - DirectoryReadOnly 実AD運用の初期検証後に効果測定して導入可否を判断
