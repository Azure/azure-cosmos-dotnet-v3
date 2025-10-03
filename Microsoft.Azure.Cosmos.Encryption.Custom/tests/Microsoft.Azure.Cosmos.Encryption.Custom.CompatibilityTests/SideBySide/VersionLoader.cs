using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests.SideBySide
{
    /// <summary>
    /// Loads a specific version of the Encryption.Custom library from NuGet packages cache.
    /// Enables cross-version testing by loading different versions side-by-side.
    /// </summary>
    public sealed class VersionLoader : IDisposable
    {
        private readonly IsolatedLoadContext loadContext;

        public Assembly Assembly { get; }
        public string Version { get; }

        private VersionLoader(string version, Assembly assembly, IsolatedLoadContext context)
        {
            this.Version = version;
            this.Assembly = assembly;
            this.loadContext = context;
        }

        /// <summary>
        /// Loads a specific version from the NuGet packages cache.
        /// </summary>
        public static VersionLoader Load(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentException("Version cannot be null or empty", nameof(version));
            }

            string packagePath = GetPackagePath(version);
            if (!Directory.Exists(packagePath))
            {
                throw new InvalidOperationException(
                    $"Package version {version} not found at {packagePath}. " +
                    "Ensure the package is restored before running tests.");
            }

            // Find the assembly DLL (prefer netstandard2.0)
            string assemblyPath = FindAssemblyPath(packagePath);
            if (!File.Exists(assemblyPath))
            {
                throw new InvalidOperationException(
                    $"Assembly not found for version {version} at {assemblyPath}");
            }

            // Load in isolated context
            var context = new IsolatedLoadContext(assemblyPath, $"CompatTest-{version}");
            var assembly = context.LoadFromAssemblyPath(assemblyPath);

            return new VersionLoader(version, assembly, context);
        }

        /// <summary>
        /// Gets a type from the loaded assembly by full name.
        /// </summary>
        public Type GetType(string fullTypeName)
        {
            if (string.IsNullOrWhiteSpace(fullTypeName))
            {
                throw new ArgumentException("Type name cannot be null or empty", nameof(fullTypeName));
            }

            var type = this.Assembly.GetType(fullTypeName);
            if (type == null)
            {
                // Try to find it in exported types
                type = this.Assembly.GetExportedTypes()
                    .FirstOrDefault(t => t.FullName == fullTypeName);
            }

            return type;
        }

        private static string GetPackagePath(string version)
        {
            string globalPackagesPath = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
            
            if (string.IsNullOrWhiteSpace(globalPackagesPath))
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                globalPackagesPath = Path.Combine(userProfile, ".nuget", "packages");
            }

            return Path.Combine(
                globalPackagesPath,
                "microsoft.azure.cosmos.encryption.custom",
                version.ToLowerInvariant());
        }

        private static string FindAssemblyPath(string packagePath)
        {
            // Prefer netstandard2.0
            string libPath = Path.Combine(packagePath, "lib", "netstandard2.0", "Microsoft.Azure.Cosmos.Encryption.Custom.dll");
            if (File.Exists(libPath))
            {
                return libPath;
            }

            // Fallback: search all lib folders
            string libDir = Path.Combine(packagePath, "lib");
            if (Directory.Exists(libDir))
            {
                var dllPath = Directory.GetFiles(libDir, "Microsoft.Azure.Cosmos.Encryption.Custom.dll", SearchOption.AllDirectories)
                    .FirstOrDefault();
                if (dllPath != null)
                {
                    return dllPath;
                }
            }

            throw new FileNotFoundException(
                $"Could not find Microsoft.Azure.Cosmos.Encryption.Custom.dll in package at {packagePath}");
        }

        public void Dispose()
        {
            this.loadContext?.Unload();
        }
    }
}
