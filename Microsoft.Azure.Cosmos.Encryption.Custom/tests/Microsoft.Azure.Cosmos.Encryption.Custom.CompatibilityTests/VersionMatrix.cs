using System;
using System.IO;
using System.Text.Json;

namespace Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests;

/// <summary>
/// Manages the version matrix for compatibility testing.
/// </summary>
public static class VersionMatrix
{
    private static readonly Lazy<TestConfig> LazyConfig = new(() => LoadConfig());

    public static TestConfig Config => LazyConfig.Value;

    public static string[] GetTestVersions()
    {
        return Config.VersionMatrix.Versions;
    }

    public static string GetBaselineVersion()
    {
        return Config.VersionMatrix.Baseline;
    }

    private static TestConfig LoadConfig()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "testconfig.json");
        if (!File.Exists(configPath))
        {
            // Fallback to default configuration
            return new TestConfig
            {
                VersionMatrix = new VersionMatrixConfig
                {
                    Baseline = "1.0.0-preview07",
                    Versions = new[] { "1.0.0-preview07", "1.0.0-preview06", "1.0.0-preview05" }
                }
            };
        }

        var json = File.ReadAllText(configPath);
        return JsonSerializer.Deserialize<TestConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Failed to load test configuration");
    }
}

public class TestConfig
{
    public VersionMatrixConfig VersionMatrix { get; set; } = new();
}

public class VersionMatrixConfig
{
    public string Baseline { get; set; } = string.Empty;
    public string[] Versions { get; set; } = Array.Empty<string>();
}
