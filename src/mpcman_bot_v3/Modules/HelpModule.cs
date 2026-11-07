using Discord;
using Discord.Interactions;
using mpcman_bot_v3.Help;

namespace mpcman_bot_v3.Modules;

[CommandContextType(InteractionContextType.Guild)]
public sealed class HelpModule(InteractionService interactions) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("help", "コマンド一覧を表示します")]
    public Task HelpAsync()
        => RespondAsync(HelpText.Build(interactions.SlashCommands.Select(c => (c.Name, c.Description))), ephemeral: true);
}
