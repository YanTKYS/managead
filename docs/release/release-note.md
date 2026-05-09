# Release Notes

各バージョンの開発作業詳細は `docs/planning/completed-work.md` を参照してください。

---

## v0.9.0
### Title
ManageAdTool ログ確認機能追加（audit / auth / write-audit ビューア）

### Note
- **ログ確認タブを追加**: 参照ログ（audit.jsonl）・認証ログ（auth.jsonl）・書き込みログ（write-audit.jsonl）を画面上で閲覧・フィルタリングできる新タブ。
- **ログ種別選択と読み込み**: ドロップダウンでログ種別を選択し「読み込み」ボタンで表示。最大 `MaxLogDisplayRows`（デフォルト 1000）件を末尾から取得。
- **フィルター機能**: 日付範囲・成否（すべて / 成功 / 失敗）・操作キーワード・対象キーワードで絞り込み。
- **詳細JSON表示**: 行選択で整形済みJSONを下部ペインに表示（パスワードフィールドは `***` にマスク）。
- **コピー・CSV出力**: 詳細JSONのクリップボードコピー、フィルター結果のCSV出力（BOM付きUTF-8）。
- **フォルダを開く**: ログ格納フォルダをエクスプローラーで開けるボタンを追加。
- **解析エラー耐性**: 不正な JSON 行は "(解析エラー)" として一覧表示し、他行の表示を継続。
- **参照ログのエンリッチ**: `ReferenceAuditLogger` に `executor` / `machineName` を追加し、全参照ログエントリに記録。
- **AppPolicy 拡張**: `MaxLogDisplayRows` を追加（デフォルト 1000）。
- **AD更新は行わない**: ログ確認タブはすべて読み取り専用。書き込み操作は実行しない。

## v0.8.0
### Title
ManageAdTool GPOシミュレーション機能追加（OUリンク参照・簡易）

### Note
- **GPOシミュレーションタブを追加**: ユーザー / コンピュータ / 両方を対象に、OUリンクから推定されるGPO一覧を参照できる新タブ。詳細なRSOP代替ではなく、OU に直接リンクされたGPOを一覧確認する簡易機能。
- **対象種別の選択**: ユーザー・コンピュータ・ユーザー+コンピュータ から選択し、それぞれの入力欄を表示。
- **GPO一覧表示**: GPO名 / GPO ID / 適用対象 / リンク先OU / 有効 / 強制適用 / 備考 を一覧表示。
- **結果コピー・CSV出力**: 一覧をクリップボードにコピー、またはCSVファイルとして出力できる。
- **参照ログに記録**: シミュレーション実行時に `audit.jsonl` へ `GpoSimulation` として記録。
- **GPO編集は実装しない**: GPO編集 / GPOリンク変更 / セキュリティフィルタ変更 / WMIフィルタ変更は対象外。
- **未考慮項目**: セキュリティフィルタ・WMIフィルタ・継承ブロック（Block Inheritance）・ループバック処理・サイトリンクは本バージョンでは考慮しない。

## v0.7.0
### Title
ManageAdTool オペレーション支援機能追加（ユーザー所属変更支援）

### Note
- **オペレーション支援タブを追加**: 定型的なユーザー所属変更作業の「変更予定作成・確認」を支援する新タブ。
- **ユーザー所属変更支援**: 対象ユーザーを検索・選択し、属性変更予定（表示名・姓・名・メールアドレス）とグループ追加・削除予定を一画面で管理できる。
- **変更予定サマリー生成**: 変更予定をテキスト形式でまとめ、クリップボードへコピー可能。決裁書・作業メモ・チケットへの貼り付けを想定。
- **確認チェックリスト**: 対象ユーザー確認・属性確認・グループ確認・関係部署確認のチェックボックスを表示（画面上のみ・永続保存なし）。
- **AD更新は直接実行しない**: このタブはあくまで変更予定の確認用。実際の更新はユーザー編集タブ・グループ編集タブから実行。
- **参照ログに記録**: サマリー生成時に `audit.jsonl` へ `OperationPlanCreated` として記録（write-audit.jsonl には記録しない）。
- **AppPolicy 拡張**: `EnableOperationSupport` / `OperationChecklistItems` を追加（デフォルト有効・固定チェックリスト）。
- **実装しないもの**: 複数操作の一括実行・ユーザー新規作成・無効化・退職処理・OU移動・パスワードリセット・承認ワークフロー・チェックリストの永続保存。

## v0.6.0
### Title
ManageAdTool グループ詳細表示強化・ユーザー限定グループメンバー追加・削除

### Note
- **グループ詳細表示強化**: ユーザーメンバー一覧（SamAccountName / DisplayName / Mail / Department / Enabled）、コンピュータメンバー数・ネストグループ数・memberOf 表示に対応。
- **グループメンバー限定編集**: ユーザーのみグループへの追加・削除が可能。グループをグループに追加・コンピュータ追加・グループ作成・削除・リネームは対象外。
- **ステージング UI**: 追加予定・削除予定リストにユーザーを積み上げ、「差分確認」後のみ更新実行が可能。
- **セーフガード**: `EditableGroupOuDns` 未設定なら更新不可。`ProtectedGroupNames` / `ProtectedGroupDns` に登録されたグループは編集不可。重複追加・非メンバー削除・矛盾操作を禁止。
- **更新フロー（8段階）**: 変更予定作成 → 差分確認 → 再認証 → 確認ダイアログ → 更新前AD再取得・整合性チェック → 更新 → 更新後AD再取得 → 監査ログ出力。
- **write-audit.jsonl**: `targetType: "Group"` / `operationName: "UpdateGroupMembers"` で記録。
- **AppPolicy 拡張**: `EditableGroupOuDns` / `ProtectedGroupNames` / `ProtectedGroupDns` を追加。
- `docs/operation/validation-group-edit.md` / `config-samples/appsettings.GroupMembershipEdit.sample.json` を追加。

## v0.5.0
### Title
ManageAdTool コンピュータオブジェクト参照・description 限定編集

### Note
- **コンピュータ参照機能を追加**: 「コンピュータ参照」タブを新設。Name / DNSHostName / sAMAccountName による検索、OS・description・無効端末フィルタに対応。
- **コンピュータ詳細表示**: Name / DNSHostName / OS / Enabled / Description / DN / LastLogon / WhenCreated / WhenChanged を表示。所属グループ表示・CSV出力にも対応。
- **description 限定更新**: コンピュータの description のみ更新可能。`AllowedComputerOuDns` 配下・Domain Admins セッション必須・空文字禁止。無効化・OU移動・削除・グループ変更・GPO編集は対象外。
- **write-audit.jsonl**: `targetType` / `targetName` / `operationName` フィールドを追加（既存ユーザー更新ログとの後方互換あり）。
- **AppPolicy 拡張**: `AllowedComputerOuDns` / `ExcludedComputerNames` / `EditableComputerAttributes` を追加。
- `docs/operation/validation-computer-edit.md` / `config-samples/appsettings.ComputerDescriptionEdit.sample.json` を追加。

## v0.4.2
### Title
ManageAdTool ユーザー属性限定編集 属性見直し版（メールアドレス / 表示名 / 姓 / 名）

### Note
- **編集対象属性の見直し**: mail / department / title → **mail / displayName / sn / givenName** に変更。
- **ユーザー名（sAMAccountName）の参照専用化**: 読み取り専用表示のみ。書き込み対象から永久除外。
- **EditableAttributeDefs**: 属性定義（日本語表示名・LDAP属性名）を一元管理するクラスを追加。
- **FieldChange.LdapAttribute**: `write-audit.jsonl` の `changes` に `ldapAttribute` フィールドを追加。
- `docs/design/design-account-expiration.md` を追加（アカウント有効期限設計方針）。

## v0.4.1
### Title
ManageAdTool ユーザー属性限定編集 安定化版（UI改善・戻し支援・監査強化）

### Note
- **更新結果表示の改善**: 属性ごとに「変更前・変更後・AD再取得値」を縦並びで表示。
- **戻し支援**: 更新成功後に戻し候補を表示し「戻し用メモをコピー」ボタンを追加。
- **確認ダイアログの改善**: 対象 DisplayName・実行端末・起動ユーザー・セッションユーザーを追加。前後の値を色分け表示。
- **差分確認状態の明確化**: 更新ボタン無効理由をボタン下に常時表示（8種類）。
- **write-audit.jsonl 強化**: `targetDisplayName` / `revertCandidate` を追加。
- **LogPath 書き込み権限チェック**: 起動時に検証し、書き込み不可の場合は警告表示。

## v0.4.0
### Title
ManageAdTool ユーザー属性限定編集 検証版

### Note
- **mail / department / title の3属性のみ** AD更新が可能になった（検証版）。
- **更新フロー**: 差分確認 → 再認証 → 確認ダイアログ → AD再取得・整合性チェック → 更新 → AD再取得。
- **パスワード管理**: appsettings.json / ログへの保存なし。再認証パスワードは即時使用・破棄。
- **セーフガード**: `AllowedTargetOuDns` 未設定の場合は更新不可。空文字更新禁止。
- **書き込み監査ログ** （`write-audit.jsonl`）を追加。

## v0.3.0
### Title
ManageAdTool 別ユーザーログイン・Domain Admins 判定基盤

### Note
- **編集者ログイン UI**: domain\user 形式・PasswordBox・ログイン/ログアウトボタン・セッション状態表示を追加。
- **LDAP バインド認証**: パスワード非保持・非ログ。Domain Admins グループメンバー判定（直接 / ネスト対応）。
- **編集セッション管理**: タイムアウト（`EditSessionMinutes`）・30秒間隔の自動期限チェック。
- **認証ログ** （`auth.jsonl`）への JSON Lines 記録。

## v0.2.0
### Title
ManageAdTool 参照専用AD確認支援ツール 強化版

### Note
- **参照専用ツールとして育てる方針**に変更。実AD更新機能は実装しない。
- **グループ検索・グループメンバー一覧**・検索結果 CSV 出力・参照ログ（JSON Lines）を追加。
- **ユーザー詳細拡充**: userAccountControl / lastLogonTimestamp / accountExpires の読み取り表示。
- **検索条件追加**: 部署・Mail 有無・無効ユーザー表示。`MaxSearchResults`（デフォルト 200）の設定化。
- **UI制御を MainWindowViewModel に移動**（IsReadOnlyMode / CanEdit / EditControlsEnabled）。

## v0.1.0
### Title
ManageAdTool MVP 初版（InMemory / DirectoryReadOnly 対応）

### Note
- WPF MVP 画面を追加（ユーザー検索・詳細表示・差分確認・処理結果欄）。
- `InMemoryAdService` を追加し、閉域検証向けのデモ運用を可能化。
- `DirectoryServicesAdReadService`（読み取り専用）を追加し、実AD検索・詳細・所属グループ表示に対応。
- GitHub Actions によるビルド自動化（通常ビルド・手動リリースビルド）。
