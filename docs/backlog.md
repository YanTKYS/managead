# Backlog

## Next
- DirectoryServicesAdReadService の検索パフォーマンス検証（PageSize、PropertiesToLoad最適化）。
- 監査ログのローテーション・改ざん検知（ハッシュチェーン）検討。
- ServiceModeごとのUI表示制御をViewModel化（現状はcode-behind）。
- 例外メッセージの利用者向け/管理者向け分離。

## Future
- 書き込み可能な DirectoryServicesAdWriteService（MVP後段）
- グループ操作/GPO操作の安全設計再導入（二重承認含む）

## AD実運用検討（キャッシュ）
- 目的: ユーザー選択切替ごとの `memberOf` 再取得を減らし、DC負荷を抑制する。
- 候補案:
  - セッション内メモリキャッシュ（キー: SamAccountName、値: グループ一覧、TTL: 30〜120秒）
  - 検索結果一覧表示時に必要属性を先読みし、詳細画面で再問い合わせを抑止
  - 明示的リフレッシュボタンで最新取得を強制（通常はキャッシュ利用）
- 運用観点:
  - TTLを短くして鮮度を担保
  - 監査/権限制御に関わる操作直前は再読込する安全策
  - キャッシュヒット率・LDAP呼出回数をログで可視化
- 実装時期:
  - DirectoryReadOnly 実AD運用の初期検証後に効果測定して導入可否を判断
