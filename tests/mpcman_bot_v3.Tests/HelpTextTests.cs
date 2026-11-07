using mpcman_bot_v3.Help;

namespace mpcman_bot_v3.Tests;

public class HelpTextTests
{
    [Fact]
    public void Build_ListsCommandsInOrder()
        => Assert.Equal(
            "/join — 部員のロールが付与されます\n/graduate — 部員からOBになります\n/help — コマンド一覧を表示します",
            HelpText.Build([("join", "部員のロールが付与されます"), ("graduate", "部員からOBになります"), ("help", "コマンド一覧を表示します")]));
}
