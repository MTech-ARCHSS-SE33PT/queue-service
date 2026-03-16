namespace QueueService.Infrastructure;

internal static class DotEnv
{
    public static bool TryGetValue(string path, string key, out string? value)
    {
        value = null;

        if (!File.Exists(path))
            return false;

        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var equalsIndex = line.IndexOf('=');
            if (equalsIndex <= 0)
                continue;

            var parsedKey = line[..equalsIndex].Trim();
            if (parsedKey.Length == 0)
                continue;

            if (!string.Equals(parsedKey, key, StringComparison.Ordinal))
                continue;

            var parsedValue = line[(equalsIndex + 1)..].Trim();

            if ((parsedValue.StartsWith('"') && parsedValue.EndsWith('"')) ||
                (parsedValue.StartsWith('\'') && parsedValue.EndsWith('\'')))
            {
                parsedValue = parsedValue[1..^1];
            }

            value = parsedValue;
            return true;
        }

        return false;
    }

    public static void Load(string path, bool overwrite = false)
    {
        if (!File.Exists(path))
            return;

        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var equalsIndex = line.IndexOf('=');
            if (equalsIndex <= 0)
                continue;

            var key = line[..equalsIndex].Trim();
            if (key.Length == 0)
                continue;

            var value = line[(equalsIndex + 1)..].Trim();

            if ((value.StartsWith('"') && value.EndsWith('"')) ||
                (value.StartsWith('\'') && value.EndsWith('\'')))
            {
                value = value[1..^1];
            }

            if (!overwrite && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                continue;

            Environment.SetEnvironmentVariable(key, value);
        }
    }

    public static bool TryGetValueFromWellKnownLocations(
        string key,
        out string? value,
        string fileName = ".env",
        int maxParentDepth = 6)
    {
        foreach (var startDir in new[]
        {
            new DirectoryInfo(Directory.GetCurrentDirectory()),
            new DirectoryInfo(AppContext.BaseDirectory),
        })
        {
            var current = startDir;
            for (var depth = 0; depth <= maxParentDepth && current is not null; depth++)
            {
                var direct = Path.Combine(current.FullName, fileName);
                if (TryGetValue(direct, key, out value))
                    return true;

                var nested = Path.Combine(current.FullName, "QueueService", fileName);
                if (TryGetValue(nested, key, out value))
                    return true;

                current = current.Parent;
            }
        }

        value = null;
        return false;
    }

    public static void LoadFromWellKnownLocations(string fileName = ".env", bool overwrite = false, int maxParentDepth = 6)
    {
        foreach (var startDir in new[]
        {
            new DirectoryInfo(Directory.GetCurrentDirectory()),
            new DirectoryInfo(AppContext.BaseDirectory),
        })
        {
            var current = startDir;
            for (var depth = 0; depth <= maxParentDepth && current is not null; depth++)
            {
                var direct = Path.Combine(current.FullName, fileName);
                if (File.Exists(direct))
                {
                    Load(direct, overwrite);
                    return;
                }

                var nested = Path.Combine(current.FullName, "QueueService", fileName);
                if (File.Exists(nested))
                {
                    Load(nested, overwrite);
                    return;
                }

                current = current.Parent;
            }
        }
    }
}
