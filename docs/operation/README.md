# operation/ ガイド

配布・検証・テスト記録のドキュメント一覧です。

## 検証手順（validation-*.md）

実AD環境での動作確認に使用します。必ず検証用OUのみを対象に実施してください。

| ファイル | 対象機能 |
|---|---|
| `validation-readonly.md` | DirectoryReadOnly 参照機能 |
| `validation-auth.md` | Domain Admins ログイン・セッション管理 |
| `validation-user-edit.md` | ユーザー属性編集（mail / displayName / sn / givenName） |
| `validation-computer-edit.md` | コンピュータ description 編集 |
| `validation-group-edit.md` | グループメンバー追加・削除 |

## 検証結果記録（test-record-*.md）

実施した検証の結果を記録します。

| ファイル | バージョン |
|---|---|
| `test-record-v0.2.0.md` | v0.2.0 |
| `test-record-v0.4.1.md` | v0.4.1 |

v0.6.0 の検証結果は `test-record-v0.6.0.md`（別途作成）に記録してください。

## デプロイ手順

`deploy.md` を参照してください。
