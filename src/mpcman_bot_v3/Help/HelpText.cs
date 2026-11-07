namespace mpcman_bot_v3.Help;

public static class HelpText
{
    /// <summary>コマンドの名前と説明から、1行1コマンドの一覧（<c>/name — description</c>）を作る。</summary>
    public static string Build(IEnumerable<(string Name, string Description)> commands)
        => string.Join("\n", commands.Select(c => $"/{c.Name} — {c.Description}"));
}
