# コンピュータ description 編集 検証手順書 (v0.5.0)

## 概要

本手順書は ManageAdTool v0.5.0 のコンピュータオブジェクト description 編集機能を実 AD 環境で検証するための手順を記述します。

**更新対象**: `description` 属性のみ  
**禁止操作**: 無効化・OU移動・削除・グループ変更・GPO編集・パスワードリセット  
**前提条件**: `DirectoryReadOnly` モード + Domain Admins セッション + `AllowedComputerOuDns` 設定

---

## 事前準備

### appsettings.json の設定

`appsettings.ComputerDescriptionEdit.sample.json` を参考に設定する。

```json
{
  "AppPolicy": {
    "ServiceMode": "DirectoryReadOnly",
    "EditorAuthMode": "DomainAdmins",
    "AdminGroupDn": "CN=Domain Admins,CN=Users,DC=example,DC=local",
    "AllowedComputerOuDns": [
      "OU=Computers,DC=example,DC=local"
    ],
    "ExcludedComputerNames": ["DC-01", "DC-02"],
    "EditableComputerAttributes": ["description"]
  }
}
```

### テスト用コンピュータ

AD 上に以下のテスト用コンピュータを用意する（または既存の検証用端末を使用）:

- `TEST-PC-001`（description あり）  
- `TEST-PC-002`（description なし）

---

## 検証項目

### 1. コンピュータ検索

| 項目 | 手順 | 期待結果 |
|------|------|---------|
| 基本検索 | 「コンピュータ参照」タブを開き、Name の一部（2文字以上）を入力して「検索」 | 該当コンピュータが一覧に表示される |
| DNSHostName 検索 | DNS ホスト名の一部を入力して検索 | 該当コンピュータが表示される |
| 1文字検索禁止 | 1文字を入力して「検索」 | 「2文字以上入力してください」メッセージ |
| OS フィルタ | OS フィールドに「Windows 11」を入力して検索 | Windows 11 端末のみ表示 |
| description フィルタ「あり」 | description フィルタを「あり」にして検索 | description 設定済み端末のみ表示 |
| description フィルタ「なし」 | description フィルタを「なし」にして検索 | description 未設定端末のみ表示 |
| 無効コンピュータ表示 | 「無効も表示」チェックをオンにして検索 | 無効状態の端末も表示される |
| 上限到達 | 広範な検索語で MaxSearchResults 件数に達する場合 | 上限件数と件数超過メッセージを表示 |

### 2. コンピュータ詳細・グループ表示

| 項目 | 手順 | 期待結果 |
|------|------|---------|
| 詳細表示 | 検索結果から端末を選択 | 右ペインに Name/SamAccountName/DNSHostName/OS/Description/Enabled/DN/LastLogon/WhenCreated/WhenChanged が表示される |
| グループ表示 | 同上 | 所属グループ一覧が名前順で表示される |
| グループコピー | 「コピー」ボタンをクリック | クリップボードにグループ一覧がコピーされる |

### 3. CSV出力

| 項目 | 手順 | 期待結果 |
|------|------|---------|
| CSV出力 | 検索実行後「CSV出力」ボタンをクリック | ファイル保存ダイアログが開き、Name/SamAccountName/DNSHostName/OS/Description/Enabled/DN/LastLogon/WhenCreated/WhenChanged の列を含む CSV が出力される |
| 検索結果0件時 | 検索前または0件時に「CSV出力」をクリック | 「CSV出力できる検索結果がありません」メッセージ |

### 4. 編集ブロック確認

| 項目 | 手順 | 期待結果 |
|------|------|---------|
| 未選択時 | コンピュータ未選択で description テキストボックス | 入力不可（IsEnabled=False） |
| 未ログイン時 | ログインせずにコンピュータ選択 | テキストボックス入力不可、理由が表示される |
| AllowedComputerOuDns 未設定 | 設定を空にしてコンピュータ選択 | テキストボックス入力不可、「AllowedComputerOuDns / AllowedTargetOuDns 未設定のため更新不可」と表示される |
| 許可OU外 | OU 外の端末を選択 | テキストボックス入力不可、「対象コンピュータが許可OU外」と表示される |
| 除外リスト | ExcludedComputerNames に登録された端末を選択 | テキストボックス入力不可、「除外コンピュータ名のため更新不可」と表示される |

### 5. description 更新フロー

| # | 手順 | 期待結果 |
|---|------|---------|
| 1 | Domain Admins アカウントでログイン | セッション開始、description テキストボックスが有効化される |
| 2 | 検索・選択・description 編集後「差分確認」をクリック | 差分プレビューが OutputBox に表示される。「限定更新実行」ボタンが有効化される |
| 3 | 入力を変更する | 「限定更新実行」ボタンが無効化され再確認を促す |
| 4 | 再度「差分確認」をクリック | 差分が再計算される |
| 5 | 「限定更新実行」をクリック | 再認証ダイアログが開く |
| 6 | 正しい Domain Admins 資格情報を入力 | ConfirmComputerUpdateDialog が開く。コンピュータ名・DNSHostName・DN・実行端末・起動ユーザー・セッションユーザー・差分（変更前赤/変更後緑）が表示される |
| 7 | 「この内容でADを更新する」をクリック | AD更新が実行され、OutputBox に更新成功メッセージ・変更前後・AD再取得値が表示される |
| 8 | write-audit.jsonl を確認 | `operationName: "UpdateComputerDescription"`, `targetType: "Computer"`, `targetName`, `changes[].ldapAttribute: "description"` が記録されていること |
| 9 | AD ADUC または PowerShell でコンピュータの description を確認 | 正しく更新されていること |
| 10 | 「戻し用メモをコピー」ボタンをクリック | 変更前の値がクリップボードにコピーされる |

### 6. 空文字更新禁止

| 項目 | 手順 | 期待結果 |
|------|------|---------|
| description を空にして更新 | description テキストボックスをクリアして「差分確認」→「限定更新実行」 | 「空文字への更新は禁止されています」メッセージ、AD更新は実行されない |

### 7. 整合性チェック

| 項目 | 手順 | 期待結果 |
|------|------|---------|
| AD値変更後に更新実行 | 差分確認後に別の手段で AD の description を変更してから「限定更新実行」を実行 | 「AD上の値が変更されているため更新を中止しました」メッセージ、更新中止 |

### 8. セッション期限切れ

| 項目 | 手順 | 期待結果 |
|------|------|---------|
| 期限切れ後の更新試行 | EditSessionMinutes 経過後に「限定更新実行」 | 「セッション期限切れ」メッセージ、更新ブロック |

### 9. 再認証失敗

| 項目 | 手順 | 期待結果 |
|------|------|---------|
| 誤パスワードで再認証 | 再認証ダイアログで誤ったパスワードを入力 | 「再認証に失敗しました」メッセージ、更新中止 |
| 非 Domain Admins で再認証 | Domain Admins メンバー外のユーザーで再認証 | 「Domain Admins のメンバーではありません」メッセージ |

### 10. キャンセル動作

| 項目 | 手順 | 期待結果 |
|------|------|---------|
| 再認証ダイアログキャンセル | 再認証ダイアログで「キャンセル」 | 更新中止、AD更新は実行されない |
| 確認ダイアログキャンセル | ConfirmComputerUpdateDialog で「キャンセル」 | 更新中止 |

---

## 確認後の処理

- 検証に使用したテスト用コンピュータの description を元の値に戻す
- write-audit.jsonl の `revertCandidate` を参照して手動戻しが可能であることを確認する
- 検証結果を `docs/test-record-v0.5.0.md` に記録する（別途作成）
