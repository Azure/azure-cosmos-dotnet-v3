//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.MSBuild
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Integration tests that verify Windows native DLLs are only copied when publishing
    /// for Windows RuntimeIdentifiers, and not for Linux/macOS targets.
    /// These tests pack the SDK into a local NuGet package to properly test the .targets file behavior.
    /// </summary>
    [TestClass]
    [TestCategory("LongRunning")]
    public class CosmosTargetsPublishTests
    {
        private static string testProjectsRoot;
        private static string localNugetPackagePath;
        private static string packageVersion;
        private static readonly string[] WindowsNativeDlls = new[]
        {
            "Microsoft.Azure.Cosmos.ServiceInterop.dll",
            "Cosmos.CRTCompat.dll",
            "msvcp140.dll",
            "vcruntime140.dll",
            "vcruntime140_1.dll"
        };

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
        {
            testProjectsRoot = Path.Combine(Path.GetTempPath(), "CosmosTargetsTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testProjectsRoot);

            // Create local NuGet package from the SDK
            CreateLocalNuGetPackage();
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            if (Directory.Exists(testProjectsRoot))
            {
                try
                {
                    Directory.Delete(testProjectsRoot, recursive: true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        /// <summary>
        /// Tests that Windows native DLLs are not copied when publishing for non-Windows platforms.
        /// </summary>
        /// <param name="runtimeIdentifier">The runtime identifier to test (e.g., linux-x64, osx-x64).</param>
        [TestMethod]
        [DataRow("linux-x64")]
        [DataRow("linux-arm64")]
        [DataRow("osx-x64")]
        [DataRow("osx-arm64")]
        public void Publish_WithNonWindowsRuntimeIdentifier_DoesNotCopyWindowsDlls(string runtimeIdentifier)
        {
            string projectPath = this.CreateTestProject($"NonWinTest_{runtimeIdentifier}");
            string publishPath = this.PublishProject(projectPath, runtimeIdentifier);

            this.AssertWindowsDllsNotPresent(publishPath, runtimeIdentifier);
        }

        /// <summary>
        /// Tests that Windows native DLLs are copied when publishing for Windows platforms.
        /// </summary>
        /// <param name="runtimeIdentifier">The runtime identifier to test (e.g., win-x64, win-x86).</param>
        [TestMethod]
        [DataRow("win-x64")]
        [DataRow("win-x86")]
        [DataRow("win-arm64")]
        public void Publish_WithWindowsRuntimeIdentifier_CopiesWindowsDlls(string runtimeIdentifier)
        {
            string projectPath = this.CreateTestProject($"WinTest_{runtimeIdentifier}");
            string publishPath = this.PublishProject(projectPath, runtimeIdentifier);

            this.AssertWindowsDllsPresent(publishPath, runtimeIdentifier);
        }

        /// <summary>
        /// Tests that Windows native DLLs are copied when publishing without a RuntimeIdentifier,
        /// which is the most common developer scenario (regular 'dotnet publish' without -r).
        /// </summary>
        [TestMethod]
        public void Publish_WithoutRuntimeIdentifier_CopiesWindowsDlls()
        {
            string projectPath = this.CreateTestProject("NoRidTest");
            string publishPath = this.PublishProject(projectPath, runtimeIdentifier: null);

            this.AssertWindowsDllsPresent(publishPath, "no RuntimeIdentifier");
        }

        private static void CreateLocalNuGetPackage()
        {
            string repoRoot = GetRepositoryRoot();
            string cosmosProjectPath = Path.Combine(repoRoot, "Microsoft.Azure.Cosmos", "src", "Microsoft.Azure.Cosmos.csproj");
            string packOutputDir = Path.Combine(testProjectsRoot, "nuget-packages");
            Directory.CreateDirectory(packOutputDir);

            // Use a unique version to avoid cache conflicts
            packageVersion = $"99.0.0-test.{DateTime.UtcNow:yyyyMMddHHmmss}";

            // Pack the SDK project
            ProcessStartInfo packInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"pack \"{cosmosProjectPath}\" -c Release -o \"{packOutputDir}\" /p:Version={packageVersion} /p:PackageVersion={packageVersion}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Console.WriteLine($"Packing SDK: dotnet {packInfo.Arguments}");

            Process packProcess = Process.Start(packInfo);
            if (packProcess == null)
            {
                Assert.Fail("Failed to start dotnet pack process");
            }

            using (packProcess)
            {
                Task<string> outputTask = packProcess.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = packProcess.StandardError.ReadToEndAsync();

                bool exited = packProcess.WaitForExit((int)TimeSpan.FromMinutes(10).TotalMilliseconds);
                if (!exited)
                {
                    packProcess.Kill();
                    Assert.Fail("dotnet pack timed out after 10 minutes");
                }

                string output = outputTask.GetAwaiter().GetResult();
                string error = errorTask.GetAwaiter().GetResult();

                if (packProcess.ExitCode != 0)
                {
                    Assert.Fail($"dotnet pack failed.\nOutput: {output}\nError: {error}");
                }

                Assert.IsTrue(string.IsNullOrEmpty(error), $"dotnet pack had unexpected error output:\n{error}");
                Console.WriteLine($"Pack succeeded. Output: {output}");
            }

            localNugetPackagePath = packOutputDir;
        }

        private string CreateTestProject(string projectName)
        {
            string projectDir = Path.Combine(testProjectsRoot, projectName);
            Directory.CreateDirectory(projectDir);

            string projectFile = Path.Combine(projectDir, $"{projectName}.csproj");
            string programFile = Path.Combine(projectDir, "Program.cs");
            string nugetConfigFile = Path.Combine(projectDir, "nuget.config");

            // Create nuget.config to use local package source
            File.WriteAllText(nugetConfigFile, $@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <clear />
    <add key=""local"" value=""{localNugetPackagePath}"" />
    <add key=""nuget.org"" value=""https://api.nuget.org/v3/index.json"" />
  </packageSources>
</configuration>");

            // Create a simple console app project that references the local NuGet package
            File.WriteAllText(projectFile, $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include=""Microsoft.Azure.Cosmos"" Version=""{packageVersion}"" />
    <PackageReference Include=""Newtonsoft.Json"" Version=""13.0.3"" />
  </ItemGroup>
</Project>");

            // Create a minimal Program.cs
            File.WriteAllText(programFile, @"System.Console.WriteLine(""Test app for verifying Cosmos SDK package behavior"");");

            return projectFile;
        }

        private string PublishProject(string projectFile, string runtimeIdentifier)
        {
            string projectDir = Path.GetDirectoryName(projectFile);
            string publishDir = Path.Combine(projectDir, "bin", "publish", runtimeIdentifier ?? "no-rid");

            // Run dotnet publish
            string ridArgument = runtimeIdentifier != null ? $"-r {runtimeIdentifier} --self-contained false" : string.Empty;
            ProcessStartInfo processInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"publish \"{projectFile}\" -c Release -o \"{publishDir}\" {ridArgument}".Trim(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Trace the command being executed for debugging
            string commandLine = $"{processInfo.FileName} {processInfo.Arguments}";
            Console.WriteLine($"Executing: {commandLine}");
            Console.WriteLine($"Working directory: {projectDir}");

            Process process = Process.Start(processInfo);
            if (process == null)
            {
                Assert.Fail($"Failed to start dotnet publish process for {runtimeIdentifier ?? "no RID"}");
            }

            using (process!)
            {
                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();

                bool exited = process.WaitForExit((int)TimeSpan.FromMinutes(5).TotalMilliseconds);
                if (!exited)
                {
                    process.Kill();
                    Assert.Fail($"dotnet publish timed out after 5 minutes for {runtimeIdentifier ?? "no RID"}.\nCommand: {commandLine}");
                }

                string output = outputTask.GetAwaiter().GetResult();
                string error = errorTask.GetAwaiter().GetResult();

                if (process.ExitCode != 0)
                {
                    Assert.Fail($"dotnet publish failed for {runtimeIdentifier ?? "no RID"}.\nCommand: {commandLine}\nOutput: {output}\nError: {error}");
                }

                Assert.IsTrue(string.IsNullOrEmpty(error), $"dotnet publish had unexpected error output:\n{error}");
                Console.WriteLine($"Publish succeeded for {runtimeIdentifier ?? "no RID"}. Exit code: {process.ExitCode}");
            }

            return publishDir;
        }

        private void AssertWindowsDllsNotPresent(string publishPath, string runtimeIdentifier)
        {
            Assert.IsTrue(Directory.Exists(publishPath), $"Publish directory does not exist: {publishPath}");

            foreach (string dll in WindowsNativeDlls)
            {
                string dllPath = Path.Combine(publishPath, dll);
                Assert.IsFalse(File.Exists(dllPath),
                    $"Windows native DLL '{dll}' should NOT be present when publishing for {runtimeIdentifier}, but was found at: {dllPath}");
            }
        }

        private void AssertWindowsDllsPresent(string publishPath, string runtimeIdentifier)
        {
            Assert.IsTrue(Directory.Exists(publishPath), $"Publish directory does not exist: {publishPath}");

            foreach (string dll in WindowsNativeDlls)
            {
                string dllPath = Path.Combine(publishPath, dll);
                Assert.IsTrue(File.Exists(dllPath),
                    $"Windows native DLL '{dll}' SHOULD be present when publishing for {runtimeIdentifier}, but was NOT found at: {dllPath}");
            }
        }

        private static string GetRepositoryRoot()
        {
            string currentDir = AppDomain.CurrentDomain.BaseDirectory;

            while (currentDir != null && !File.Exists(Path.Combine(currentDir, "Microsoft.Azure.Cosmos.sln")))
            {
                DirectoryInfo parent = Directory.GetParent(currentDir);
                if (parent == null)
                {
                    break;
                }
                currentDir = parent.FullName;
            }

            Assert.IsNotNull(currentDir, "Could not find repository root");
            return currentDir;
        }
    }
}
