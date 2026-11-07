using mpcman_bot_v3.Roles;

namespace mpcman_bot_v3.Tests;

public class MessagesTests
{
    [Fact]
    public void JoinGranted_MatchesSpecCopy()
        => Assert.Equal("<@1>へサーバーの利用権限を付与しました。これからよろしく！", Messages.JoinGranted("<@1>"));

    [Fact]
    public void Graduated_MatchesSpecCopy()
        => Assert.Equal("<@1>は卒業しOBとなりました。これからもよろしく！", Messages.Graduated("<@1>"));
}
