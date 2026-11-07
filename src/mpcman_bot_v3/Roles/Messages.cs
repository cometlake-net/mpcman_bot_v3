namespace mpcman_bot_v3.Roles;

/// <summary>コマンドの返信文言（設計書のとおり）。</summary>
public static class Messages
{
    public static string JoinGranted(string mention) => $"{mention}へサーバーの利用権限を付与しました。これからよろしく！";

    public const string AlreadyMember = "あなたは既に利用権限を持っています！";

    public static string Graduated(string mention) => $"{mention}は卒業しOBとなりました。これからもよろしく！";

    public const string NotMember = "そのコマンドを使用する権限がありません！";

    public const string ConfigError = "Botの設定に問題があり処理できませんでした。管理者に連絡してください。";
}
