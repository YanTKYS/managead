using System.Text.Json;
using Xunit;
using ManageAdTool.Models;
using ManageAdTool.Services;

namespace ManageAdTool.Tests;

public class AuditLoggerTests
{
    [Fact]
    public void AuthAuditLogger_DoesNotWritePasswordField()
    {
        var directory = CreateTempDirectory();
        var logger = new AuthAuditLogger(Path.Combine(directory, "audit.jsonl"));

        logger.Log("Login", "admin@example.local", success: true, message: "ログイン成功");

        using var doc = ReadSingleJsonLine(Path.Combine(directory, "auth.jsonl"));
        Assert.False(doc.RootElement.TryGetProperty("password", out _));
        Assert.Equal("Login", doc.RootElement.GetProperty("action").GetString());
        Assert.DoesNotContain("password", doc.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WriteAuditLogger_WritesTargetTypeOperationNameAndLdapAttribute()
    {
        var directory = CreateTempDirectory();
        var logger = new WriteAuditLogger(Path.Combine(directory, "audit.jsonl"));
        var entry = new WriteAuditEntry
        {
            ServiceMode = "DirectoryReadOnly",
            Executor = "operator",
            MachineName = "CLIENT01",
            TargetType = "Computer",
            TargetName = "PC-001",
            OperationName = "UpdateComputerDescription",
            Changes = new[]
            {
                new FieldChange("説明 (description)", "old", "new") { LdapAttribute = "description" }
            },
            Success = true
        };

        var success = logger.Log(entry);

        Assert.True(success);
        using var doc = ReadSingleJsonLine(Path.Combine(directory, "write-audit.jsonl"));
        Assert.Equal("Computer", doc.RootElement.GetProperty("targetType").GetString());
        Assert.Equal("UpdateComputerDescription", doc.RootElement.GetProperty("operationName").GetString());
        var change = doc.RootElement.GetProperty("changes")[0];
        Assert.Equal("description", change.GetProperty("ldapAttribute").GetString());
    }

    [Fact]
    public void WriteAuditLogger_ReturnsFalseWhenWriteFails()
    {
        var directory = CreateTempDirectory();
        Directory.CreateDirectory(Path.Combine(directory, "write-audit.jsonl"));
        var logger = new WriteAuditLogger(Path.Combine(directory, "audit.jsonl"));

        var success = logger.Log(new WriteAuditEntry());

        Assert.False(success);
    }

    [Fact]
    public void ReferenceAuditLogger_WritesExecutorAndMachineName()
    {
        var directory = CreateTempDirectory();
        var path = Path.Combine(directory, "audit.jsonl");
        var logger = new ReferenceAuditLogger(path, executor: "operator", machineName: "CLIENT01");

        logger.Log("SearchUsers", "sato", resultCount: 1, success: true, message: "ok");

        using var doc = ReadSingleJsonLine(path);
        Assert.Equal("operator", doc.RootElement.GetProperty("executor").GetString());
        Assert.Equal("CLIENT01", doc.RootElement.GetProperty("machineName").GetString());
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"managead-audit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static JsonDocument ReadSingleJsonLine(string path)
        => JsonDocument.Parse(File.ReadLines(path).Single());
}
