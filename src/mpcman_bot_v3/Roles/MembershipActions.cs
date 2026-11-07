namespace mpcman_bot_v3.Roles;

public enum JoinResult { Granted, AlreadyMember }

public enum GraduateResult { Graduated, NotMember }

public static class MembershipActions
{
    /// <summary>部員ロールを持っていなければ付与する。他のロール（OB など）は判定に使わず、変更もしない。</summary>
    public static async Task<JoinResult> JoinAsync(IMemberRoleEditor member, ulong memberRoleId)
    {
        if (member.RoleIds.Contains(memberRoleId))
            return JoinResult.AlreadyMember;

        await member.AddRoleAsync(memberRoleId);
        return JoinResult.Granted;
    }

    /// <summary>
    /// 部員ロールを外して OB ロールを付与する。
    /// OB を先に付けてから部員を外すので、途中で失敗しても「どちらのロールもない」状態にはならない。
    /// </summary>
    public static async Task<GraduateResult> GraduateAsync(IMemberRoleEditor member, ulong memberRoleId, ulong alumniRoleId)
    {
        if (!member.RoleIds.Contains(memberRoleId))
            return GraduateResult.NotMember;

        if (!member.RoleIds.Contains(alumniRoleId))
            await member.AddRoleAsync(alumniRoleId);

        await member.RemoveRoleAsync(memberRoleId);
        return GraduateResult.Graduated;
    }
}
