using mpcman_bot_v3.Roles;

namespace mpcman_bot_v3.Tests;

public class MembershipActionsTests
{
    const ulong Member = 10;
    const ulong Ob = 20;
    const ulong Booster = 30;

    sealed class FakeMember(params ulong[] roles) : IMemberRoleEditor
    {
        readonly HashSet<ulong> _roles = [.. roles];
        public List<string> Calls { get; } = [];
        public bool ThrowOnAdd { get; init; }
        public bool ThrowOnRemove { get; init; }

        public IReadOnlyCollection<ulong> RoleIds => _roles;

        public Task AddRoleAsync(ulong roleId)
        {
            Calls.Add($"add:{roleId}");
            if (ThrowOnAdd) throw new InvalidOperationException("add failed");
            _roles.Add(roleId);
            return Task.CompletedTask;
        }

        public Task RemoveRoleAsync(ulong roleId)
        {
            Calls.Add($"remove:{roleId}");
            if (ThrowOnRemove) throw new InvalidOperationException("remove failed");
            _roles.Remove(roleId);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Join_GrantsMember_WhenNoRoles()
    {
        var m = new FakeMember();
        Assert.Equal(JoinResult.Granted, await MembershipActions.JoinAsync(m, Member));
        Assert.Equal(["add:10"], m.Calls);
    }

    [Fact]
    public async Task Join_GrantsMember_WhenHasOtherRoles()
    {
        var m = new FakeMember(Booster);
        Assert.Equal(JoinResult.Granted, await MembershipActions.JoinAsync(m, Member));
        Assert.Equal(["add:10"], m.Calls);
    }

    [Fact]
    public async Task Join_GrantsMember_AndKeepsOb_WhenOb()
    {
        var m = new FakeMember(Ob);
        Assert.Equal(JoinResult.Granted, await MembershipActions.JoinAsync(m, Member));
        Assert.Equal(["add:10"], m.Calls);
        Assert.Superset(new HashSet<ulong> { Member, Ob }, new HashSet<ulong>(m.RoleIds));
    }

    [Fact]
    public async Task Join_DoesNothing_WhenAlreadyMember()
    {
        var m = new FakeMember(Member);
        Assert.Equal(JoinResult.AlreadyMember, await MembershipActions.JoinAsync(m, Member));
        Assert.Empty(m.Calls);
    }

    [Fact]
    public async Task Graduate_SwapsMemberForOb()
    {
        var m = new FakeMember(Member);
        Assert.Equal(GraduateResult.Graduated, await MembershipActions.GraduateAsync(m, Member, Ob));
        Assert.Equal(["add:20", "remove:10"], m.Calls);
    }

    [Fact]
    public async Task Graduate_OnlyRemovesMember_WhenAlreadyOb()
    {
        var m = new FakeMember(Member, Ob);
        Assert.Equal(GraduateResult.Graduated, await MembershipActions.GraduateAsync(m, Member, Ob));
        Assert.Equal(["remove:10"], m.Calls);
    }

    [Fact]
    public async Task Graduate_DoesNothing_WhenNotMember()
    {
        var m = new FakeMember(Ob);
        Assert.Equal(GraduateResult.NotMember, await MembershipActions.GraduateAsync(m, Member, Ob));
        Assert.Empty(m.Calls);
    }

    [Fact]
    public async Task Graduate_KeepsBothRoles_WhenRemoveFails()
    {
        var m = new FakeMember(Member) { ThrowOnRemove = true };
        await Assert.ThrowsAsync<InvalidOperationException>(() => MembershipActions.GraduateAsync(m, Member, Ob));
        Assert.Equal(new HashSet<ulong> { Member, Ob }, new HashSet<ulong>(m.RoleIds));
    }

    [Fact]
    public async Task Graduate_DoesNotRemove_WhenAddFails()
    {
        var m = new FakeMember(Member) { ThrowOnAdd = true };
        await Assert.ThrowsAsync<InvalidOperationException>(() => MembershipActions.GraduateAsync(m, Member, Ob));
        Assert.DoesNotContain("remove:10", m.Calls);
        Assert.Equal(new HashSet<ulong> { Member }, new HashSet<ulong>(m.RoleIds));
    }
}
