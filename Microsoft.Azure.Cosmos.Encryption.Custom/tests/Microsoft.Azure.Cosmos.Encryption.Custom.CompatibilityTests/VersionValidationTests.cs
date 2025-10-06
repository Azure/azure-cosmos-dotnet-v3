namespace Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using FluentAssertions;
    using Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests.SideBySide;
    using Xunit;
    using Xunit.Abstractions;

    /// <summary>
    /// Tests to validate that we're actually testing different versions
    /// and not accidentally testing the same version twice.
    /// </summary>
    public class VersionValidationTests : CompatibilityTestBase
    {
        public VersionValidationTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void VersionMatrix_ShouldContainDistinctVersions()
        {
            // Arrange
            var versions = VersionMatrix.GetTestVersions();
            
            this.Output.WriteLine($"Version matrix contains {versions.Length} version(s):");
            foreach (var version in versions)
            {
                this.Output.WriteLine($"  - {version}");
            }

            // Act & Assert
            var distinctVersions = versions.Distinct().ToArray();
            
            versions.Length.Should().Be(distinctVersions.Length, 
                "version matrix should not contain duplicate versions");
        }

        [Fact]
        public void LoadedVersions_ShouldHaveDifferentAssemblyVersions()
        {
            // Arrange
            var versions = VersionMatrix.GetTestVersions();

            if (versions.Length < 2)
            {
                this.Output.WriteLine("⚠️  Only one version in matrix, skipping cross-version validation");
                return;
            }

            var loadedVersionInfo = new Dictionary<string, string>();

            // Act - Load each version and get its assembly version
            foreach (var packageVersion in versions)
            {
                var resolvedVersion = VersionMatrix.ResolveVersion(packageVersion);
                using (var loader = VersionLoader.Load(resolvedVersion))
                {
                    var assembly = loader.Assembly;
                    var assemblyVersion = assembly.GetName().Version?.ToString() ?? "unknown";
                    var infoVersion = assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                        .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
                        .FirstOrDefault()?.InformationalVersion ?? assemblyVersion;

                    loadedVersionInfo[packageVersion] = infoVersion;
                    
                    this.Output.WriteLine($"Package {packageVersion} (resolved to {resolvedVersion}):");
                    this.Output.WriteLine($"  Assembly Version: {assemblyVersion}");
                    this.Output.WriteLine($"  Informational Version: {infoVersion}");
                }
            }

            // Assert - Verify all versions are different
            var distinctActualVersions = loadedVersionInfo.Values.Distinct().ToList();
            
            loadedVersionInfo.Values.Count.Should().Be(distinctActualVersions.Count,
                because: "each package version should load a different assembly version");

            this.Output.WriteLine("");
            this.Output.WriteLine("✅ Validation Result:");
            this.Output.WriteLine($"   Testing {loadedVersionInfo.Count} distinct version(s)");
            
            foreach (var kvp in loadedVersionInfo)
            {
                this.Output.WriteLine($"   • {kvp.Key} → {kvp.Value}");
            }
        }

        [Fact]
        public void Preview07_And_Preview08_ShouldBeDistinct()
        {
            // Arrange
            var versions = VersionMatrix.GetTestVersions();

            var preview07 = versions.FirstOrDefault(v => v.Contains("preview07"));
            var preview08 = versions.FirstOrDefault(v => v.Contains("preview08"));

            if (preview07 == null || preview08 == null)
            {
                this.Output.WriteLine("⚠️  Both preview07 and preview08 not found in version matrix");
                this.Output.WriteLine($"   Versions in matrix: {string.Join(", ", versions)}");
                return; // Skip if we don't have both versions
            }

            // Act
            string version07Info;
            string version08Info;

            var resolved07 = VersionMatrix.ResolveVersion(preview07);
            var resolved08 = VersionMatrix.ResolveVersion(preview08);

            using (var loader07 = VersionLoader.Load(resolved07))
            {
                var infoAttr = loader07.Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                    .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
                    .FirstOrDefault();
                version07Info = infoAttr?.InformationalVersion ?? loader07.Assembly.GetName().Version?.ToString() ?? "unknown";
            }

            using (var loader08 = VersionLoader.Load(resolved08))
            {
                var infoAttr = loader08.Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                    .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
                    .FirstOrDefault();
                version08Info = infoAttr?.InformationalVersion ?? loader08.Assembly.GetName().Version?.ToString() ?? "unknown";
            }

            // Assert
            this.Output.WriteLine($"Preview07 ({preview07}) resolved to: {resolved07}");
            this.Output.WriteLine($"  Actual version: {version07Info}");
            this.Output.WriteLine($"Preview08 ({preview08}) resolved to: {resolved08}");
            this.Output.WriteLine($"  Actual version: {version08Info}");

            version07Info.Should().NotBe(version08Info, 
                because: $"preview07 and preview08 must be different versions. Got preview07={version07Info}, preview08={version08Info}");

            this.Output.WriteLine("✅ Confirmed: preview07 and preview08 are distinct versions");
        }
    }
}
