using QueueService.Infrastructure;

namespace QueueService.Tests;

public sealed class DotEnvTests
{
    [Fact]
    public void TryGetValue_ParsesCommentsWhitespaceQuotesAndMissingKeys()
    {
        var path = WriteTempEnv(
            "",
            "# ignored",
            " ASB_CONN = \"Endpoint=sb://example/\" ",
            "ASB_TOPIC='queue-topic'",
            "INVALID",
            "=missing-key",
            "EMPTY_KEY = value");

        var foundConnection = DotEnv.TryGetValue(path, "ASB_CONN", out var connection);
        var foundTopic = DotEnv.TryGetValue(path, "ASB_TOPIC", out var topic);
        var missing = DotEnv.TryGetValue(path, "ASB_SUB", out var sub);

        Assert.True(foundConnection);
        Assert.Equal("Endpoint=sb://example/", connection);
        Assert.True(foundTopic);
        Assert.Equal("queue-topic", topic);
        Assert.False(missing);
        Assert.Null(sub);
    }

    [Fact]
    public void TryGetValue_ReturnsFalseForMissingFile()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), ".env");

        var found = DotEnv.TryGetValue(missingPath, "ASB_CONN", out var value);

        Assert.False(found);
        Assert.Null(value);
    }

    [Fact]
    public void Load_SetsVariablesAndRespectsOverwriteFlag()
    {
        var key = $"QUEUE_TEST_{Guid.NewGuid():N}";
        var path = WriteTempEnv($"{key}=from-file");

        Environment.SetEnvironmentVariable(key, "existing");
        DotEnv.Load(path);
        Assert.Equal("existing", Environment.GetEnvironmentVariable(key));

        DotEnv.Load(path, overwrite: true);
        Assert.Equal("from-file", Environment.GetEnvironmentVariable(key));

        Environment.SetEnvironmentVariable(key, null);
    }

    [Fact]
    public void LoadFromWellKnownLocations_LoadsNestedEnvFileFromCurrentDirectory()
    {
        var originalDirectory = Directory.GetCurrentDirectory();
        var key = $"QUEUE_TEST_{Guid.NewGuid():N}";
        var root = Directory.CreateTempSubdirectory();
        var nested = Directory.CreateDirectory(Path.Combine(root.FullName, "QueueService"));
        File.WriteAllLines(Path.Combine(nested.FullName, ".queue-test.env"), new[] { $"{key}=nested-value" });

        try
        {
            Directory.SetCurrentDirectory(root.FullName);

            DotEnv.LoadFromWellKnownLocations(".queue-test.env", maxParentDepth: 0);

            Assert.Equal("nested-value", Environment.GetEnvironmentVariable(key));
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
            Directory.SetCurrentDirectory(originalDirectory);
            root.Delete(recursive: true);
        }
    }

    private static string WriteTempEnv(params string[] lines)
    {
        var directory = Directory.CreateTempSubdirectory();
        var path = Path.Combine(directory.FullName, ".env");
        File.WriteAllLines(path, lines);
        return path;
    }
}
