# MPCman bot v3 設計仕様書

- 作成日：2026-11-10
- 対象：部活動Discordサーバー向け補助bot「MPCman bot」v3
- 前バージョン：v2（Python / discord.py、`/Users/cometlake/MPCman-bot`）

## 1. 目的と前提

### 1.1 目的
部活動のDiscordサーバーで、部員ロールの付与（参加）と、部員からOBへの切り替え（卒業）を、本人がスラッシュコマンドで行えるようにする。v2から機能を絞り、プライバシーに配慮して作り直す。

### 1.2 要件
- v2から引き継ぐ機能は `join` と `graduate` の2つだけ。そのほかの機能（参加時の挨拶、`$clear`、画像投稿、`$hello`、`$help`）は廃止する。
- **メッセージ本文は読み取らない**。操作はスラッシュコマンドだけで受け付ける。
- コマンドは本人が自分で実行する。管理者による承認や代理操作は行わない。
- 新機能の追加予定はない。最小構成で作る。
- 動作環境は手元のx86-64 Debianマシン。
- 将来は後輩に引き継ぐ。プログラミング経験の浅い部員でも、手順書を見て導入、運用、設定変更ができるようにする。
- 開発はWindows、macOS、Linuxのいずれでも行えるようにする。
- コードはGitHubで管理する。

### 1.3 対象外
- 複数のDiscordサーバーへの対応（部活のサーバー1つだけを対象にする）
- データベースなどへのデータの保存
- GitHub Actionsの設定ファイルの作成（ユーザーが自分で作る。要件は第7章に記載）

## 2. 技術構成

| 項目 | 採用するもの |
|---|---|
| 言語とランタイム | C#、.NET 10（LTS） |
| Discordライブラリ | Discord.NET（`InteractionService` でスラッシュコマンドを扱う） |
| プロジェクトのテンプレート | Worker Service（`dotnet new worker`） |
| 共通の仕組み | .NET Generic Host（設定、ログ、DI）、`Microsoft.Extensions.Hosting.Systemd` |
| 配布形式 | linux-x64向けの自己完結型の単一ファイル |
| 常駐の方法 | systemd（Dockerは使わない。後から移行できる設計にする） |
| テスト | xUnit |

### 2.1 Discordとの接続
- 要求するGateway Intentは **`Guilds` だけ**。特権Intent（Message Content、Server Members、Presence）は使わない。
- スラッシュコマンドは、設定した**サーバー専用のコマンド**として登録する（登録した直後から使えるため）。
- botに必要な権限は「ロールの管理（Manage Roles）」と「メッセージの送信（Send Messages）」の2つだけ。招待URLのスコープは `bot` と `applications.commands`。

## 3. ファイル構成

```
mpcman_bot_v3/
├── .editorconfig
├── .gitattributes                … 改行コードをLFにそろえる
├── .gitignore
├── MpcmanBot.sln
├── README.md
├── docs/
│   ├── operations.md             … 運用担当者向け手順書
│   ├── development.md            … 開発担当者向け手順書
│   └── superpowers/specs/        … 本仕様書
├── deploy/
│   └── mpcman.service            … systemdの設定ファイル
├── src/MpcmanBot/
│   ├── MpcmanBot.csproj
│   ├── Program.cs                … 起動処理（設定、ログ、各部品の組み立て）
│   ├── Worker.cs                 … Discordへの接続とコマンドの登録・振り分け
│   ├── BotOptions.cs             … 設定の中身を表すクラスと検証
│   ├── Commands/
│   │   ├── JoinCommand.cs        … /join のすべて（判定、ロール変更、返信）
│   │   └── GraduateCommand.cs    … /graduate のすべて
│   ├── appsettings.example.json  … 設定のひな形（配布物に含める）
│   ├── appsettings.Development.json
│   └── Properties/PublishProfiles/linux-x64.pubxml
└── tests/MpcmanBot.Tests/
```

### 3.1 構成の方針
- **スラッシュコマンドごとに1ファイル**にする。判定ロジックを別のファイルやクラスに分けない。コマンドが2つしかなく、判定も簡単なため。
- `Worker.cs` はテンプレートの名前のまま残す。Worker Serviceのひな形から作られていることがわかりやすいため。
- どのコマンドも「今のロールを取得 → 判定 → ロールを変更 → 返信」の同じ流れで書く。
- null許容参照型を有効にする。
- 書式は `.editorconfig` と `dotnet format` でそろえる。
- コメントは日本語で書き、「何をしているか」ではなく「なぜそうしているか」を書く。
- インターフェースなどの抽象化は、必要になるまで作らない。

### 3.2 OSの違いへの対応
- linux-x64向けの設定（RuntimeIdentifier、SelfContained、PublishSingleFile）は、`.csproj` ではなく発行プロファイル `linux-x64.pubxml` に書く。そうすることで、どのOSでも `dotnet run` と `dotnet test` がそのまま動く。
- 配布物は `dotnet publish -c Release -p:PublishProfile=linux-x64` で作る。どのOSで実行しても同じものができる。
- `InvariantGlobalization=true` を設定し、Linux側に `libicu` を入れなくても動くようにする。
- `UseSystemd()` は、systemdの外で動いているときは何もしない。WindowsやmacOSで開発するときも、コードを変える必要はない。

## 4. 設定

### 4.1 設定ファイル（`appsettings.json`）

```json
{
  "Bot": {
    "GuildId": 0,
    "MemberRoleId": 0,
    "ObRoleId": 0
  },
  "Messages": {
    "JoinSuccess": "ようこそ、{user}さん！これからよろしく！",
    "JoinAlreadyMember": "あなたは既に部員です。",
    "GraduateSuccess": "🎓 {user}さんが卒業しOBになりました！これからもよろしく！",
    "GraduateNotMember": "部員ロールを持っていないため卒業できません。",
    "Error": "処理に失敗しました。時間をおいて再度試すか、管理者に連絡してください。"
  }
}
```

- ロールとサーバーは**名前ではなくID**で指定する。名前で指定すると、ロール名を変えただけで動かなくなるため。
- **ひな形をコピーする方式**にする。配布物に `appsettings.example.json` を含め、導入するときに `appsettings.json` としてコピーして編集してもらう。botがファイルを自動で作ることはしない。
- 読み込むのは起動時だけ。設定を変えたら、botを再起動して反映する。
- 設定ファイルの場所は環境変数 `MPCMAN_CONFIG` で指定する。指定がないときは、.NET標準の場所（アプリの実行フォルダ。開発中はプロジェクトのフォルダ）にある `appsettings.json` と `appsettings.{Environment}.json` を読む。
- `Messages` の項目のうち、書かれていないものには上の既定値を使う。

### 4.2 トークン
- トークンは設定ファイルには書かず、**環境変数 `Bot__Token`** から読む。コードの中では `Bot:Token` として扱う。
- 本番では、systemdの `EnvironmentFile=/etc/mpcman-bot/secrets.env` で渡す。このファイルの所有者はrootで、パーミッションは600にする。
- 開発中は `dotnet user-secrets set "Bot:Token" "<トークン>"` で設定する。
- トークンはログに出さない。

### 4.3 起動時の検証
次の場合は、原因をログに出して**終了コード78**で終了する。systemd側で再起動しないように設定する（6.2参照）。

| チェックすること | ログのメッセージ（例） |
|---|---|
| 設定ファイルがない（`MPCMAN_CONFIG` で指定した場所） | `appsettings.json が見つかりません。appsettings.example.json をコピーしてください` |
| `GuildId`、`MemberRoleId`、`ObRoleId` が0または未設定 | `Bot:MemberRoleId が設定されていません` |
| トークンが空 | `Bot__Token が設定されていません（secrets.env を確認）` |
| ログインに失敗した（トークンが不正） | `ログインに失敗しました。トークンを確認してください` |
| `GuildId` のサーバーが見つからない | `GuildId のサーバーが見つかりません。botを招待済みか確認してください` |
| 部員ロール、またはOBロールがサーバーに存在しない | `MemberRoleId のロールがサーバーに存在しません` |
| botに「ロールの管理」権限がない | `botに「ロールの管理」権限がありません` |
| botの最上位ロールが部員ロール、またはOBロール以下の順位にある | `botのロールを「部員」「OB」より上に移動してください` |

サーバー、ロール、権限の4項目は、Discordへの接続が完了した時点（Readyイベント）で確認する。

`MPCMAN_CONFIG` を指定していない場合（開発中など）は、設定ファイルがなくてもエラーにはしない（.NET標準の動きに合わせる）。その場合、IDが未設定になるので、2行目のチェックで終了する。

## 5. コマンドの仕様

### 5.1 共通
- コマンド名は英語、説明文は日本語にする。
- サーバーの全員がコマンドを使える。使えるかどうかは、Discordの権限設定ではなくロールの判定で決める。
- **成功したときの返信は、全員に見える通常の返信**にする。Discordにより「〇〇さんが `/join` を使用しました」という表示が付く。確認ボタンは使わない。
- **弾いたときと失敗したときの返信は、本人にだけ見える返信（エフェメラル）**にする。
- ロールの変更は、メンバーのロール一覧をまとめて書き換える1回のAPI呼び出しで行う。途中で失敗して、どちらのロールもない状態が残るのを防ぐため。Discord.NETでこの方法が期待どおりに動くかは、実装のときに確認する。期待どおりでなければ、付与 → 解除の順に実行する方法に切り替え、その判断を記録する。
- `{user}` は実行した人へのメンションに置き換える。`AllowedMentions` で通知先を実行した本人だけに限定し、文言に `@everyone` やロールのメンションが含まれていても通知されないようにする。

### 5.2 `/join`
説明文：「部員ロールを受け取ります」

| 状況 | botの動き | 返信 |
|---|---|---|
| 部員ロールを持っている | 何もしない | 本人だけ：`JoinAlreadyMember` |
| 部員ロールを持っていない | 部員ロールを付与し、OBロールを持っていれば外す | 全員：`JoinSuccess` |

OBの人も部員ロールを持っていない人と同じように扱う。誤って卒業した人は、`/join` で自分で元に戻せる。

### 5.3 `/graduate`
説明文：「卒業してOBロールに切り替えます」

| 状況 | botの動き | 返信 |
|---|---|---|
| 部員ロールを持っていない | 何もしない | 本人だけ：`GraduateNotMember` |
| 部員ロールを持っている | 部員ロールを外してOBロールを付与する | 全員：`GraduateSuccess` |

## 6. エラー処理と運用

### 6.1 実行中のエラー
- コマンドの処理中に予期しない例外が起きた場合は、本人にだけ `Error` の文言を返し、詳しい内容をログに出す。
- レート制限は、Discord.NETが自動で待って再実行するので、特に処理しない。
- 返信は3秒以内に直接行う。deferは使わない。deferすると、その時点で全員に見せるか本人だけに見せるかを決める必要があり、「成功は全員、失敗は本人だけ」という方針と合わないため。
- Discordとの接続が切れた場合は、Discord.NETが自動で再接続する。
- プログラムが異常終了した場合は、systemdが10秒後に再起動する。

### 6.2 終了コード

| コード | 意味 | systemdの動き |
|---|---|---|
| 0 | 正常終了 | 再起動する（`Restart=always`） |
| 78 | 設定エラー | **再起動しない**（`RestartPreventExitStatus=78`） |
| それ以外 | 異常終了 | 10秒後に再起動する |

### 6.3 ログ
- 標準出力に出す。systemdでは `journalctl -u mpcman` で見られる。Dockerに移行しても `docker logs` で同じように見られる。
- Discord.NET内部のログも、`ILogger` にまとめる。

| レベル | 記録する内容 |
|---|---|
| 情報 | 起動と停止、コマンドの実行結果（コマンド名、ユーザーID、結果） |
| 警告 | 接続の切断と再接続 |
| エラー | 失敗の詳細 |

- ユーザーIDは記録する（トラブルのときに追跡するため。ユーザーの了承済み）。
- メッセージ本文とトークンは記録しない。

## 7. テストとCI

### 7.1 自動テスト（xUnit、`tests/MpcmanBot.Tests/`）
Discordにつながなくても確かめられる部分だけを自動テストにする。

- 設定の検証：IDが0や未設定のとき、トークンが空のときに、エラーになること
- 文言の既定値：項目が書かれていないときに、既定値が使われること
- `{user}` の置き換え：1つのとき、複数のとき、含まれないときに正しく置き換わること

コマンドの処理は、Discord.NETのクラスの偽物（モック）を作るのが難しいため、自動テストにしない。代わりに7.2の手動確認で確かめる。

`dotnet test` 一発で、すべてのテストを実行できるようにする。

### 7.2 手動確認（テスト用Discordサーバー）
本番の部活サーバーとは別のテスト用サーバーで確認する。`docs/operations.md` にリリース前のチェックリストとして載せる。

- [ ] 部員ロールなしで `/join` → 部員ロールが付き、全員にお祝いが見える
- [ ] OBで `/join` → 部員ロールが付いてOBロールが外れ、全員にお祝いが見える
- [ ] 部員で `/join` → ロールは変わらず、本人だけに返信が見える
- [ ] 部員で `/graduate` → OBになり、全員にお祝いが見える
- [ ] 部員でない人が `/graduate` → ロールは変わらず、本人だけに返信が見える
- [ ] 文言に `@everyone` を書いても、全員への通知が飛ばない
- [ ] トークンを空にして起動 → 終了コード78で終了し、再起動を繰り返さない
- [ ] botのロールを部員ロールより下にして起動 → 該当するエラーメッセージで終了する

### 7.3 GitHub Actions（ユーザーが作成する）
設定ファイル（`.github/workflows/`）はユーザーが自分で作る。実装する側は、次の要件を満たせるように下準備だけをする（発行プロファイルとテストの構成）。

- **自動チェック**：pushとプルリクエストのときに、ビルド、`dotnet format --verify-no-changes`、`dotnet test` を実行する。
- **リリース**：`v*` の形のタグをpushしたときに、`dotnet publish -c Release -p:PublishProfile=linux-x64` で配布物を作る。`appsettings.example.json` と一緒にGitHubのリリースページに添付する。
- 任意：matrixでWindows、macOS、Linuxの3つのOSでテストする。

Actionsができるまでは、手元のPCで発行して `scp` などでサーバーにコピーする。

## 8. 配布と運用

### 8.1 サーバー上のファイル配置

| 場所 | 中身 |
|---|---|
| `/opt/mpcman-bot/MpcmanBot` | 実行ファイル |
| `/etc/mpcman-bot/appsettings.json` | 設定と文言（パーミッション644） |
| `/etc/mpcman-bot/secrets.env` | `Bot__Token=...`（所有者root、パーミッション600） |
| `/etc/systemd/system/mpcman.service` | systemdの設定（リポジトリの `deploy/mpcman.service` をコピーする） |

### 8.2 systemdの設定

```ini
[Unit]
Description=MPCman Discord bot
After=network-online.target
Wants=network-online.target

[Service]
Type=notify
ExecStart=/opt/mpcman-bot/MpcmanBot
Environment=MPCMAN_CONFIG=/etc/mpcman-bot/appsettings.json
EnvironmentFile=/etc/mpcman-bot/secrets.env
Restart=always
RestartSec=10
RestartPreventExitStatus=78

DynamicUser=yes
ProtectSystem=strict
ProtectHome=yes
PrivateTmp=yes
NoNewPrivileges=yes

[Install]
WantedBy=multi-user.target
```

単一ファイルの実行ファイルが、`DynamicUser=yes` と `ProtectSystem=strict` の制限の下で問題なく起動するか（一時ファイルの展開などで書き込みが必要にならないか）は、実装のときに実機で確認する。

### 8.3 運用で使うコマンド

| やりたいこと | コマンド |
|---|---|
| 状態を確認する | `systemctl status mpcman` |
| 再起動する（設定変更の反映も） | `sudo systemctl restart mpcman` |
| ログを見る | `journalctl -u mpcman -f` |
| 更新する | リリースページからダウンロード → `sudo systemctl stop mpcman` → 実行ファイルを置き換える → `sudo systemctl start mpcman` |

### 8.4 Dockerへの移行（将来）
コードは変えずに、次の方法で移行できるようにしておく。
- ベースイメージは `mcr.microsoft.com/dotnet/runtime-deps` を使い、実行ファイルをコピーする。
- `secrets.env` はDocker Composeの `env_file:` でそのまま読み込む。
- `appsettings.json` はボリュームでマウントし、`MPCMAN_CONFIG` で場所を指定する。

## 9. ドキュメント

| ファイル | 対象 | 内容 |
|---|---|---|
| `README.md` | 全員 | botの概要と、各ドキュメントへの案内 |
| `docs/operations.md` | 運用担当 | 初回導入（Discord Developer Portalでのbot作成、招待URL、ロールの順位、開発者モードでのIDの調べ方、サーバーへの設置）、更新、設定変更、トラブル対応表、トークンが漏れたときの対応（Developer Portalでリセット → `secrets.env` を書き換え → 再起動）、リリース前のチェックリスト（7.2） |
| `docs/development.md` | 開発担当 | 開発環境の作り方（Windows、macOS、Linux）、User Secrets、テスト用サーバー、「どこを直せば何が変わるか」の早見表 |

「どこを直せば何が変わるか」の早見表には、少なくとも次の内容を載せる。

| 変えたいこと | 直す場所 |
|---|---|
| メッセージの文言 | `appsettings.json` の `Messages` |
| 対象のロールやサーバー | `appsettings.json` の `Bot` |
| 参加の条件 | `Commands/JoinCommand.cs` |
| 卒業の条件 | `Commands/GraduateCommand.cs` |
| コマンドの登録やDiscordへの接続 | `Worker.cs` |
| 設定の項目や検証 | `BotOptions.cs` |
