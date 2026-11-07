namespace mpcman_bot_v3.Roles;

/// <summary>名前で見つかったロール。同名のロールが複数あれば <see cref="HasDuplicates"/> が true。</summary>
public sealed record RoleMatch(ulong Id, bool HasDuplicates);

public static class RoleFinder
{
    /// <summary>
    /// ロール名に完全一致（大文字小文字を区別）するロールを探す。
    /// 見つからなければ null。複数あれば列挙順で最初のものを返す。
    /// </summary>
    public static RoleMatch? Find(IEnumerable<(ulong Id, string Name)> roles, string name)
    {
        var matches = roles.Where(r => string.Equals(r.Name, name, StringComparison.Ordinal)).Take(2).ToList();
        return matches.Count == 0 ? null : new RoleMatch(matches[0].Id, matches.Count > 1);
    }
}
