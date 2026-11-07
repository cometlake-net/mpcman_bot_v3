using mpcman_bot_v3.Roles;

namespace mpcman_bot_v3.Tests;

public class RoleFinderTests
{
    static readonly (ulong, string)[] Roles = [(1, "@everyone"), (10, "部員"), (20, "OB")];

    [Fact]
    public void Find_ReturnsId_WhenNameMatches()
        => Assert.Equal(new RoleMatch(10, false), RoleFinder.Find(Roles, "部員"));

    [Fact]
    public void Find_ReturnsNull_WhenNameMissing()
        => Assert.Null(RoleFinder.Find(Roles, "幹部"));

    [Fact]
    public void Find_IsExactMatch()
        => Assert.Null(RoleFinder.Find(Roles, "ob"));

    [Fact]
    public void Find_ReturnsFirstAndFlagsDuplicates()
        => Assert.Equal(new RoleMatch(10, true), RoleFinder.Find([(10, "部員"), (11, "部員")], "部員"));
}
