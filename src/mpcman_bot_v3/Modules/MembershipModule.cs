using Discord;
using Discord.Interactions;
using Discord.Net;
using Microsoft.Extensions.Options;
using mpcman_bot_v3.Bot;
using mpcman_bot_v3.Roles;

namespace mpcman_bot_v3.Modules;

[CommandContextType(InteractionContextType.Guild)]
public sealed class MembershipModule(IOptions<BotOptions> options, ILogger<MembershipModule> logger)
    : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("join", "部員のロールが付与されます")]
    public async Task JoinAsync()
    {
        if (FindRole(options.Value.MemberRoleName) is not { } memberRoleId)
        {
            await RespondAsync(Messages.ConfigError, ephemeral: true);
            return;
        }

        JoinResult result;
        try
        {
            result = await MembershipActions.JoinAsync(CurrentMember(), memberRoleId);
        }
        catch (HttpException ex)
        {
            LogRoleUpdateFailure(ex, "join");
            await RespondAsync(Messages.ConfigError, ephemeral: true);
            return;
        }

        if (result == JoinResult.Granted)
            await RespondAsync(Messages.JoinGranted(Context.User.Mention));
        else
            await RespondAsync(Messages.AlreadyMember, ephemeral: true);
    }

    [SlashCommand("graduate", "部員からOBになります")]
    public async Task GraduateAsync()
    {
        if (FindRole(options.Value.MemberRoleName) is not { } memberRoleId
            || FindRole(options.Value.AlumniRoleName) is not { } alumniRoleId)
        {
            await RespondAsync(Messages.ConfigError, ephemeral: true);
            return;
        }

        GraduateResult result;
        try
        {
            result = await MembershipActions.GraduateAsync(CurrentMember(), memberRoleId, alumniRoleId);
        }
        catch (HttpException ex)
        {
            LogRoleUpdateFailure(ex, "graduate");
            await RespondAsync(Messages.ConfigError, ephemeral: true);
            return;
        }

        if (result == GraduateResult.Graduated)
            await RespondAsync(Messages.Graduated(Context.User.Mention));
        else
            await RespondAsync(Messages.NotMember, ephemeral: true);
    }

    private GuildUserRoleEditor CurrentMember() => new((IGuildUser)Context.User);

    private ulong? FindRole(string name)
    {
        var match = RoleFinder.Find(Context.Guild.Roles.Select(r => (r.Id, r.Name)), name);
        if (match is null)
        {
            logger.LogError("ロール「{RoleName}」がサーバーに見つかりません。設定（Bot:MemberRoleName / Bot:AlumniRoleName）とサーバーのロール名を確認してください。", name);
            return null;
        }

        if (match.HasDuplicates)
            logger.LogWarning("ロール「{RoleName}」が複数あります。ID {RoleId} のロールを使います。", name, match.Id);

        return match.Id;
    }

    private void LogRoleUpdateFailure(HttpException ex, string command)
    {
        const string hint = "Bot に「ロールの管理」権限があるか、Bot のロールが「部員」「OB」より上に並んでいるか確認してください。";
        logger.LogError(ex, "/{Command} でロールの変更に失敗しました（{Code}: {Reason}）。{Hint}",
            command, ex.DiscordCode, ex.Reason, ex.DiscordCode == DiscordErrorCode.MissingPermissions ? hint : "");
    }
}
