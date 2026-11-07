using Discord.Interactions;
using Discord.WebSocket;
using mpcman_bot_v3.Bot;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOptions<BotOptions>()
    .BindConfiguration(BotOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton(_ => new DiscordSocketClient(DiscordClientConfig.Create()));
builder.Services.AddSingleton(sp => new InteractionService(sp.GetRequiredService<DiscordSocketClient>()));
builder.Services.AddHostedService<DiscordBotService>();

var host = builder.Build();
host.Run();
