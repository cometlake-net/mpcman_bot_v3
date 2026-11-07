using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Options;
using mpcman_bot_v3.Roles;

namespace mpcman_bot_v3.Bot;

/// <summary>Discord に接続し、スラッシュコマンドを設定されたサーバーに登録・実行する。</summary>
public sealed class DiscordBotService(
    DiscordSocketClient client,
    InteractionService interactions,
    IServiceProvider services,
    IOptions<BotOptions> options,
    ILogger<DiscordBotService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;

        client.Log += LogAsync;
        interactions.Log += LogAsync;
        client.Ready += () => OnReadyAsync(opts.GuildId);
        client.InteractionCreated += OnInteractionCreatedAsync;
        interactions.InteractionExecuted += OnInteractionExecutedAsync;

        // Ready は再接続のたびに発火するため、モジュールの読み込みはここで1回だけ行う。
        await interactions.AddModulesAsync(typeof(DiscordBotService).Assembly, services);

        await client.LoginAsync(TokenType.Bot, opts.Token);
        await client.StartAsync();

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // 終了要求。下で切断する。
        }

        await client.StopAsync();
        await client.LogoutAsync();
    }

    private async Task OnReadyAsync(ulong guildId)
    {
        if (client.GetGuild(guildId) is null)
        {
            logger.LogError(
                "設定されたサーバー（ID: {GuildId}）が見つかりません。Bot:GuildId が正しいか、Bot がそのサーバーに参加しているか確認してください。",
                guildId);
            return;
        }

        try
        {
            var commands = await interactions.RegisterCommandsToGuildAsync(guildId, true);
            logger.LogInformation("サーバー {GuildId} に {Count} 個のコマンドを登録しました。", guildId, commands.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "サーバー {GuildId} へのコマンド登録に失敗しました。", guildId);
        }
    }

    private async Task OnInteractionCreatedAsync(SocketInteraction interaction)
    {
        var context = new SocketInteractionContext(client, interaction);
        await interactions.ExecuteCommandAsync(context, services);
    }

    private async Task OnInteractionExecutedAsync(ICommandInfo? command, IInteractionContext context, IResult result)
    {
        if (result.IsSuccess)
            return;

        var exception = (result as ExecuteResult?)?.Exception;
        logger.LogError(exception, "コマンド {Command} の実行に失敗しました: {Error} {Reason}",
            command?.Name, result.Error, result.ErrorReason);

        if (context.Interaction.HasResponded)
            return;

        try
        {
            await context.Interaction.RespondAsync(Messages.ConfigError, ephemeral: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "エラーの返信に失敗しました。");
        }
    }

    private Task LogAsync(LogMessage message)
    {
        var level = message.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Debug,
            _ => LogLevel.Trace,
        };
        logger.Log(level, message.Exception, "[{Source}] {Message}", message.Source, message.Message);
        return Task.CompletedTask;
    }
}
