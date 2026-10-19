using FluentAssertions;
using Pannella.TUI;

namespace Pannella.Tests.Unit;

[Collection("SessionLog")]
public class SessionLogTests : IDisposable
{
    public SessionLogTests() => SessionLog.ResetForTests();

    public void Dispose() => SessionLog.ResetForTests();

    [Fact]
    public void Record_KeepsEveryLine_InOrder()
    {
        SessionLog.Record("one");
        SessionLog.Record(null);
        SessionLog.Record("two");

        SessionLog.Snapshot().Should().Equal("one", "two");
    }

    [Fact]
    public void RecordRaw_FoldsCarriageReturnProgress_AndSplitsOnNewline()
    {
        SessionLog.RecordRaw("Downloading\n");
        SessionLog.RecordRaw("[==   ] 40%\r[==== ] 80%\r[=====] 100%\n");
        SessionLog.RecordRaw("tail without newline");

        SessionLog.Snapshot().Should().Equal("Downloading", "[=====] 100%", "tail without newline");
    }

    [Fact]
    public void Record_AfterPartialRaw_FlushesThePartialFirst()
    {
        SessionLog.RecordRaw("partial");
        SessionLog.Record("status");

        SessionLog.Snapshot().Should().Equal("partial", "status");
    }

    [Fact]
    public void Export_WritesHeaderAndLines()
    {
        string dir = Path.Combine(Path.GetTempPath(), "pupdate-log-" + Guid.NewGuid().ToString("N"));
        SessionLog.Record("hello");

        try
        {
            string path = SessionLog.Export(dir, "9.9.9", "/sd");
            string text = File.ReadAllText(path);

            Path.GetFileName(path).Should().MatchRegex(@"^pupdate_log_\d{8}_\d{6}\.txt$");
            text.Should().StartWith("pupdate 9.9.9 - session log");
            text.Should().Contain("Install:   /sd");
            text.Should().EndWith("hello" + Environment.NewLine);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
