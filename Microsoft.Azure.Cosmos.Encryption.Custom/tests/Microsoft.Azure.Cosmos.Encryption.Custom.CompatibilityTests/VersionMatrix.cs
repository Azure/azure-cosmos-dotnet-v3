using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests;

/// <summary>
/// Manages the version matrix for compatibility testing.
/// Handles both published NuGet versions and the current source code ("current").
/// </summary>
public static class VersionMatrix
{
    private static readonly Lazy<TestConfig> LazyConfig = new(() => LoadConfig());
    private static readonly object BuildLock = new();
    private static string? currentVersionNumber;

    public static TestConfig Config => LazyConfig.Value;

    public static string[] GetTestVersions()
    {
        return Config.VersionMatrix.Versions;
    }

    public static string GetBaselineVersion()
    {
        return Config.VersionMatrix.Baseline;
    }

    /// <summary>
    /// Gets the actual version number for a version string.
    /// For "current", builds the package and returns the version number.
    /// For other versions, returns the version string as-is.
    /// </summary>
    public static string ResolveVersion(string version)
    {
        if (string.Equals(version, "current", StringComparison.OrdinalIgnoreCase))
        {
            return BuildCurrentPackage();
        }
        return version;
    }

    /// <summary>
    /// Checks if a version represents the current source code.
    /// </summary>
    public static bool IsCurrentVersion(string version)
    {
        return string.Equals(version, "current", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Builds a package from the current source code and returns its version number.
    /// The package is built once and cached for subsequent calls.
    /// </summary>
    private static string BuildCurrentPackage()
    {
        lock (BuildLock)
        {
            if (currentVersionNumber != null)
            {
                return currentVersionNumber;
            }

            Console.WriteLine("🔨 Building package from current source...");

            var config = Config.VersionMatrix;
            var projectPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory, config.CurrentSourcePath ?? "../../../../Microsoft.Azure.Cosmos.Encryption.Custom/src/Microsoft.Azure.Cosmos.Encryption.Custom.csproj"));
            
            var outputPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory, config.LocalPackageOutputPath ?? "../../../../../artifacts/local-packages"));

            if (!File.Exists(projectPath))
            {
                throw new FileNotFoundException($"Source project not found at: {projectPath}");
            }

            // Ensure output directory exists
            Directory.CreateDirectory(outputPath);

            // Determine version from Directory.Build.props or use timestamp-based version
            var versionSuffix = $"current-{DateTime.UtcNow:yyyyMMddHHmmss}";
            var fullVersion = $"1.0.0-{versionSuffix}";
            
            // Build the package - use CustomEncryptionVersion to override the version
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"pack \"{projectPath}\" --configuration Release --output \"{outputPath}\" -p:CustomEncryptionVersion={fullVersion}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start dotnet pack process");
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                Console.WriteLine($"Build output: {output}");
                Console.WriteLine($"Build errors: {error}");
                throw new InvalidOperationException($"Failed to build current source package. Exit code: {process.ExitCode}");
            }

            // Find the built package to determine version
            var packageFiles = Directory.GetFiles(outputPath, "Microsoft.Azure.Cosmos.Encryption.Custom.*.nupkg")
                .Where(f => !f.EndsWith(".symbols.nupkg"))
                .OrderByDescending(File.GetCreationTimeUtc)
                .ToArray();

            if (packageFiles.Length == 0)
            {
                throw new FileNotFoundException($"No package found in {outputPath} after build");
            }

            var latestPackage = packageFiles[0];
            var packageName = Path.GetFileNameWithoutExtension(latestPackage);
            
            // Extract version from package name: Microsoft.Azure.Cosmos.Encryption.Custom.1.0.0-current-20251003123456.nupkg
            var versionPart = packageName.Replace("Microsoft.Azure.Cosmos.Encryption.Custom.", "");
            currentVersionNumber = versionPart;

            Console.WriteLine($"✅ Built package version: {currentVersionNumber}");
            Console.WriteLine($"   Location: {latestPackage}");

            return currentVersionNumber;
        }
    }

    private static TestConfig LoadConfig()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "testconfig.json");
        if (!File.Exists(configPath))
        {
            // Fallback to default configuration - only test preview07 and newer
            return new TestConfig
            {
                VersionMatrix = new VersionMatrixConfig
                {
                    Baseline = "1.0.0-preview07",
                    Versions = new[] { "1.0.0-preview07" },
                    CurrentSourcePath = "../../../src/Microsoft.Azure.Cosmos.Encryption.Custom.csproj",
                    LocalPackageOutputPath = "../../../../artifacts/local-packages"
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
    public string? CurrentSourcePath { get; set; }
    public string? LocalPackageOutputPath { get; set; }
}
