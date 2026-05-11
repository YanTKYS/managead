# operation/ ガイド

ManageAdTool の操作説明、管理者向け設定、トラブルシューティング、実 AD 検証手順、検証結果記録の入口です。

---

## 操作・運用手順

| ファイル | 対象読者 | 内容 |
|---|---|---|
| `user-manual.md` | 情報システム担当者 | 起動、検索、詳細表示、限定編集、GPOシミュレーション、オペレーション支援、ログ確認、CSV出力の操作手順 |
| `admin-manual.md` | 配布・設定・保守管理者 | 配置、appsettings.json、config-samples、OU制限、保護・除外設定、ログ、Domain Admins 認証、検証用 OU から始める運用手順 |
| `troubleshooting.md` | 利用者・管理者 | 起動不可、検索不可、認証失敗、編集不可、更新不可、ログ、CSV、GPOシミュレーションの切り分け |
| `deploy.md` | 配布担当者 | 配布・閉域端末への持ち込み・初回起動手順 |

---

## 検証手順（validation-*.md）

実 AD 環境での動作確認に使用します。必ず検証用 OU のみを対象に開始してください。

| ファイル | 対象機能 |
|---|---|
| `validation-readonly.md` | DirectoryReadOnly 参照機能 |
| `validation-auth.md` | Domain Admins ログイン・セッション管理 |
| `validation-user-edit.md` | ユーザー属性編集（mail / displayName / sn / givenName） |
| `validation-computer-edit.md` | コンピュータ description 編集 |
| `validation-group-edit.md` | グループメンバー追加・削除 |
| `validation-group-member-edit.md` | グループメンバー編集の補足検証 |
| `validation-gpo-simulation.md` | GPOシミュレーション（簡易・OUリンク参照） |
| `validation-operation-support.md` | オペレーション支援 |
| `validation-log-viewer.md` | ログ確認 |

---

## 検証結果記録（test-record-*.md）

実施した検証の結果を記録します。

| ファイル | バージョン |
|---|---|
| `test-record-v0.1.0.md` | v0.1.0 |
| `test-record-v0.2.0.md` | v0.2.0 |
| `test-record-v0.4.1.md` | v0.4.1 |
| `test-record-v0.7.0.md` | v0.7.0 |
| `test-record-v0.9.0.md` | v0.9.0 |
| `test-record-v0.9.6.md` | v0.9.6 打鍵テスト |

新しい検証結果は、該当バージョンの `test-record-*.md` を追加または既存様式をコピーして記録してください。
