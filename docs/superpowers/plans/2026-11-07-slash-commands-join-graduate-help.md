# `/join`・`/graduate`・`/help` スラッシュコマンド 実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** v2 の `$join`・`$graduate` をスラッシュコマンド `/join`・`/graduate` として移植し、本人にだけ表示する `/help` を追加する。

**Architecture:** .NET 10 Worker（Generic Host）の `BackgroundService` が `DiscordSocketClient` を起動し、Discord.Net の `InteractionService` でモジュールクラスのコマンドを1つのサーバーに登録・実行する。ロールの判断と付け外しの順序は、Discord に依存しない小さなクラス（`Roles/`）に分け、xUnit で単体テストする。モジュールは薄く保ち、Discord とのやりとりだけを担う。

**Tech Stack:** C# / .NET 10、Discord.Net 3.20.2（`Discord.Net.Interactions` 含む）、Microsoft.Extensions.Hosting、xUnit

**Spec:** `docs/superpowers/specs/2026-11-07-slash-commands-join-graduate-help-design.md`

## Global Constraints

- Gateway Intents は `GatewayIntents.Guilds` のみ。特権 Intent（GuildMembers / MessageContent / GuildPresences）は使わない。
- コマンドは `Bot:GuildId` のサーバーにだけギルドコマンドとして登録する（グローバル登録はしない）。DM では使えないようにする（`[CommandContextType(InteractionContextType.Guild)]`）。
- ロール名の初期値: 部員ロール `部員`、OB ロール `OB`。設定で変更可能。
- 返信の文言は spec のとおり一字一句（Task 2 の `Messages` に集約）。
- 成功時の `/join`・`/graduate` は公開、断り・エラー・`/help` はエフェメラル（`ephemeral: true`）。
- Bot トークンはリポジトリに置かない（開発はユーザーシークレット、Docker は環境変数 `Bot__Token`）。
- 機能に必要な権限は README.md に書く（CLAUDE.md のルール）。
- 名前空間に `Discord` を含めない（ライブラリの `Discord` 名前空間と衝突するため）。

## Review Focus

1. **Bot のロールが「部員」「OB」より下、または「ロールの管理」権限がない** → Discord が `HttpException`（50013 Missing Permissions）を返す。本人にだけ設定エラーを返し、原因をログに出すこと（Task 4 Step 6 で手動確認）。
2. **設定したロール名がサーバーに存在しない** → `RoleFinder.Find` が `null`。本人にだけ設定エラーを返し、ログに出すこと（Task 1 のテスト、Task 4 Step 6 で手動確認）。
3. **`/graduate` の途中で失敗（OB 付与後に部員の削除が失敗）** → 「部員」と「OB」の両方を持つ状態で止まり、どちらもない状態にはならないこと（Task 2 のテスト）。
4. **すでに「OB」も持っている部員が `/graduate`** → OB の付与は呼ばずに部員だけ外し、成功扱い（Task 2 のテスト）。
5. **コマンド処理中の予期しない例外、または再接続で `Ready` が複数回発火** → 利用者には本人にだけ設定エラーが返り「アプリケーションが応答しませんでした」にならないこと。モジュールの読み込みは1回だけで、再登録しても壊れないこと（Task 4 Step 6 で手動確認）。

---

## File Structure

```
mpcman_bot_v3.slnx                          … テストプロジェクトを追加
README.md                                   … 新規: 権限・設定・起動方法
src/mpcman_bot_v3/
  Program.cs                                … DI 登録を差し替え
  Worker.cs                                 … 削除
  appsettings.json                          … "Bot" セクションを追加
  Bot/BotOptions.cs                         … 設定クラス
  Bot/DiscordBotService.cs                  … 接続・コマンド登録・実行・ログ橋渡し
  Bot/GuildUserRoleEditor.cs                … IGuildUser を IMemberRoleEditor に合わせるアダプタ
  Roles/RoleFinder.cs                       … 名前からロール ID を探す（重複検出つき）
  Roles/IMemberRoleEditor.cs                … メンバーのロール操作の抽象
  Roles/MembershipActions.cs                … join / graduate の判断と操作順序
  Roles/Messages.cs                         … 返信文言
  Help/HelpText.cs                          … /help の本文を組み立てる
  Modules/MembershipModule.cs               … /join, /graduate
  Modules/HelpModule.cs                     … /help
tests/mpcman_bot_v3.Tests/
  mpcman_bot_v3.Tests.csproj
  RoleFinderTests.cs
  MembershipActionsTests.cs
  MessagesTests.cs
  HelpTextTests.cs
```

---

### Task 1: テストプロジェクトと `RoleFinder`

**Files:**
- Create: `tests/mpcman_bot_v3.Tests/mpcman_bot_v3.Tests.csproj`（`dotnet new xunit`）
- Modify: `mpcman_bot_v3.slnx`（`/tests/` フォルダにテストプロジェクトを追加）
- Create: `src/mpcman_bot_v3/Roles/RoleFinder.cs`
- Test: `tests/mpcman_bot_v3.Tests/RoleFinderTests.cs`

**Interfaces:**
- Produces:
  - `namespace mpcman_bot_v3.Roles; public sealed record RoleMatch(ulong Id, bool HasDuplicates);`
  - `public static class RoleFinder { public static RoleMatch? Find(IEnumerable<(ulong Id, string Name)> roles, string name); }`
    完全一致（大文字小文字を区別）。見つからなければ `null`。複数一致なら列挙順で最初の1つの ID と `HasDuplicates = true`。

- [ ] **Step 1: テストプロジェクトを作成してソリューションに追加**

Run:
```
dotnet new xunit -o tests/mpcman_bot_v3.Tests -f net10.0
dotnet add tests/mpcman_bot_v3.Tests reference src/mpcman_bot_v3/mpcman_bot_v3.csproj
dotnet sln mpcman_bot_v3.slnx add --solution-folder tests tests/mpcman_bot_v3.Tests/mpcman_bot_v3.Tests.csproj
```
テンプレートの `UnitTest1.cs` は削除する。
Expected: `dotnet build mpcman_bot_v3.slnx` が成功。

- [ ] **Step 2: 失敗するテストを書く**（`RoleFinderTests`）

```csharp
static readonly (ulong, string)[] Roles = [(1, "@everyone"), (10, "部員"), (20, "OB")];

[Fact] public void Find_ReturnsId_WhenNameMatches()
    => Assert.Equal(new RoleMatch(10, false), RoleFinder.Find(Roles, "部員"));

[Fact] public void Find_ReturnsNull_WhenNameMissing()
    => Assert.Null(RoleFinder.Find(Roles, "幹部"));

[Fact] public void Find_IsExactMatch()
    => Assert.Null(RoleFinder.Find(Roles, "ob"));

[Fact] public void Find_ReturnsFirstAndFlagsDuplicates()
    => Assert.Equal(new RoleMatch(10, true), RoleFinder.Find([(10, "部員"), (11, "部員")], "部員"));
```

- [ ] **Step 3: 失敗を確認**

Run: `dotnet test tests/mpcman_bot_v3.Tests --filter RoleFinderTests`
Expected: `RoleFinder` が未定義でビルド失敗。

- [ ] **Step 4: `RoleFinder.Find` を実装**

- [ ] **Step 5: テストが通ることを確認**

Run: `dotnet test tests/mpcman_bot_v3.Tests --filter RoleFinderTests`
Expected: 4 件 PASS

- [ ] **Step 6: コミット**

```
git add mpcman_bot_v3.slnx tests/ src/mpcman_bot_v3/Roles/RoleFinder.cs
git commit -m "feat: テストプロジェクトとロール名検索 RoleFinder を追加"
```

---

### Task 2: `MembershipActions` と返信文言 `Messages`

**Files:**
- Create: `src/mpcman_bot_v3/Roles/IMemberRoleEditor.cs`
- Create: `src/mpcman_bot_v3/Roles/MembershipActions.cs`
- Create: `src/mpcman_bot_v3/Roles/Messages.cs`
- Test: `tests/mpcman_bot_v3.Tests/MembershipActionsTests.cs`, `tests/mpcman_bot_v3.Tests/MessagesTests.cs`

**Interfaces:**
- Produces（すべて `namespace mpcman_bot_v3.Roles`）:
  - `public interface IMemberRoleEditor { IReadOnlyCollection<ulong> RoleIds { get; } Task AddRoleAsync(ulong roleId); Task RemoveRoleAsync(ulong roleId); }`
  - `public enum JoinResult { Granted, AlreadyMember }`
  - `public enum GraduateResult { Graduated, NotMember }`
  - `public static class MembershipActions`
    - `public static Task<JoinResult> JoinAsync(IMemberRoleEditor member, ulong memberRoleId)` — 部員を持っていなければ `AddRoleAsync(memberRoleId)` して `Granted`。持っていれば何もせず `AlreadyMember`。他のロールは見ない。
    - `public static Task<GraduateResult> GraduateAsync(IMemberRoleEditor member, ulong memberRoleId, ulong alumniRoleId)` — 部員がなければ何もせず `NotMember`。あれば **OB を先に付与**（すでに持っていれば付与を呼ばない）→ **その後**部員を削除 → `Graduated`。例外は握りつぶさず呼び出し元に伝える。
  - `public static class Messages`
    - `string JoinGranted(string mention)` → `$"{mention}へサーバーの利用権限を付与しました。これからよろしく！"`
    - `const string AlreadyMember = "あなたは既に利用権限を持っています！"`
    - `string Graduated(string mention)` → `$"{mention}は卒業しOBとなりました。これからもよろしく！"`
    - `const string NotMember = "そのコマンドを使用する権限がありません！"`
    - `const string ConfigError = "Botの設定に問題があり処理できませんでした。管理者に連絡してください。"`

- [ ] **Step 1: 失敗するテストを書く**

テスト内に `FakeMember : IMemberRoleEditor` を作る。ロール集合を持ち、呼び出しを `List<string> Calls`（`"add:20"`, `"remove:10"` の形）に記録し、`ThrowOnRemove` / `ThrowOnAdd` が真なら該当操作で `InvalidOperationException` を投げる（投げる前に集合は変えない）。部員 = 10、OB = 20、ブースター = 30。

`MembershipActionsTests`:
```csharp
Join_GrantsMember_WhenNoRoles            // roles {} → Granted, Calls == ["add:10"]
Join_GrantsMember_WhenHasOtherRoles      // roles {30} → Granted, Calls == ["add:10"]
Join_GrantsMember_AndKeepsOb_WhenOb      // roles {20} → Granted, Calls == ["add:10"], RoleIds ⊇ {10,20}
Join_DoesNothing_WhenAlreadyMember       // roles {10} → AlreadyMember, Calls 空
Graduate_SwapsMemberForOb                // roles {10} → Graduated, Calls == ["add:20","remove:10"]
Graduate_OnlyRemovesMember_WhenAlreadyOb // roles {10,20} → Graduated, Calls == ["remove:10"]
Graduate_DoesNothing_WhenNotMember       // roles {20} → NotMember, Calls 空
Graduate_KeepsBothRoles_WhenRemoveFails  // roles {10}, ThrowOnRemove → 例外が伝わる, RoleIds == {10,20}
Graduate_DoesNotRemove_WhenAddFails      // roles {10}, ThrowOnAdd → 例外が伝わる, Calls に "remove:10" なし, RoleIds == {10}
```

`MessagesTests`: `Messages.JoinGranted("<@1>")` が `"<@1>へサーバーの利用権限を付与しました。これからよろしく！"`、`Messages.Graduated("<@1>")` が `"<@1>は卒業しOBとなりました。これからもよろしく！"` に等しい。

- [ ] **Step 2: 失敗を確認**

Run: `dotnet test tests/mpcman_bot_v3.Tests --filter "MembershipActionsTests|MessagesTests"`
Expected: 型が未定義でビルド失敗。

- [ ] **Step 3: `IMemberRoleEditor`・`MembershipActions`・`Messages` を上記シグネチャで実装**

- [ ] **Step 4: テストが通ることを確認**

Run: `dotnet test tests/mpcman_bot_v3.Tests`
Expected: 全件 PASS

- [ ] **Step 5: コミット**

```
git add src/mpcman_bot_v3/Roles tests/mpcman_bot_v3.Tests
git commit -m "feat: join/graduate のロール判断と返信文言を追加"
```

---

### Task 3: `/help` の本文 `HelpText`

**Files:**
- Create: `src/mpcman_bot_v3/Help/HelpText.cs`
- Test: `tests/mpcman_bot_v3.Tests/HelpTextTests.cs`

**Interfaces:**
- Produces: `namespace mpcman_bot_v3.Help; public static class HelpText { public static string Build(IEnumerable<(string Name, string Description)> commands); }`
  各行 `/{Name} — {Description}`（ダッシュは U+2014 の em dash、前後に半角スペース）を入力順に `\n` で連結。末尾に改行なし。

- [ ] **Step 1: 失敗するテストを書く**

```csharp
[Fact] public void Build_ListsCommandsInOrder()
    => Assert.Equal(
        "/join — 部員のロールが付与されます\n/graduate — 部員からOBになります\n/help — コマンド一覧を表示します",
        HelpText.Build([("join", "部員のロールが付与されます"), ("graduate", "部員からOBになります"), ("help", "コマンド一覧を表示します")]));
```

- [ ] **Step 2: 失敗を確認** — Run: `dotnet test tests/mpcman_bot_v3.Tests --filter HelpTextTests` / Expected: ビルド失敗

- [ ] **Step 3: `HelpText.Build` を実装**

- [ ] **Step 4: 通ることを確認** — Run: 同上 / Expected: PASS

- [ ] **Step 5: コミット**

```
git add src/mpcman_bot_v3/Help tests/mpcman_bot_v3.Tests/HelpTextTests.cs
git commit -m "feat: /help の本文組み立てを追加"
```

---

### Task 4: Bot のホスティングとコマンドモジュール

**Files:**
- Create: `src/mpcman_bot_v3/Bot/BotOptions.cs`
- Create: `src/mpcman_bot_v3/Bot/DiscordBotService.cs`
- Create: `src/mpcman_bot_v3/Bot/GuildUserRoleEditor.cs`
- Create: `src/mpcman_bot_v3/Modules/MembershipModule.cs`
- Create: `src/mpcman_bot_v3/Modules/HelpModule.cs`
- Modify: `src/mpcman_bot_v3/Program.cs`
- Modify: `src/mpcman_bot_v3/appsettings.json`
- Modify: `src/mpcman_bot_v3/mpcman_bot_v3.csproj`（パッケージ追加）
- Delete: `src/mpcman_bot_v3/Worker.cs`

**Interfaces:**
- Consumes: `RoleFinder.Find`, `RoleMatch`（Task 1）、`IMemberRoleEditor`, `MembershipActions`, `JoinResult`, `GraduateResult`, `Messages`（Task 2）、`HelpText.Build`（Task 3）
- Produces:
  - `namespace mpcman_bot_v3.Bot; public sealed class BotOptions { public const string SectionName = "Bot"; [Required] public string Token { get; set; } = ""; [Range(typeof(ulong), "1", "18446744073709551615")] public ulong GuildId { get; set; } public string MemberRoleName { get; set; } = "部員"; public string AlumniRoleName { get; set; } = "OB"; }`
  - `public sealed class GuildUserRoleEditor(IGuildUser user) : IMemberRoleEditor` — `RoleIds` は `user.RoleIds`、追加・削除は `user.AddRoleAsync` / `user.RemoveRoleAsync`。
  - `public sealed class DiscordBotService(DiscordSocketClient client, InteractionService interactions, IServiceProvider services, IOptions<BotOptions> options, ILogger<DiscordBotService> logger) : BackgroundService`

- [ ] **Step 1: 設定と DI**

`appsettings.json` に追加（トークンとサーバー ID は書かない）:
```json
"Bot": { "MemberRoleName": "部員", "AlumniRoleName": "OB" }
```
`ValidateDataAnnotations` のため、本体に `Microsoft.Extensions.Options.DataAnnotations` パッケージ（`Microsoft.Extensions.Hosting` と同じ 10.0.x）を追加する。

`Program.cs`:
- `AddOptions<BotOptions>().BindConfiguration(BotOptions.SectionName).ValidateDataAnnotations().ValidateOnStart()`（トークン未設定・サーバー ID 未設定なら起動時に分かるエラーで止まる）
- `DiscordSocketClient` をシングルトン登録: `new DiscordSocketConfig { GatewayIntents = GatewayIntents.Guilds }`
- `InteractionService` をシングルトン登録: `new InteractionService(sp.GetRequiredService<DiscordSocketClient>())`
- `AddHostedService<DiscordBotService>()`。`Worker` の登録と `Worker.cs` を削除。

- [ ] **Step 2: `DiscordBotService`**

`ExecuteAsync` の流れ:
1. `client.Log` と `interactions.Log` を `ILogger` に流す（`LogSeverity` → `LogLevel` に対応付け）。
2. `await interactions.AddModulesAsync(typeof(Program).Assembly, services)` — **`Ready` の外で1回だけ**呼ぶ（Review Focus 5）。
3. `client.Ready += ...`: `client.GetGuild(GuildId)` が `null` なら `LogError`（サーバー ID が間違っているか Bot が未参加）して登録をスキップ、プロセスは落とさない。見つかれば `interactions.RegisterCommandsToGuildAsync(GuildId, true)`。
4. `client.InteractionCreated += ...`: `new SocketInteractionContext(client, interaction)` で `interactions.ExecuteCommandAsync(ctx, services)`。
5. `interactions.InteractionExecuted += ...`: `!result.IsSuccess` なら `LogError`（`result.Error`, `result.ErrorReason`）。`ctx.Interaction.HasResponded` が偽なら `RespondAsync(Messages.ConfigError, ephemeral: true)`（Review Focus 5）。
6. `LoginAsync(TokenType.Bot, Token)` → `StartAsync()` → `await Task.Delay(Timeout.Infinite, stoppingToken)`（キャンセルは正常終了扱い）→ `StopAsync()` / `LogoutAsync()`。

- [ ] **Step 3: `MembershipModule`**

`public sealed class MembershipModule(IOptions<BotOptions> options, ILogger<MembershipModule> logger) : InteractionModuleBase<SocketInteractionContext>`、クラスに `[CommandContextType(InteractionContextType.Guild)]`。

- `[SlashCommand("join", "部員のロールが付与されます")] public Task JoinAsync()`
- `[SlashCommand("graduate", "部員からOBになります")] public Task GraduateAsync()`

共通の流れ:
1. `Context.Guild.Roles` を `(r.Id, r.Name)` にして `RoleFinder.Find`。`null` なら `LogError`（どのロール名が見つからないか）し `RespondAsync(Messages.ConfigError, ephemeral: true)` で終了。`HasDuplicates` なら `LogWarning`。
2. `new GuildUserRoleEditor((IGuildUser)Context.User)` で `MembershipActions.JoinAsync` / `GraduateAsync`。
3. 結果ごとに返信:
   - `Granted` → `RespondAsync(Messages.JoinGranted(Context.User.Mention))`（公開）
   - `AlreadyMember` → `RespondAsync(Messages.AlreadyMember, ephemeral: true)`
   - `Graduated` → `RespondAsync(Messages.Graduated(Context.User.Mention))`（公開）
   - `NotMember` → `RespondAsync(Messages.NotMember, ephemeral: true)`
4. `Discord.Net.HttpException` を捕まえたら `LogError`（`ex.DiscordCode`、`ex.Reason` を含め、50013 なら「Bot のロール順か『ロールの管理』権限を確認」と添える）し、`RespondAsync(Messages.ConfigError, ephemeral: true)`（Review Focus 1）。

- [ ] **Step 4: `HelpModule`**

`public sealed class HelpModule(InteractionService interactions) : InteractionModuleBase<SocketInteractionContext>`、`[CommandContextType(InteractionContextType.Guild)]`。
`[SlashCommand("help", "コマンド一覧を表示します")]` で `HelpText.Build(interactions.SlashCommands.Select(c => (c.Name, c.Description)))` を `ephemeral: true` で返す。

- [ ] **Step 5: ビルドと既存テスト**

Run: `dotnet build mpcman_bot_v3.slnx` と `dotnet test tests/mpcman_bot_v3.Tests`
Expected: 警告なしでビルド成功、全テスト PASS。
加えて `Bot:Token` 未設定で `dotnet run --project src/mpcman_bot_v3` → `BotOptions` の検証エラーで止まることを確認。

- [ ] **Step 6: 実サーバーでの手動確認**（ユーザーがトークンとサーバー ID を用意する）

```
dotnet user-secrets --project src/mpcman_bot_v3 set "Bot:Token" "<トークン>"
dotnet user-secrets --project src/mpcman_bot_v3 set "Bot:GuildId" "<サーバーID>"
dotnet run --project src/mpcman_bot_v3
```
確認すること:
- 起動ログにエラーがなく、コマンド候補に `/join` `/graduate` `/help` が出る
- ロールなしのアカウントで `/join` → 部員付与・公開メッセージ。もう一度 `/join` → 本人だけに `あなたは既に利用権限を持っています！`
- `/graduate` → 部員が外れ OB 付与・公開メッセージ。もう一度 → 本人だけに `そのコマンドを使用する権限がありません！`
- `/help` → 本人だけに3行の一覧
- Bot のロールを「部員」より下に下げて `/join` → 本人だけに設定エラー、ログに 50013 と対処のヒント（Review Focus 1）
- `Bot:MemberRoleName` を存在しない名前にして `/join` → 本人だけに設定エラー、ログにロール名（Review Focus 2）
- `Bot:GuildId` を Bot が参加していない ID にして起動 → エラーログが出てプロセスは動き続ける

- [ ] **Step 7: コミット**

```
git add -A src/mpcman_bot_v3
git commit -m "feat: Discord 接続と /join /graduate /help コマンドを実装"
```

---

### Task 5: README.md

**Files:**
- Create: `README.md`

- [ ] **Step 1: README.md を書く**

含める内容（CLAUDE.md のルール。機能ごとに「どの権限がなぜ必要か」がわかるように）:
- 概要と、使えるコマンド（`/join` `/graduate` `/help` と挙動の一行説明）
- **招待時の OAuth2 スコープ**: `bot`、`applications.commands`（スラッシュコマンドの登録に必要）
- **Bot 権限**: ロールの管理（Manage Roles）— `/join`・`/graduate` でロールを付け外しするため
- **Gateway Intents**: `Guilds` のみ。特権 Intent は不要（Developer Portal での有効化は不要）
- **サーバー側の設定**: サーバー設定 → ロールで、Bot のロールを「部員」「OB」より上に置く（下だと 50013 で失敗する）
- **設定項目**: `Bot:Token`、`Bot:GuildId`（必須）、`Bot:MemberRoleName`（既定 `部員`）、`Bot:AlumniRoleName`（既定 `OB`）。開発時はユーザーシークレット、Docker では環境変数 `Bot__Token` / `Bot__GuildId`
- **起動方法**: `dotnet run --project src/mpcman_bot_v3`、Docker では `docker build -t mpcman_bot_v3 .` → `docker run -e Bot__Token=... -e Bot__GuildId=... mpcman_bot_v3`
- **テスト**: `dotnet test`

- [ ] **Step 2: コミット**

```
git add README.md
git commit -m "docs: README に必要な権限と設定・起動方法を記載"
```
