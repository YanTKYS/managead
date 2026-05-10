using Xunit;
using ManageAdTool.Services;

namespace ManageAdTool.Tests;

public class LogReaderTests
{
    [Fact]
    public void ReadLog_ReturnsEmptyForMissingFile()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"managead-missing-{Guid.NewGuid():N}.jsonl");

        var entries = LogReader.ReadLog(missing, maxRows: 10);

        Assert.Empty(entries);
    }

    [Fact]
    public void ReadLog_KeepsBrokenJsonLinesAsParseErrors()
    {
        var path = WriteLogLines(
            "{\"action\":\"ok\",\"target\":\"sato.taro\"}",
            "{broken json",
            "{\"action\":\"done\",\"target\":\"tanaka.hana\"}");

        var entries = LogReader.ReadLog(path, maxRows: 10);

        Assert.Equal(3, entries.Count);
        Assert.False(entries[0].IsParseError);
        Assert.True(entries[1].IsParseError);
        Assert.Equal("(解析エラー)", entries[1].Action);
        Assert.False(entries[2].IsParseError);
    }

    [Fact]
    public void ReadLog_ReturnsOnlyLastMaxRowsInFileOrder()
    {
        var path = WriteLogLines(
            "{\"action\":\"one\"}",
            "{\"action\":\"two\"}",
            "{\"action\":\"three\"}",
            "{\"action\":\"four\"}");

        var entries = LogReader.ReadLog(path, maxRows: 2);

        Assert.Collection(entries,
            entry => Assert.Equal("three", entry.Action),
            entry => Assert.Equal("four", entry.Action));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ReadLog_ReturnsEmptyWhenMaxRowsIsNotPositive(int maxRows)
    {
        var path = WriteLogLines("{\"action\":\"one\"}");

        var entries = LogReader.ReadLog(path, maxRows);

        Assert.Empty(entries);
    }

    [Fact]
    public void FormatAndMaskJson_MasksPasswordKeysCaseInsensitively()
    {
        var masked = LogReader.FormatAndMaskJson("{\"user\":\"admin\",\"password\":\"secret\",\"Password\":\"another\"}");

        Assert.Contains("\"password\": \"***\"", masked);
        Assert.Contains("\"Password\": \"***\"", masked);
        Assert.DoesNotContain("secret", masked);
        Assert.DoesNotContain("another", masked);
    }

    private static string WriteLogLines(params string[] lines)
    {
        var path = Path.Combine(Path.GetTempPath(), $"managead-log-{Guid.NewGuid():N}.jsonl");
        File.WriteAllLines(path, lines);
        return path;
    }
}
