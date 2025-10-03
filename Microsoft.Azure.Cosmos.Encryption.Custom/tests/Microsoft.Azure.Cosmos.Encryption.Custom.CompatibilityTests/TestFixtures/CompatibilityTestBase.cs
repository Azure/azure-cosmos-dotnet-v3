namespace Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests.TestFixtures
{
    using System;
    using System.Reflection;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Xunit;
    using Xunit.Abstractions;

    /// <summary>
    /// Base class for all compatibility tests with common setup/teardown and utilities.
    /// </summary>
    public abstract class CompatibilityTestBase : IDisposable
    {
        protected ITestOutputHelper Output { get; }
        protected string TestedVersion { get; }

        protected CompatibilityTestBase(ITestOutputHelper output)
        {
            this.Output = output ?? throw new ArgumentNullException(nameof(output));
            this.TestedVersion = GetPackageVersion();

            this.Output.WriteLine($"========================================");
            this.Output.WriteLine($"Testing Version: {this.TestedVersion}");
            this.Output.WriteLine($"Test: {this.GetType().Name}");
            this.Output.WriteLine($"========================================");
        }

        /// <summary>
        /// Gets the actual version of the package being tested.
        /// </summary>
        private static string GetPackageVersion()
        {
            Assembly assembly = typeof(DataEncryptionKeyProperties).Assembly;
            Version version = assembly.GetName().Version;

            // Try to get informational version (includes preview suffix)
            AssemblyInformationalVersionAttribute infoVersionAttr = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            return infoVersionAttr?.InformationalVersion ?? version?.ToString() ?? "Unknown";
        }

        protected void LogInfo(string message)
        {
            this.Output.WriteLine($"[INFO] {message}");
        }

        protected void LogWarning(string message)
        {
            this.Output.WriteLine($"[WARN] {message}");
        }

        protected void LogError(string message)
        {
            this.Output.WriteLine($"[ERROR] {message}");
        }

        public virtual void Dispose()
        {
            // Cleanup logic if needed
        }
    }
}
