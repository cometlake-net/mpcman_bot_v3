using Discord;
using mpcman_bot_v3.Bot;

namespace mpcman_bot_v3.Tests;

public class DiscordClientConfigTests
{
    [Fact]
    public void Create_UsesOnlyGuildsIntent()
        => Assert.Equal(GatewayIntents.Guilds, DiscordClientConfig.Create().GatewayIntents);

    // 3秒の応答期限チェックを Discord 側の時刻（snowflake）ではなく受信時刻で行い、
    // ホストの時計がずれていても返信が TimeoutException にならないようにする。
    [Fact]
    public void Create_MeasuresResponseDeadlineFromLocalReceiptTime()
        => Assert.False(DiscordClientConfig.Create().UseInteractionSnowflakeDate);
}
