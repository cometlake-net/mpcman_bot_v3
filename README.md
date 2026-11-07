# mpcman_bot_v3

サークル用の Discord Bot（v3）。C# / .NET 10 と Discord.Net で動きます。

## コマンド

| コマンド | 内容 | 返信 |
|---|---|---|
| `/join` | 「部員」ロールを持っていない人に「部員」ロールを付与します。ほかのロール（OB など）はそのまま残ります。 | 成功は公開、すでに部員なら本人にだけ |
| `/graduate` | 「部員」ロールを外し、「OB」ロールを付与します。部員でない人は使えません。 | 成功は公開、部員でなければ本人にだけ |
| `/help` | コマンドの一覧を表示します。 | 本人にだけ |

コマンドはサーバー内でのみ使えます（DM では使えません）。

## 必要な権限と設定

### Bot の招待（OAuth2 スコープ）

Developer Portal の「OAuth2 → URL Generator」で、次の2つのスコープにチェックを入れて招待 URL を作ります。

| スコープ | 必要な理由 |
|---|---|
| `bot` | Bot をサーバーに参加させるため |
| `applications.commands` | `/join`・`/graduate`・`/help` をサーバーに登録するため |

### Bot の権限

| 権限 | 必要な理由 | 使う機能 |
|---|---|---|
| ロールの管理（Manage Roles） | メンバーに「部員」「OB」ロールを付けたり外したりするため | `/join`、`/graduate` |

`/help` に必要な権限はありません。スラッシュコマンドの返信はチャンネルへの「メッセージを送信」権限がなくても送れます。

### Gateway Intents

使うのは `Guilds` だけです。**特権 Intent（Server Members / Message Content / Presence）は不要**なので、Developer Portal の「Bot → Privileged Gateway Intents」で有効にする必要はありません。

### サーバー側の設定

- 「サーバー設定 → ロール」で、**Bot のロールを「部員」「OB」より上に並べてください。** Bot は自分より下のロールしか付け外しできません。下にあると `/join`・`/graduate` が失敗し、ログに `50013`（Missing Permissions）が出ます。
- サーバーに「部員」「OB」という名前のロールを作っておいてください（名前は設定で変えられます）。

## 設定項目

| キー | 必須 | 既定値 | 内容 |
|---|---|---|---|
| `Bot:Token` | ○ | — | Bot トークン。**リポジトリに書かないでください。** |
| `Bot:GuildId` | ○ | — | コマンドを登録するサーバーの ID |
| `Bot:MemberRoleName` | | `部員` | 部員ロールの名前 |
| `Bot:AlumniRoleName` | | `OB` | OB ロールの名前 |

`Bot:Token` か `Bot:GuildId` が設定されていないと、起動時にエラーで止まります。
`Bot:GuildId` のサーバーに Bot が参加していない場合は、エラーをログに出したうえで動き続けます。

サーバー ID は、Discord の「ユーザー設定 → 詳細設定」で開発者モードを有効にし、サーバー名を右クリック →「サーバー ID をコピー」で取得できます。

## 起動方法

### 開発時

トークンとサーバー ID をユーザーシークレットに保存します。

```
dotnet user-secrets --project src/mpcman_bot_v3 set "Bot:Token" "<トークン>"
dotnet user-secrets --project src/mpcman_bot_v3 set "Bot:GuildId" "<サーバーID>"
dotnet run --project src/mpcman_bot_v3
```

### Docker

```
docker build -t mpcman_bot_v3 .
docker run -e Bot__Token=<トークン> -e Bot__GuildId=<サーバーID> mpcman_bot_v3
```

環境変数では、区切りの `:` を `__`（アンダースコア2つ）に置き換えます。

## テスト

```
dotnet test
```
