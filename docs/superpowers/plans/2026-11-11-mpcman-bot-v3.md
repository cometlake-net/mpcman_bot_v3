# MPCman bot v3 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 部活動Discordサーバーで、本人が `/join`（部員ロールの付与）と `/graduate`（部員からOBへの切り替え）を実行できるbotを、C#／Discord.NETで作る。

**Architecture:** Worker Serviceテンプレートから作る常駐プログラム。`Program.cs` が設定とDIを組み立て、`Worker.cs` がDiscordへの接続・起動時の検証・コマンド登録を行い、`Commands/` の各ファイルがスラッシュコマンド1つ分の処理をすべて持つ。設定は `appsettings.json`、トークンは環境変数から読み、設定エラーは終了コード78で終了する。

**Tech Stack:** C#、.NET 10（SDK 10.0.x）、Discord.Net 3.x、Microsoft.Extensions.Hosting（Generic Host）、Microsoft.Extensions.Hosting.Systemd、xUnit、systemd

**Spec:** `docs/superpowers/specs/2026-11-10-mpcman-bot-v3-design.md`（実装者は必ず両方を読むこと。メッセージの文言、systemdの設定、ファイル配置の正確な値は仕様書にある）

## Global Constraints

- ターゲットは `net10.0`。null許容参照型（`<Nullable>enable</Nullable>`）を有効にする。
- Discord.Netは最新の安定版3.x（3.20.2以降）を使う。プレリリース版は使わない。
- Gateway Intentは `GatewayIntents.Guilds` だけ。メッセージ本文を読む処理は書かない。
- スラッシュコマンドは `Bot:GuildId` のサーバー専用として登録する。グローバルには登録しない。
- 設定エラーのときの終了コードは **78**。
- トークンは環境変数 `Bot__Token`（設定キー `Bot:Token`）からだけ読む。JSONのひな形には書かない。ログにも出さない。
- メッセージの既定の文言は、仕様書4.1のJSONにある文字列と一字一句同じにする。
- linux-x64向けの設定（`RuntimeIdentifier`、`SelfContained`、`PublishSingleFile`）は発行プロファイルだけに書く。`.csproj` には書かない。
- スラッシュコマンドごとに1ファイル。判定用のクラスや、テストのためだけのインターフェースは作らない。`Worker.cs` の名前は変えない。
- コメントは日本語で、「なぜそうしているか」だけを書く。
- 改行コードはLF。各タスクの最後に `dotnet format --verify-no-changes` が通ること。
- `.github/workflows/` は作らない（ユーザーが自分で作る）。
- ブランチは `master`。コミットメッセージは日本語で書き、末尾に `Co-Authored-By: Claude Fable 5.2 <noreply@anthropic.com>` を付ける。

## 人間が用意するもの

Task 3の最後とTask 4・5の確認には、実際のDiscordとDebianが必要になる。該当するステップには【人間】と書いてある。実装者はそこで止まり、ユーザーに確認を依頼する。

- テスト用Discordサーバー（「部員」「OB」に相当するロール2つを作っておく）
- テスト用botアプリケーション（Discord Developer Portalで作成し、スコープ `bot` と `applications.commands`、権限「ロールの管理」「メッセージの送信」でテスト用サーバーに招待する）
- 上記のトークン、サーバーID、ロールID 2つ
- 確認用のアカウント（サーバーのオーナーではないもの。できれば2つ）
- x86-64のDebianマシン（Task 5の確認用）

## Review Focus

仕様書には書かれていないが、運用で起きやすい入力。1〜5はテストまたは確認コマンドを該当タスクに入れてある。

1. **`appsettings.json` のJSONの書き間違い**（引用符やかっこの閉じ忘れ）：スタックトレースを出して再起動を繰り返すのではなく、原因を1行で出して終了コード78で終了する。→ Task 3 Step 4
2. **IDに数字以外を書く**（`"GuildId": "abc"`）、または数字を引用符で囲む（`"GuildId": "123"`）：前者は終了コード78、後者はそのまま動く。→ Task 2 Step 1、Task 3 Step 4
3. **文言を空文字にする**（`"JoinSuccess": ""`）：Discordは空のメッセージを拒否するので、既定の文言を使う。→ Task 2 Step 1
4. **`secrets.env` のトークンの前後に空白や改行が入る**：前後の空白を取り除いて使う。空白だけなら未設定として扱う。→ Task 2 Step 1
5. **`MemberRoleId` と `ObRoleId` に同じIDを書く**：`/graduate` が何も変えないのにお祝いを出してしまうので、起動時に設定エラーにする。→ Task 2 Step 1

自動では確かめられないので、Task 4の手動確認に入れたもの：実行者がbotより上位のロールや連携ロール（ブーストなど）を持っている場合に、ほかのロールが変わらないこと。

---

### Task 1: プロジェクトの土台

**Files:**
- Create: `MpcmanBot.sln`、`.editorconfig`、`.gitattributes`、`.gitignore`
- Create: `src/MpcmanBot/`（`dotnet new worker` で生成。`MpcmanBot.csproj`、`Program.cs`、`Worker.cs`、`appsettings.Development.json`、`Properties/launchSettings.json`）
- Create: `src/MpcmanBot/appsettings.example.json`、`src/MpcmanBot/Properties/PublishProfiles/linux-x64.pubxml`
- Create: `tests/MpcmanBot.Tests/MpcmanBot.Tests.csproj`

**Interfaces:**
- Produces: `dotnet build`、`dotnet test`、`dotnet format --verify-no-changes`、`dotnet publish src/MpcmanBot -c Release -p:PublishProfile=linux-x64` がリポジトリのルートから通る状態。配布物の出力先は `artifacts/linux-x64/`。

- [ ] **Step 1: ひな形を生成する**

```bash
dotnet new sln -n MpcmanBot --format sln
dotnet new worker -n MpcmanBot -o src/MpcmanBot --framework net10.0
dotnet new xunit -n MpcmanBot.Tests -o tests/MpcmanBot.Tests --framework net10.0
dotnet sln add src/MpcmanBot tests/MpcmanBot.Tests
dotnet add tests/MpcmanBot.Tests reference src/MpcmanBot
dotnet add src/MpcmanBot package Discord.Net
dotnet add src/MpcmanBot package Microsoft.Extensions.Hosting.Systemd
dotnet new gitignore
dotnet new editorconfig
```

`--format sln` は、.NET 10の既定である `.slnx` ではなく、仕様書どおりの `.sln` を作るために付ける。xUnitテンプレートが作る `UnitTest1.cs` は削除する。`Program.cs` と `Worker.cs` はTask 3まで生成されたままにしておく。

- [ ] **Step 2: 改行コードと無視するファイルを設定する**

- `.gitattributes`：`* text=auto eol=lf` の1行。
- `.editorconfig`：`[*]` に `end_of_line = lf`、`charset = utf-8`、`insert_final_newline = true` を設定する。
- `.gitignore` に追記：`artifacts/` と `src/MpcmanBot/appsettings.json`（開発者ごとのIDが入るのでコミットしない）。

- [ ] **Step 3: 設定のひな形を作る**

テンプレートが作った `src/MpcmanBot/appsettings.json` を `appsettings.example.json` に名前を変える。中身は、仕様書4.1のJSON（`Bot` と `Messages`）に、テンプレートの `Logging` セクションを加えたものにする。`appsettings.Development.json` は `Logging` だけのまま残す。

- [ ] **Step 4: `.csproj` と発行プロファイルを設定する**

`MpcmanBot.csproj`：
- `<InvariantGlobalization>true</InvariantGlobalization>` を追加する。
- `appsettings.example.json` は、ビルドの出力先と発行先の両方にコピーする。
- `appsettings.json` と `appsettings.Development.json` は、発行先にコピーしない（`CopyToPublishDirectory="Never"`）。開発者の設定が配布物に混ざるのを防ぐため。

`Properties/PublishProfiles/linux-x64.pubxml`：`Configuration=Release`、`RuntimeIdentifier=linux-x64`、`SelfContained=true`、`PublishSingleFile=true`、`DebugType=embedded`、`PublishDir=$(MSBuildProjectDirectory)/../../artifacts/linux-x64/`。

- [ ] **Step 5: 確認する**

```bash
dotnet format && dotnet build && dotnet test && dotnet format --verify-no-changes
dotnet publish src/MpcmanBot -c Release -p:PublishProfile=linux-x64
file artifacts/linux-x64/MpcmanBot && ls artifacts/linux-x64/
```

Expected: ビルドと書式チェックが成功する。`file` の結果に `ELF 64-bit` と `x86-64` が含まれる。`ls` の結果に `MpcmanBot` と `appsettings.example.json` があり、`appsettings.json` と `appsettings.Development.json` はない。

- [ ] **Step 6: コミットする**

```bash
git add -A && git commit -m "chore: Worker Serviceのひな形とビルド設定を追加"
```

---

### Task 2: 設定のクラスと検証

**Files:**
- Create: `src/MpcmanBot/BotOptions.cs`（下の4つの型をすべてこのファイルに置く）
- Test: `tests/MpcmanBot.Tests/BotOptionsTests.cs`、`tests/MpcmanBot.Tests/MessageOptionsTests.cs`

**Interfaces:**
- Produces（名前空間 `MpcmanBot`）：

```csharp
public sealed class BotOptions
{
    public const string SectionName = "Bot";
    public const int ConfigErrorExitCode = 78;
    public ulong GuildId { get; set; }
    public ulong MemberRoleId { get; set; }
    public ulong ObRoleId { get; set; }
    public string Token { get; set; }          // 代入時に前後の空白を取り除く。既定値は ""
}

public sealed class MessageOptions
{
    public const string SectionName = "Messages";
    public string JoinSuccess { get; set; }        // 以下5つとも、既定値は仕様書4.1の文言。
    public string JoinAlreadyMember { get; set; }  // 空文字や空白だけを代入されたら、今の値を保つ
    public string GraduateSuccess { get; set; }
    public string GraduateNotMember { get; set; }
    public string Error { get; set; }
    public static string Format(string template, string mention);  // "{user}" をすべて mention に置き換える
}

public sealed class BotOptionsValidator : IValidateOptions<BotOptions>
```

- [ ] **Step 1: 失敗するテストを書く**

両方のテストクラスで使う補助メソッド（各クラスにprivateで置く）：

```csharp
static T Bind<T>(string section, params (string Key, string? Value)[] values) where T : new()
{
    var config = new ConfigurationBuilder()
        .AddInMemoryCollection(values.ToDictionary(v => $"{section}:{v.Key}", v => v.Value))
        .Build();
    var options = new T();
    config.GetSection(section).Bind(options);
    return options;
}
```

`BotOptionsTests.cs`：

```csharp
static BotOptions Valid() => new() { GuildId = 1, MemberRoleId = 2, ObRoleId = 3, Token = "token" };
static ValidateOptionsResult Validate(BotOptions o) => new BotOptionsValidator().Validate(null, o);

[Fact] public void Validate_AllSet_Succeeds() => Assert.True(Validate(Valid()).Succeeded);

[Theory]
[InlineData("GuildId")] [InlineData("MemberRoleId")] [InlineData("ObRoleId")]
public void Validate_ZeroId_Fails(string name)
{
    var o = Valid();
    typeof(BotOptions).GetProperty(name)!.SetValue(o, 0UL);
    Assert.Contains($"Bot:{name} が設定されていません", Validate(o).Failures!);
}

[Theory] [InlineData("")] [InlineData("   ")]
public void Validate_BlankToken_Fails(string token)
{
    var o = Valid(); o.Token = token;
    Assert.Contains("Bot__Token が設定されていません（secrets.env を確認）", Validate(o).Failures!);
}

[Fact] public void Validate_SameRoleIds_Fails()
{
    var o = Valid(); o.ObRoleId = o.MemberRoleId;
    Assert.Contains("Bot:MemberRoleId と Bot:ObRoleId に同じIDが設定されています", Validate(o).Failures!);
}

[Fact] public void Validate_ReportsAllFailuresAtOnce()
    => Assert.Equal(4, Validate(new BotOptions()).Failures!.Count());   // ID 3つとトークン

[Fact] public void Token_IsTrimmed() => Assert.Equal("abc", new BotOptions { Token = "  abc \n" }.Token);

[Fact] public void Bind_QuotedNumericId_Binds()
    => Assert.Equal(123UL, Bind<BotOptions>("Bot", ("GuildId", "123")).GuildId);

[Fact] public void Bind_NonNumericId_Throws()
    => Assert.Throws<InvalidOperationException>(() => Bind<BotOptions>("Bot", ("GuildId", "abc")));
```

`Validate_ReportsAllFailuresAtOnce` が示すとおり、IDが両方0のときは「同じID」のエラーを出さない。

`MessageOptionsTests.cs`：

```csharp
[Fact] public void Defaults_MatchSpec()
{
    var m = new MessageOptions();
    Assert.Equal("ようこそ、{user}さん！これからよろしく！", m.JoinSuccess);
    Assert.Equal("あなたは既に部員です。", m.JoinAlreadyMember);
    Assert.Equal("🎓 {user}さんが卒業しOBになりました！これからもよろしく！", m.GraduateSuccess);
    Assert.Equal("部員ロールを持っていないため卒業できません。", m.GraduateNotMember);
    Assert.Equal("処理に失敗しました。時間をおいて再度試すか、管理者に連絡してください。", m.Error);
}

[Fact] public void Bind_MissingKeys_KeepDefaults()
{
    var m = Bind<MessageOptions>("Messages", ("JoinSuccess", "やあ、{user}"));
    Assert.Equal("やあ、{user}", m.JoinSuccess);
    Assert.Equal(new MessageOptions().Error, m.Error);
}

[Theory] [InlineData("")] [InlineData("  ")]
public void Bind_BlankValue_KeepsDefault(string value)
    => Assert.Equal(new MessageOptions().JoinSuccess,
                    Bind<MessageOptions>("Messages", ("JoinSuccess", value)).JoinSuccess);

[Theory]
[InlineData("{user}と{user}", "<@1>と<@1>")]
[InlineData("なし", "なし")]
[InlineData("ようこそ、{user}さん", "ようこそ、<@1>さん")]
public void Format_ReplacesEveryPlaceholder(string template, string expected)
    => Assert.Equal(expected, MessageOptions.Format(template, "<@1>"));
```

- [ ] **Step 2: テストが失敗することを確認する**

Run: `dotnet test`
Expected: コンパイルエラー（`BotOptions` などが存在しない）

- [ ] **Step 3: `src/MpcmanBot/BotOptions.cs` に4つの型を実装する**

Interfacesのシグネチャどおりに書く。`BotOptionsValidator` は、失敗をすべて集めてから `ValidateOptionsResult.Fail(failures)` で返す（設定ミスを1回の起動でまとめて直せるようにするため）。

- [ ] **Step 4: テストが通ることを確認する**

Run: `dotnet test && dotnet format --verify-no-changes`
Expected: すべてPASS

- [ ] **Step 5: コミットする**

```bash
git add -A && git commit -m "feat: 設定のクラスと検証を追加"
```

---

### Task 3: 起動処理とDiscordへの接続

**Files:**
- Modify: `src/MpcmanBot/Program.cs`（テンプレートの中身を置き換える）
- Modify: `src/MpcmanBot/Worker.cs`（テンプレートの中身を置き換える）

**Interfaces:**
- Consumes: Task 2の `BotOptions`、`MessageOptions`、`BotOptionsValidator`、`BotOptions.ConfigErrorExitCode`
- Produces: DIコンテナに次のものが登録されている。Task 4のコマンドはこれらをコンストラクタで受け取る。
  - `IOptions<BotOptions>`、`IOptions<MessageOptions>`
  - シングルトンの `DiscordSocketClient`（`GatewayIntents = GatewayIntents.Guilds`）
  - シングルトンの `InteractionService`
- Produces: `Worker` は、実行中のアセンブリにある `InteractionModuleBase<SocketInteractionContext>` の派生クラスをすべてコマンドとして登録する。Task 4は `Commands/` にクラスを置くだけでよい。

- [ ] **Step 1: `Program.cs` を書く**

`Main` に相当する処理全体を1つの `try` で囲み、終了コードを `return` する。処理の順番：

1. 環境変数 `MPCMAN_CONFIG` が設定されていて、そのファイルがなければ、標準エラー出力に `appsettings.json が見つかりません。appsettings.example.json をコピーしてください（<パス>）` と出して78を返す。
2. `Host.CreateApplicationBuilder(args)` を呼ぶ。`MPCMAN_CONFIG` が設定されていれば `AddJsonFile(path, optional: false, reloadOnChange: false)` で追加し、そのあとにもう一度 `AddEnvironmentVariables()` を呼ぶ（環境変数を常に最優先にするため）。
3. サービスを登録する：`AddSystemd()`、`AddOptions<BotOptions>().BindConfiguration(BotOptions.SectionName).ValidateOnStart()`、`BotOptionsValidator`（`IValidateOptions<BotOptions>` として）、`AddOptions<MessageOptions>().BindConfiguration(MessageOptions.SectionName)`、`DiscordSocketClient`、`InteractionService`、`AddHostedService<Worker>()`。
4. `await host.RunAsync()` のあと、`Environment.ExitCode` を返す（`Worker` が設定した値を使うため）。

`catch`：
- `OptionsValidationException`：`Failures` を1行ずつ標準エラー出力に出して78を返す。
- それ以外の例外：`設定の読み込みに失敗しました: <ex.Message>` を標準エラー出力に出して78を返す。ここに届くのはJSONの書き間違いやIDの型違いで、再起動しても直らないため。`Worker` の中の失敗は `Worker` 自身が処理するので、ここには届かない。

- [ ] **Step 2: `Worker.cs` を書く**

```csharp
public sealed class Worker(
    DiscordSocketClient client,
    InteractionService interactions,
    IServiceProvider services,
    IOptions<BotOptions> options,
    IHostApplicationLifetime lifetime,
    ILogger<Worker> logger) : BackgroundService
```

`ExecuteAsync` の流れ：
1. `client.Log` と `interactions.Log` を `ILogger` に流す（`LogSeverity` の Critical / Error / Warning / Info は同じ名前のレベルに、Verbose / Debug は Debug に対応させる）。
2. `client.Disconnected` と、2回目以降の `client.Connected` を Warning で記録する。
3. `interactions.AddModulesAsync(Assembly.GetExecutingAssembly(), services)` を呼ぶ。
4. `client.InteractionCreated` で `interactions.ExecuteCommandAsync(new SocketInteractionContext(client, interaction), services)` を呼ぶ。
5. `client.Ready` で下の「接続後の検証」を行う。
6. `client.LoginAsync(TokenType.Bot, options.Value.Token)` → `client.StartAsync()` → `stoppingToken` がキャンセルされるまで待つ → `client.StopAsync()`。

失敗の扱い（privateメソッド `FailConfiguration(string message)` を作る。中身は `LogCritical` → `Environment.ExitCode = BotOptions.ConfigErrorExitCode` → `lifetime.StopApplication()`）：
- `LoginAsync` が `ArgumentException`（トークンの形式が不正。Discord.Netが通信の前に検査する）、またはHTTP 401の `Discord.Net.HttpException` を投げたら、`FailConfiguration("ログインに失敗しました。トークンを確認してください")`。
- それ以外の例外（停止時の `OperationCanceledException` は除く）は、`LogError` → `Environment.ExitCode = 1` → `lifetime.StopApplication()`。ネットワーク障害などはsystemdに再起動させるため、78にはしない。Readyハンドラの中の例外も同じ扱いにする（Discord.Netはイベントハンドラの例外を握りつぶすので、ハンドラ内で `try` / `catch` する）。

接続後の検証（Readyハンドラ。上から順に確認し、最初に当てはまったもので `FailConfiguration` を呼んで戻る。文言は仕様書4.3の表と同じ）：

| 条件 | メッセージ |
|---|---|
| `client.GetGuild(GuildId)` が null | `GuildId のサーバーが見つかりません。botを招待済みか確認してください` |
| `guild.GetRole(MemberRoleId)` が null | `MemberRoleId のロールがサーバーに存在しません` |
| `guild.GetRole(ObRoleId)` が null | `ObRoleId のロールがサーバーに存在しません` |
| `guild.CurrentUser.GuildPermissions.ManageRoles` が false | `botに「ロールの管理」権限がありません` |
| `guild.CurrentUser.Hierarchy` がどちらかのロールの `Position` 以下 | `botのロールを「部員」「OB」より上に移動してください` |

すべて通ったら `interactions.RegisterCommandsToGuildAsync(GuildId)` を呼び、Information で起動完了を記録する。Readyは再接続のたびに呼ばれるが、毎回同じ処理をしてよい。

- [ ] **Step 3: ビルドとテストを確認する**

Run: `dotnet build && dotnet test && dotnet format --verify-no-changes`
Expected: すべて成功

- [ ] **Step 4: 設定エラーの終了コードを確認する（Discordへの接続は不要）**

`src/MpcmanBot/appsettings.json` がない状態で、スクラッチ用のディレクトリに次の3つのファイルを作る。
- `broken.json`：`{ "Bot": { "GuildId": 1`
- `badid.json`：`{ "Bot": { "GuildId": "abc", "MemberRoleId": 2, "ObRoleId": 3 } }`
- `ok.json`：`{ "Bot": { "GuildId": 1, "MemberRoleId": 2, "ObRoleId": 3 } }`

`cd src/MpcmanBot` して、それぞれ `dotnet run --no-launch-profile; echo "exit=$?"` で実行する（`--no-launch-profile` は、開発モードのUser Secretsを読ませないため）。

| 実行のしかた | 期待する出力 |
|---|---|
| 環境変数なし | `Bot:GuildId が設定されていません` ほか計4行、`exit=78` |
| `MPCMAN_CONFIG=/nonexistent/appsettings.json` | `appsettings.json が見つかりません…`、`exit=78` |
| `MPCMAN_CONFIG=<broken.json>` | `設定の読み込みに失敗しました: …`、`exit=78` |
| `MPCMAN_CONFIG=<badid.json> Bot__Token=x` | `設定の読み込みに失敗しました: …`、`exit=78` |
| `MPCMAN_CONFIG=<ok.json>`（トークンなし） | `Bot__Token が設定されていません（secrets.env を確認）`、`exit=78` |
| `MPCMAN_CONFIG=<ok.json> Bot__Token=invalid` | `ログインに失敗しました。トークンを確認してください`、`exit=78` |

どの場合も、出力にスタックトレースが出ないこと。

- [ ] **Step 5: 【人間】テスト用サーバーにつないで確認する**

`appsettings.example.json` を `appsettings.json` にコピーしてテスト用サーバーのIDを書き、`dotnet user-secrets set "Bot:Token" "<トークン>"` を実行してから `dotnet run` する。
- botがオンラインになり、ログに起動完了が出る。
- `ObRoleId` を存在しないIDにすると、`ObRoleId のロールがサーバーに存在しません` と出て終了する。
- botのロールを部員ロールより下に動かすと、`botのロールを「部員」「OB」より上に移動してください` と出て終了する。
- Ctrl+Cで、エラーを出さずに終了する。

- [ ] **Step 6: コミットする**

```bash
git add -A && git commit -m "feat: 起動処理、Discordへの接続、起動時の検証を追加"
```

---

### Task 4: `/join` と `/graduate`

**Files:**
- Create: `src/MpcmanBot/Commands/JoinCommand.cs`
- Create: `src/MpcmanBot/Commands/GraduateCommand.cs`

**Interfaces:**
- Consumes: Task 3でDIに登録された `IOptions<BotOptions>`、`IOptions<MessageOptions>`。Task 2の `MessageOptions.Format(template, mention)`。
- Produces:

```csharp
public sealed class JoinCommand(IOptions<BotOptions> bot, IOptions<MessageOptions> messages, ILogger<JoinCommand> logger)
    : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("join", "部員ロールを受け取ります")]
    public async Task JoinAsync();
}

public sealed class GraduateCommand(IOptions<BotOptions> bot, IOptions<MessageOptions> messages, ILogger<GraduateCommand> logger)
    : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("graduate", "卒業してOBロールに切り替えます")]
    public async Task GraduateAsync();
}
```

仕様書7.1のとおり、コマンドの自動テストは書かない。Step 3の手動確認で確かめる。

- [ ] **Step 1: 2つのコマンドを実装する**

どちらも「今のロールを取得 → 判定 → ロールを変更 → 返信」の同じ流れで、上から順に読める形に書く。

| コマンド | 弾く条件 | 弾いたときの返信 | ロールの変更 | 成功したときの返信 |
|---|---|---|---|---|
| `/join` | 部員ロールを持っている | `JoinAlreadyMember` | 部員を追加し、OBを外す | `JoinSuccess` |
| `/graduate` | 部員ロールを持っていない | `GraduateNotMember` | 部員を外し、OBを追加する | `GraduateSuccess` |

- 実行者は `(SocketGuildUser)Context.User` で取得する。
- ロールの変更は、1回の `user.ModifyAsync(p => p.RoleIds = ...)` で行う。渡すIDの一覧は、今のロールから `@everyone`（`IsEveryone`）を除き、上の表の追加と削除を反映したもの。
- 成功したときの返信は、`RespondAsync(MessageOptions.Format(文言, user.Mention), allowedMentions: new AllowedMentions { UserIds = [user.Id] })`。
- 弾いたときと失敗したときの返信は、`ephemeral: true` と `allowedMentions: AllowedMentions.None` を付ける。
- 処理全体を `try` で囲む。例外が起きたら `LogError` で記録し、`Error` の文言を本人にだけ返す。すでに返信済み（`Context.Interaction.HasResponded`）なら `FollowupAsync` を使う。その返信も失敗した場合（3秒の期限切れなど）は、記録するだけにする。
- 結果は Information で記録する：コマンド名、ユーザーID、結果（`success` / `already_member` / `not_member` / `error`）。

- [ ] **Step 2: ビルドとテストを確認する**

Run: `dotnet build && dotnet test && dotnet format --verify-no-changes`
Expected: すべて成功

- [ ] **Step 3: 【人間】テスト用サーバーで確認する**

仕様書7.2のチェックリストの上から6項目を確認する。あわせて、次の3つも確認する。
- [ ] 部員とOB以外のロールを持つ人が `/join` と `/graduate` を実行しても、ほかのロールが変わらない
- [ ] botより上位のロールを持つ人が実行しても、成功してほかのロールが変わらない
- [ ] ログに、コマンド名、ユーザーID、結果が出ている

上の2つ目が失敗する場合（ロール一覧の一括書き換えをDiscordが拒否する場合）は、ロールの変更を `AddRoleAsync` → `RemoveRoleAsync` の順に実行する方法へ切り替える。切り替えたことと理由を、仕様書5.1に追記する。

- [ ] **Step 4: コミットする**

```bash
git add -A && git commit -m "feat: /join と /graduate を追加"
```

---

### Task 5: systemdの設定とドキュメント

**Files:**
- Create: `deploy/mpcman.service`
- Create: `README.md`、`docs/operations.md`、`docs/development.md`

**Interfaces:**
- Consumes: Task 1の発行コマンドと出力先、Task 3のエラーメッセージと終了コード。文書に書くコマンド、パス、メッセージは、実装と一字一句同じにする。

- [ ] **Step 1: `deploy/mpcman.service` を作る**

仕様書8.2の内容をそのまま書く。

- [ ] **Step 2: `docs/operations.md` を書く（運用担当者向け）**

経験の浅い部員が上から順に実行すれば終わる形にする。必要な見出し：

1. **初回導入**：Discord Developer Portalでのbotの作成、トークンの取得、招待URL（スコープ `bot` と `applications.commands`、権限「ロールの管理」「メッセージの送信」）、botのロールを「部員」「OB」より上に置くこと、開発者モードでのIDの調べ方、サーバーへの設置（仕様書8.1の配置。実行ファイルは755、`secrets.env` は所有者rootの600）、`systemctl enable --now mpcman`
2. **日常の操作**：仕様書8.3の表
3. **設定の変更**：文言とIDの変え方、再起動での反映
4. **更新**：リリースページから入手する方法と、Actionsがない間の方法（手元のPCで `dotnet publish src/MpcmanBot -c Release -p:PublishProfile=linux-x64` を実行し、`artifacts/linux-x64/MpcmanBot` を `scp` でコピーする）
5. **トラブル対応表**：仕様書4.3のメッセージとTask 3の2つのメッセージ（`設定の読み込みに失敗しました`、`ログインに失敗しました`）ごとに、原因と直し方を書く。「botがオフラインのときは、まず `journalctl -u mpcman -n 50` を見る」ことも書く
6. **誤って卒業した人を戻す**：本人が `/join` を実行する
7. **トークンが漏れたとき**：Developer Portalでリセット → `secrets.env` を書き換える → 再起動
8. **リリース前のチェックリスト**：仕様書7.2の8項目と、Task 4 Step 3で追加した項目

- [ ] **Step 3: `docs/development.md` と `README.md` を書く**

`docs/development.md`（開発担当者向け）：
- 開発環境（.NET 10 SDK。Windows、macOS、Linuxのどれでもよい）
- テスト用サーバーとテスト用botの用意、`appsettings.example.json` を `appsettings.json` にコピーすること、`dotnet user-secrets set "Bot:Token" "<トークン>"`
- よく使うコマンド：`dotnet run`、`dotnet test`、`dotnet format`、発行コマンド
- 仕様書9章の「どこを直せば何が変わるか」の早見表
- GitHub Actionsはまだ設定していないこと。満たしてほしい要件は仕様書7.3にあること

`README.md`：botの概要（できること2つ、メッセージ本文を読まないこと）と、上の2つの文書および仕様書へのリンク。

- [ ] **Step 4: 文書と実装が合っているか確認する**

文書に書いたすべてのコマンド、パス、エラーメッセージを、実装（`Program.cs`、`Worker.cs`、`BotOptions.cs`、`linux-x64.pubxml`、`mpcman.service`）と突き合わせる。エラーメッセージは `grep` で実装側に同じ文字列があることを確かめる。

Run: `dotnet build && dotnet test && dotnet format --verify-no-changes`
Expected: すべて成功

- [ ] **Step 5: 【人間】Debianで確認する**

`docs/operations.md` の「初回導入」を、書いてあるとおりにx86-64のDebianで実行する。
- [ ] `systemctl status mpcman` が `active (running)` になり、Discordでbotがオンラインになる（`DynamicUser=yes` と `ProtectSystem=strict` の下で、単一ファイルの実行ファイルが起動できることの確認を兼ねる）
- [ ] `secrets.env` のトークンを空にして再起動すると、終了コード78で止まり、再起動を繰り返さない（`systemctl status` で確認する）
- [ ] マシンを再起動すると、botが自動で立ち上がる
- [ ] 手順書のとおりに進めて、詰まるところがなかった

起動できない場合は、`journalctl -u mpcman` の内容をもとにsystemdの制限を見直し、変えた内容と理由を仕様書8.2に追記する。

- [ ] **Step 6: コミットする**

```bash
git add -A && git commit -m "docs: systemdの設定と運用・開発の手順書を追加"
```
