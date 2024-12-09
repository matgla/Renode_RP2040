using Antmicro.Renode.Testing;

namespace Antmicro.Renode.RobotFramework
{
class DisplayTesterKeywords : TestersProvider<TerminalTester, ISegmentDisplay>, IRobotFrameworkKeywordProvider
{
    public void Dispose()
    {

    }
    [RobotFrameworkKeyword]
    public void HelloKeyword()
    {
    }

};

}
