using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using mpcman_bot_v3.Bot;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOptions<BotOptions>()
    .BindConfiguration(BotOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

// スラッシュコマンドだけを使うので Guilds 以外の Intent（特に特権 Intent）は不要。
builder.Services.AddSingleton(_ => new DiscordSocketClient(new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.Guilds,
}));
builder.Services.AddSingleton(sp => new InteractionService(sp.GetRequiredService<DiscordSocketClient>()));
builder.Services.AddHostedService<DiscordBotService>();

var host = builder.Build();
host.Run();
