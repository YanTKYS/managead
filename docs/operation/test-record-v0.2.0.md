# ManageAdTool v0.2.0 手動テスト記録

## 実施日
- （実施時に記入）

## 対象バージョン
- v0.2.0

## 実施環境
- ServiceMode: InMemory（閉域確認）/ DirectoryReadOnly（実AD確認）

---

## InMemory モード確認項目

| # | 確認項目 | 結果 | 備考 |
|---|---|---|---|
| 1 | アプリ起動 | | |
| 2 | ユーザー検索（SamAccountName / DisplayName） | | |
| 3 | 検索条件（部署・Mail有無・無効ユーザー表示）の絞り込み | | |
| 4 | ユーザー詳細表示（userAccountControl / lastLogonTimestamp / accountExpires） | | |
| 5 | 所属グループの名前順表示・グループコピー | | |
| 6 | グループ検索 | | |
| 7 | グループメンバー一覧表示 | | |
| 8 | 検索結果CSV出力 | | |
| 9 | 検索件数が上限（MaxSearchResults）に達したとき警告メッセージが表示される | | |
| 10 | DirectoryReadOnly モード相当の UI 制御（更新ボタン無効）| | InMemory は DirectoryReadOnly 以外扱いだが参照専用動作を確認 |
| 11 | 処理結果欄に操作結果が表示される | | |

---

## DirectoryReadOnly モード確認項目

実AD接続が必要な項目。`docs/validation-readonly.md` の手順に従って実施する。

| # | 確認項目 | 結果 | 備考 |
|---|---|---|---|
| 1 | アプリ起動 | | |
| 2 | 実ADユーザー検索（SamAccountName / DisplayName / Mail） | | |
| 3 | 検索条件（部署・Mail有無・無効ユーザー表示）の絞り込み | | |
| 4 | ユーザー詳細表示（userAccountControl / lastLogonTimestamp / accountExpires） | | |
| 5 | 所属グループの名前順表示・グループコピー | | |
| 6 | グループ検索 | | |
| 7 | グループメンバー一覧表示 | | |
| 8 | 検索結果CSV出力 | | |
| 9 | MaxSearchResults 上限到達時の警告メッセージ表示 | | 上限に近い件数が返る検索語で確認 |
| 10 | 参照ログ（audit.jsonl）への追記 | | UserSearch / UserDetail / UserGroups / GroupSearch / GroupMembers |
| 11 | 更新ボタンが無効（参照専用の確認） | | |
| 12 | エラー発生時に処理結果欄に利用者向けメッセージが表示される | | |

---

## 未実施項目
- 実AD更新
  - 理由: v0.2.0 は参照専用。DirectoryServicesAdWriteService / DirectoryServicesAdLimitedWriteService は未実装・ビルド対象外
- グループ操作、GPO編集、無効化、退職処理、OU移動、一括更新
  - 理由: v0.2.0 方針により対象外

## 備考
- `InMemoryAdService.GetGroupMembers` はグループの DistinguishedName で呼ばれる。v0.2.0 で DN → グループ名への変換を修正済み。
