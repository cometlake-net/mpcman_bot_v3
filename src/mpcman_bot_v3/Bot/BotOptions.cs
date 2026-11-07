using System.ComponentModel.DataAnnotations;

namespace mpcman_bot_v3.Bot;

/// <summary>設定セクション "Bot" の内容。</summary>
public sealed class BotOptions
{
    public const string SectionName = "Bot";

    /// <summary>Bot トークン。リポジトリには置かず、ユーザーシークレットか環境変数 Bot__Token で渡す。</summary>
    [Required(ErrorMessage = "Bot:Token が設定されていません。")]
    public string Token { get; set; } = "";

    /// <summary>コマンドを登録するサーバー（ギルド）の ID。</summary>
    [Range(typeof(ulong), "1", "18446744073709551615", ErrorMessage = "Bot:GuildId が設定されていません。")]
    public ulong GuildId { get; set; }

    public string MemberRoleName { get; set; } = "部員";

    public string AlumniRoleName { get; set; } = "OB";
}
