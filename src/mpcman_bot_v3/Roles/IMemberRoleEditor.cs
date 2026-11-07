namespace mpcman_bot_v3.Roles;

/// <summary>サーバーメンバー1人のロールを参照・変更する操作。Discord に依存しない形でテストするための抽象。</summary>
public interface IMemberRoleEditor
{
    IReadOnlyCollection<ulong> RoleIds { get; }
    Task AddRoleAsync(ulong roleId);
    Task RemoveRoleAsync(ulong roleId);
}
