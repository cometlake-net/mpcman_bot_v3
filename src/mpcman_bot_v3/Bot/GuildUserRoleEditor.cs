using Discord;
using mpcman_bot_v3.Roles;

namespace mpcman_bot_v3.Bot;

/// <summary>Discord のサーバーメンバーを <see cref="IMemberRoleEditor"/> として扱うアダプタ。</summary>
public sealed class GuildUserRoleEditor(IGuildUser user) : IMemberRoleEditor
{
    public IReadOnlyCollection<ulong> RoleIds => user.RoleIds;

    public Task AddRoleAsync(ulong roleId) => user.AddRoleAsync(roleId);

    public Task RemoveRoleAsync(ulong roleId) => user.RemoveRoleAsync(roleId);
}
