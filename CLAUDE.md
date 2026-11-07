# CLAUDE.md

サークル用 Discord Bot（v3）。C# / .NET 10 Worker + Discord.Net で実装する。

## ドキュメントのルール

- 機能（コマンドやイベント処理）を実装・変更したときは、その機能に必要な権限を **README.md** に記載・更新すること。対象は以下のとおり。
  - Bot の招待時に必要な OAuth2 スコープ（例: `bot`, `applications.commands`）
  - Bot に必要な権限（例: ロールの管理）
  - 必要な Gateway Intents（特権 Intent が必要な場合は Developer Portal での有効化手順も）
  - ロールの並び順など、サーバー側で必要な設定（例: Bot のロールを操作対象ロールより上に置く）
- 機能ごとに「どの権限がなぜ必要か」がわかるように書くこと。
