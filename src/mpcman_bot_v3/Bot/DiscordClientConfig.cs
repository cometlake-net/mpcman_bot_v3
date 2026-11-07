using Discord;
using Discord.WebSocket;

namespace mpcman_bot_v3.Bot;

public static class DiscordClientConfig
{
    public static DiscordSocketConfig Create() => new()
    {
        // スラッシュコマンドだけを使うので Guilds 以外の Intent（特に特権 Intent）は不要。
        GatewayIntents = GatewayIntents.Guilds,

        // Discord.Net は返信前に「受信から3秒以内か」を自前で確かめる。既定では Discord 側の時刻（snowflake）と
        // ローカル時計を比べるため、ホスト（WSL2 / Docker など）の時計が進んでいると、ロールを変えた後の返信が失敗する。
        // 受信したときのローカル時刻を基準にして、時計のずれの影響を受けないようにする。
        UseInteractionSnowflakeDate = false,
    };
}
